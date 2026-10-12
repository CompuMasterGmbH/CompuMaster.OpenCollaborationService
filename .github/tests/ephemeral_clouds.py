"""Provision only disposable, loopback-bound CI clouds; no shared-server credentials."""
import base64
import json
import http.client
import os
import secrets
import subprocess
import sys
import time
import urllib.error
import urllib.request

CONTAINERS = ("ocs-versions-nextcloud", "ocs-versions-is")
VOLUMES = ("ocs-versions-nextcloud-data", "ocs-versions-is-config", "ocs-versions-is-data")


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
    for _ in range(90):
        try:
            data = get(url, user, password)
            if data.get("ocs", {}).get("meta", {}).get("statuscode") in (100, 200):
                return data
        except (urllib.error.URLError, ValueError, TimeoutError, ConnectionError, http.client.HTTPException):
            pass
        time.sleep(2)
    raise RuntimeError("Disposable cloud did not become ready; no shared server was contacted.")


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
        run("docker", "volume", "create", name, quiet=True)
    password = "Test-" + secrets.token_hex(24)
    reader_password = "Reader-" + secrets.token_hex(24)
    print("::add-mask::" + password, flush=True)
    print("::add-mask::" + reader_password, flush=True)
    os.environ.update(NEXTCLOUD_ADMIN_USER="owner", NEXTCLOUD_ADMIN_PASSWORD=password,
                      SQLITE_DATABASE="cloud", NEXTCLOUD_TRUSTED_DOMAINS="127.0.0.1 localhost",
                      IDM_ADMIN_PASSWORD=password, OCIS_URL="http://127.0.0.1:9200",
                      OCIS_INSECURE="true", PROXY_TLS="false", PROXY_ENABLE_BASIC_AUTH="true",
                      PROXY_HTTP_ADDR="0.0.0.0:9200", OCIS_LOG_LEVEL="error", OC_PASS=reader_password)
    nc_env = sum((["-e", name] for name in ("NEXTCLOUD_ADMIN_USER", "NEXTCLOUD_ADMIN_PASSWORD", "SQLITE_DATABASE", "NEXTCLOUD_TRUSTED_DOMAINS")), [])
    run("docker", "run", "-d", "--name", CONTAINERS[0], "-p", "127.0.0.1:8080:80",
        "-v", VOLUMES[0] + ":/var/www/html", *nc_env, "nextcloud:34.0.3-apache", quiet=True)
    is_env = sum((["-e", name] for name in ("IDM_ADMIN_PASSWORD", "OCIS_URL", "OCIS_INSECURE", "PROXY_TLS", "PROXY_ENABLE_BASIC_AUTH", "PROXY_HTTP_ADDR", "OCIS_LOG_LEVEL")), [])
    mounts = ("-v", VOLUMES[1] + ":/etc/ocis", "-v", VOLUMES[2] + ":/var/lib/ocis")
    run("docker", "run", "--rm", *mounts, *is_env, "owncloud/ocis:8.2.1", "init", quiet=True)
    run("docker", "run", "-d", "--name", CONTAINERS[1], "-p", "127.0.0.1:9200:9200",
        *mounts, *is_env, "owncloud/ocis:8.2.1", "server", quiet=True)
    nc = ready("http://127.0.0.1:8080/ocs/v2.php/cloud/capabilities?format=json", "owner", password)
    ocis = ready("http://127.0.0.1:9200/ocs/v2.php/cloud/capabilities?format=json", "admin", password)
    run("docker", "exec", "-u", "www-data", "-e", "OC_PASS", CONTAINERS[0], "php", "occ", "user:add", "--password-from-env", "readonly", quiet=True)
    drives = get("http://127.0.0.1:9200/graph/v1.0/me/drives", "admin", password)["value"]
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
            "TEST_INFINITESCALE.STANDARDUSER_SERVERURL": "http://127.0.0.1:9200/",
            "TEST_INFINITESCALE.STANDARDUSER_USERNAME": "admin", "TEST_INFINITESCALE.STANDARDUSER_PASSWORD": password,
            "TEST_INFINITESCALE.STANDARDUSER_DAVROOT": "http://127.0.0.1:9200/dav/spaces/" + space + "/",
            "OCS_EPHEMERAL_READONLY_USERNAME": "readonly", "OCS_EPHEMERAL_READONLY_PASSWORD": reader_password})


def cleanup():
    for name in CONTAINERS:
        run("docker", "rm", "-f", name, quiet=True) if subprocess.run(("docker", "container", "inspect", name), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0 else None
    for name in VOLUMES:
        run("docker", "volume", "rm", name, quiet=True) if subprocess.run(("docker", "volume", "inspect", name), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0 else None
    for kind, names in (("container", CONTAINERS), ("volume", VOLUMES)):
        for name in names:
            if subprocess.run(("docker", kind, "inspect", name), stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL).returncode == 0:
                raise RuntimeError("Owned " + kind + " cleanup could not be verified.")
    print("Disposable containers and volumes are absent.")


if __name__ == "__main__":
    if sys.argv[1:] == ["setup"]:
        setup()
    elif sys.argv[1:] == ["disable-versioning"]:
        run("docker", "exec", "-u", "www-data", CONTAINERS[0], "php", "occ", "app:disable", "files_versions", quiet=True)
    elif sys.argv[1:] == ["cleanup"]:
        cleanup()
    else:
        raise SystemExit("Expected setup, disable-versioning or cleanup.")
