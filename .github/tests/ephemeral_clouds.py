"""Provision only disposable, loopback-bound CI clouds; no shared-server credentials."""
import base64
import json
import http.client
import os
import secrets
import subprocess
import sys
import tempfile
import time
import urllib.error
import urllib.request

CONTAINERS = ("ocs-versions-nextcloud", "ocs-versions-is")
VOLUMES = ("ocs-versions-nextcloud-data", "ocs-versions-is-config", "ocs-versions-is-data")
OWNER_LABEL = "ocs.version-acceptance"
OWNER = os.environ.get("GITHUB_RUN_ID", "local") + "/" + os.environ.get("GITHUB_RUN_ATTEMPT", "0")


def run(*args, quiet=False):
    subprocess.run(args, check=True, stdout=subprocess.DEVNULL if quiet else None,
                   stderr=subprocess.DEVNULL if quiet else None)


def get(url, user, password):
    request = urllib.request.Request(url, headers={
        "Authorization": "Basic " + base64.b64encode((user + ":" + password).encode()).decode(),
        "OCS-APIRequest": "true"})
    # Do not forward authentication on redirects.
    class NoRedirect(urllib.request.HTTPRedirectHandler):
        def redirect_request(self, req, fp, code, msg, headers, newurl):
            return None
    with urllib.request.build_opener(NoRedirect).open(request, timeout=10) as response:
        return json.load(response)


def ready(url, user, password):
    deadline = time.monotonic() + 180
    last = "no response"
    while time.monotonic() < deadline:
        try:
            data = get(url, user, password)
            status = str(data.get("ocs", {}).get("meta", {}).get("statuscode"))
            if status in ("100", "200"):
                return data
            last = "OCS status " + status
        except (urllib.error.URLError, ValueError, TimeoutError, ConnectionError, http.client.HTTPException) as error:
            last = "HTTP status " + str(error.code) if isinstance(error, urllib.error.HTTPError) else type(error).__name__
        time.sleep(2)
    for name in CONTAINERS:
        subprocess.run(("docker", "inspect", "--format", "{{.State.Status}} {{.State.ExitCode}}", name), check=False)
    raise RuntimeError("Disposable cloud did not become ready: " + last + "; no shared server was contacted.")


def export(values):
    with open(os.environ["GITHUB_ENV"], "a", encoding="utf-8") as output:
        for name, value in values.items():
            if "\n" in value or "\r" in value:
                raise ValueError("Invalid CI environment value.")
            output.write(name + "=" + value + "\n")


def setup():
    if os.environ.get("GITHUB_ACTIONS") != "true":
        raise RuntimeError("This provisioner is restricted to a disposable GitHub runner.")
    for name in CONTAINERS:
        found = subprocess.run(("docker", "container", "inspect", name), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        if found.returncode == 0:
            raise RuntimeError("An owned container already exists; refusing uncertain setup.")
    for name in VOLUMES:
        found = subprocess.run(("docker", "volume", "inspect", name), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL)
        if found.returncode == 0:
            raise RuntimeError("An owned volume already exists; refusing uncertain setup.")
        run("docker", "volume", "create", "--label", OWNER_LABEL + "=" + OWNER, name, quiet=True)
    password = "Test-" + secrets.token_hex(24)
    reader_password = "Reader-" + secrets.token_hex(24)
    print("::add-mask::" + password, flush=True)
    print("::add-mask::" + reader_password, flush=True)
    os.environ.update(NEXTCLOUD_ADMIN_USER="owner", NEXTCLOUD_ADMIN_PASSWORD=password,
                      SQLITE_DATABASE="cloud", NEXTCLOUD_TRUSTED_DOMAINS="127.0.0.1 localhost",
                      IDM_ADMIN_PASSWORD=password, OCIS_URL="https://localhost:9200",
                      OCIS_INSECURE="true", PROXY_TLS="true", PROXY_ENABLE_BASIC_AUTH="true",
                      PROXY_HTTP_ADDR="0.0.0.0:9200", OCIS_LOG_LEVEL="error", OC_PASS=reader_password)
    # The built-in IDP requires TLS when no TLS reverse proxy is present.
    tls = tempfile.mkdtemp(prefix="ocs-version-tls-", dir=os.environ["RUNNER_TEMP"])
    export({"OCS_EPHEMERAL_TLS_DIRECTORY": tls})
    os.chmod(tls, 0o755)
    cert, key = os.path.join(tls, "server.crt"), os.path.join(tls, "server.key")
    run("openssl", "req", "-x509", "-nodes", "-newkey", "rsa:2048", "-days", "1", "-subj", "/CN=localhost",
        "-addext", "subjectAltName=DNS:localhost,IP:127.0.0.1", "-keyout", key, "-out", cert, quiet=True)
    os.chmod(key, 0o444)
    run("sudo", "cp", cert, "/usr/local/share/ca-certificates/ocs-version-acceptance.crt", quiet=True)
    run("sudo", "update-ca-certificates", quiet=True)
    os.environ.update(PROXY_TRANSPORT_TLS_CERT="/run/ocs-test-tls/server.crt", PROXY_TRANSPORT_TLS_KEY="/run/ocs-test-tls/server.key")
    nc_env = sum((["-e", name] for name in ("NEXTCLOUD_ADMIN_USER", "NEXTCLOUD_ADMIN_PASSWORD", "SQLITE_DATABASE", "NEXTCLOUD_TRUSTED_DOMAINS")), [])
    run("docker", "run", "-d", "--name", CONTAINERS[0], "--label", OWNER_LABEL + "=" + OWNER, "-p", "127.0.0.1:8080:80",
        "-v", VOLUMES[0] + ":/var/www/html", *nc_env, "nextcloud:34.0.3-apache", quiet=True)
    is_env = sum((["-e", name] for name in ("IDM_ADMIN_PASSWORD", "OCIS_URL", "OCIS_INSECURE", "PROXY_TLS", "PROXY_ENABLE_BASIC_AUTH", "PROXY_HTTP_ADDR", "OCIS_LOG_LEVEL", "PROXY_TRANSPORT_TLS_CERT", "PROXY_TRANSPORT_TLS_KEY")), [])
    mounts = ("-v", VOLUMES[1] + ":/etc/ocis", "-v", VOLUMES[2] + ":/var/lib/ocis", "-v", tls + ":/run/ocs-test-tls:ro")
    run("docker", "run", "--rm", *mounts, *is_env, "owncloud/ocis:8.2.1", "init", quiet=True)
    run("docker", "run", "-d", "--name", CONTAINERS[1], "--label", OWNER_LABEL + "=" + OWNER, "-p", "127.0.0.1:9200:9200",
        *mounts, *is_env, "owncloud/ocis:8.2.1", "server", quiet=True)
    nc = ready("http://127.0.0.1:8080/ocs/v2.php/cloud/capabilities?format=json", "owner", password)
    ocis = ready("https://localhost:9200/ocs/v2.php/cloud/capabilities?format=json", "admin", password)
    run("docker", "exec", "-u", "www-data", "-e", "OC_PASS", CONTAINERS[0], "php", "occ", "user:add", "--password-from-env", "readonly", quiet=True)
    drives = get("https://localhost:9200/graph/v1.0/me/drives", "admin", password)["value"]
    personal = [drive for drive in drives if drive.get("driveType") == "personal"]
    if len(personal) != 1:
        raise RuntimeError("Expected exactly one owned personal space.")
    import urllib.parse
    space = urllib.parse.quote(personal[0]["id"], safe="")
    print("Disposable Nextcloud version:", nc["ocs"]["data"]["version"].get("string"))
    print("Disposable Infinite Scale version:", ocis["ocs"]["data"]["version"].get("productversion", "8.2.1 image"))
    export({"OCS_EPHEMERAL_ACCEPTANCE": "1", "OCS_VERSION_TESTS_EXCLUSIVE": "1",
            "TEST_NEXTCLOUD.STANDARDUSER_SERVERURL": "http://127.0.0.1:8080/",
            "TEST_NEXTCLOUD.STANDARDUSER_USERNAME": "owner", "TEST_NEXTCLOUD.STANDARDUSER_PASSWORD": password,
            "TEST_INFINITESCALE.STANDARDUSER_SERVERURL": "https://localhost:9200/",
            "TEST_INFINITESCALE.STANDARDUSER_USERNAME": "admin", "TEST_INFINITESCALE.STANDARDUSER_PASSWORD": password,
            "TEST_INFINITESCALE.STANDARDUSER_DAVROOT": "https://localhost:9200/dav/spaces/" + space + "/",
            "OCS_EPHEMERAL_READONLY_USERNAME": "readonly", "OCS_EPHEMERAL_READONLY_PASSWORD": reader_password})


def cleanup():
    failures = []
    for kind, names in (("container", CONTAINERS), ("volume", VOLUMES)):
        for name in names:
            result = subprocess.run(("docker", kind, "inspect", name), capture_output=True)
            if result.returncode != 0:
                continue
            value = json.loads(result.stdout)[0]
            labels = value.get("Config", {}).get("Labels", {}) if kind == "container" else value.get("Labels", {})
            if (labels or {}).get(OWNER_LABEL) != OWNER:
                failures.append("Refused to remove unowned " + kind + ": " + name)
                continue
            args = ("docker", "rm", "-f", name) if kind == "container" else ("docker", "volume", "rm", name)
            if subprocess.run(args, stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode != 0:
                failures.append("Failed to remove owned " + kind + ": " + name)
    for kind, names in (("container", CONTAINERS), ("volume", VOLUMES)):
        for name in names:
            if subprocess.run(("docker", kind, "inspect", name), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0:
                failures.append("Owned " + kind + " cleanup could not be verified: " + name)
    if failures:
        raise RuntimeError("; ".join(failures))
    tls = os.environ.get("OCS_EPHEMERAL_TLS_DIRECTORY")
    if tls:
        import pathlib
        import shutil
        directory = pathlib.Path(tls).resolve()
        owned_root = pathlib.Path(os.environ["RUNNER_TEMP"]).resolve()
        if directory.parent != owned_root or not directory.name.startswith("ocs-version-tls-"):
            raise RuntimeError("TLS cleanup target is outside the owned runner scope.")
        shutil.rmtree(directory)
        if directory.exists():
            raise RuntimeError("Temporary TLS material is still present.")
        run("sudo", "rm", "--", "/usr/local/share/ca-certificates/ocs-version-acceptance.crt", quiet=True)
        run("sudo", "update-ca-certificates", quiet=True)
    print("Disposable containers and volumes are absent.")


if __name__ == "__main__":
    if sys.argv[1:] == ["setup"]:
        setup()
    elif sys.argv[1:] == ["disable-versioning"]:
        run("docker", "exec", "-u", "www-data", CONTAINERS[0], "php", "occ", "app:disable", "files_versions", quiet=True)
        # occ updates persistent app configuration, but existing PHP workers can retain
        # cached enabled-app/capability state. Restart only this runner-owned container.
        run("docker", "restart", "--time", "10", CONTAINERS[0], quiet=True)
        data = ready("http://127.0.0.1:8080/ocs/v2.php/cloud/capabilities?format=json",
                     os.environ["TEST_NEXTCLOUD.STANDARDUSER_USERNAME"], os.environ["TEST_NEXTCLOUD.STANDARDUSER_PASSWORD"])
        if data["ocs"]["data"]["capabilities"].get("files", {}).get("versioning") is True:
            raise RuntimeError("Owned Nextcloud still advertises versioning after disabling the app and restarting.")
    elif sys.argv[1:] == ["cleanup"]:
        cleanup()
    else:
        raise SystemExit("Expected setup, disable-versioning or cleanup.")
