using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using CompuMaster.Ocs.Core;
using System.Xml.Linq;
using NUnit.Framework;
using WebDav;

namespace CompuMaster.Ocs.OwnCloudSharpTests
{
    // Never enables itself merely because credentials happen to be available.
    [TestFixture, NonParallelizable, Category("RemoteFileVersions")]
    public sealed class DavFileVersionLiveTests
    {
        [TestCase("NEXTCLOUD", DavFileVersionProtocol.Nextcloud)]
        [TestCase("OWNCLOUD", DavFileVersionProtocol.OwnCloudServer)]
        public async Task HistoricalContentRestorationAndAdvertisedRemoval(string family, DavFileVersionProtocol protocol)
        {
            await RoundTrip(family, protocol);
        }

        internal static async Task RoundTrip(string family, DavFileVersionProtocol protocol, bool legacy = false)
        {
            if (Environment.GetEnvironmentVariable("OCS_VERSION_TESTS_EXCLUSIVE") != "1")
                Assert.Ignore("Requires a coordinated exclusive cloud-server window and explicit opt-in.");
            string prefix = "TEST_" + family + ".STANDARDUSER_";
            string server = Required(prefix + "SERVERURL");
            string login = Required(prefix + "USERNAME");
            string password = Required(prefix + "PASSWORD");
            var instance = new Uri(server.TrimEnd('/') + "/");
            Assert.That(instance.Scheme == "https" || (Environment.GetEnvironmentVariable("OCS_EPHEMERAL_ACCEPTANCE") == "1" && instance.IsLoopback && instance.Scheme == "http"), Is.True, "Acceptance credentials require HTTPS except for explicitly owned loopback-only CI containers.");
            Assert.That(instance.AbsolutePath.Contains("remote.php"), Is.False, "SERVERURL must be the instance root.");
            using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3)))
            using (var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }))
            using (var transport = new WebDavClient(http))
            {
                http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(login + ":" + password)));
                http.DefaultRequestHeaders.Add("OCS-APIRequest", "true");
                var user = await OcsData(http, new Uri(instance, "ocs/v2.php/cloud/user?format=xml"), timeout.Token);
                var capabilities = await OcsData(http, new Uri(instance, "ocs/v2.php/cloud/capabilities?format=xml"), timeout.Token);
                string userId = (string)user.Element("id");
                Assert.That(userId, Is.Not.Null.And.Not.Empty, "Authenticated protocol user ID is required.");
                var fileCapabilities = capabilities.Element("capabilities")?.Element("files");
                Assert.That(Capability(fileCapabilities?.Element("versioning")), Is.True, "The test server must advertise versioning.");
                bool? removal = Capability(fileCapabilities?.Element("version_deletion"));
                TestContext.Out.WriteLine(family + " server version: " + (string)capabilities.Element("version")?.Element("string") + "; advertised deletion: " + removal);
                var suppliedFiles = protocol == DavFileVersionProtocol.OwnCloudInfiniteScale
                    ? new Uri(Required(prefix + "DAVROOT"))
                    : new Uri(instance, legacy ? "remote.php/webdav/" : "remote.php/dav/files/" + Uri.EscapeDataString(userId) + "/");
                Assert.That(suppliedFiles.GetLeftPart(UriPartial.Authority), Is.EqualTo(instance.GetLeftPart(UriPartial.Authority)), "Configured current-file endpoint must remain on this authenticated instance.");
                var endpoint = DavFileVersionEndpoint.Resolve(suppliedFiles, protocol, userId);
                var dav = endpoint.DavRoot;
                var files = endpoint.FilesRoot;
                // This fixture owns this exact scope; configured permanent DMS roots remain untouched.
                var root = new Uri(files, "ZZZ_UnitTests_OcsVersions/");
                var file = new Uri(root, "DavFileVersionLiveTests.RoundTrip.txt");
                var client = new DavFileVersionClient(transport, dav, protocol, userId, removal);
                Exception failure = null;
                try
                {
                    var existingRoot = await transport.Propfind(root, ResourceOnly(timeout.Token));
                    if (existingRoot.StatusCode == 404) Check(await transport.Mkcol(root, new MkColParameters { CancellationToken = timeout.Token }), "Create owned test root");
                    else Check(existingRoot, "Read owned test root");
                    var verifiedRoot = await transport.Propfind(root, ResourceOnly(timeout.Token));
                    Check(verifiedRoot, "Verify owned test root");
                    Assert.That(verifiedRoot.Resources.Count, Is.EqualTo(1));
                    Assert.That(verifiedRoot.Resources.Single().IsCollection, Is.True);
                    await RemoveAndVerify(transport, file, timeout.Token);
                    byte[] original = Encoding.UTF8.GetBytes("OCS file-version acceptance: original\r\n");
                    byte[] current = Encoding.UTF8.GetBytes("OCS file-version acceptance: replacement with different size\r\n");
                    await Put(transport, file, original, timeout.Token);
                    await Task.Delay(2100, timeout.Token);
                    await Put(transport, file, current, timeout.Token);
                    IReadOnlyList<DavFileVersion> versions = null;
                    for (int attempt = 0; attempt < 10; attempt++)
                    {
                        versions = await client.ListVersionsAsync(file, timeout.Token);
                        if (versions.Count > 0) break;
                        await Task.Delay(1000, timeout.Token);
                    }
                    Assert.That(versions, Is.Not.Empty, "Overwriting must create retained history on the configured test server.");
                    DavFileVersion selected = null;
                    foreach (var version in versions)
                    {
                        TestContext.Out.WriteLine(family + " owned fixture version: " + version.VersionId + "; length: " + version.ContentLength);
                        using (var content = new MemoryStream())
                        {
                            await client.DownloadVersionAsync(version, content, timeout.Token);
                            if (content.ToArray().SequenceEqual(original)) selected = version;
                        }
                    }
                    Assert.That(selected, Is.Not.Null, "History must contain the exact original bytes.");
                    Uri legacyFile = legacy ? new Uri(suppliedFiles, "ZZZ_UnitTests_OcsVersions/DavFileVersionLiveTests.RoundTrip.txt") : null;
                    if (legacyFile != null)
                    {
                        var alias = await transport.Propfind(legacyFile, new PropfindParameters { ApplyTo = ApplyTo.Propfind.ResourceOnly,
                            CustomProperties = new[] { XName.Get("fileid", "http://owncloud.org/ns") }, CancellationToken = timeout.Token });
                        Check(alias, "Verify selected legacy alias");
                        Assert.That(alias.Resources.Single().Properties.Single(p => p.Name == XName.Get("fileid", "http://owncloud.org/ns")).Value, Is.EqualTo(selected.FileId));
                    }
                    await client.RestoreVersionAsync(selected, timeout.Token);
                    using (var restored = await transport.GetRawFile(legacyFile ?? file, new GetFileParameters { CancellationToken = timeout.Token }))
                    using (var bytes = new MemoryStream())
                    {
                        Check(restored, "Download restored current file");
                        await restored.Stream.CopyToAsync(bytes, 81920, timeout.Token);
                        Assert.That(bytes.ToArray(), Is.EqualTo(original));
                    }
                    var refreshed = await client.ListVersionsAsync(file, timeout.Token, selected.FileId);
                    Assert.That(refreshed.All(v => v.FileId == selected.FileId), Is.True);
                    if (client.CanRemoveVersions == true)
                    {
                        Assert.That(refreshed, Is.Not.Empty, "Restoration should retain history for advertised removal verification.");
                        var toRemove = refreshed.First();
                        await client.RemoveVersionAsync(toRemove, timeout.Token);
                        var remaining = await client.ListVersionsAsync(file, timeout.Token, selected.FileId);
                        Assert.That(remaining.Any(v => v.VersionId == toRemove.VersionId), Is.False);
                        TestContext.Out.WriteLine(family + " individual version removal verified.");
                    }
                    else
                        Assert.Throws<NotSupportedException>(() => client.RemoveVersion(selected));
                }
                catch (Exception ex) { failure = ex; throw; }
                finally
                {
                    using (var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                    {
                        try
                        {
                            await RemoveAndVerify(transport, file, cleanup.Token);
                            await RemoveEmptyOwnedRoot(transport, root, cleanup.Token);
                            TestContext.Out.WriteLine(family + " owned file and scope absence verified.");
                        }
                        catch (Exception ex)
                        {
                            if (failure != null) throw new AggregateException("Acceptance failed and owned file cleanup also failed.", failure, ex);
                            throw;
                        }
                    }
                }
            }
        }

        private static string Required(string name)
        {
            string value = Environment.GetEnvironmentVariable(name);
            Assert.That(value, Is.Not.Null.And.Not.Empty, "Required acceptance setting is missing: " + name);
            return value;
        }

        private static bool? Capability(XElement element) => element == null ? (bool?)null : element.Value == "true" || element.Value == "1";

        private static async Task<XElement> OcsData(HttpClient http, Uri uri, CancellationToken token)
        {
            using (var response = await http.GetAsync(uri, token))
            {
                Assert.That(response.IsSuccessStatusCode, Is.True, "Authenticated OCS discovery failed: " + (int)response.StatusCode);
                var envelope = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root;
                Assert.That((int?)envelope?.Element("meta")?.Element("statuscode"), Is.EqualTo(200).Or.EqualTo(100));
                return envelope.Element("data");
            }
        }

        private static PropfindParameters ResourceOnly(CancellationToken token) => new PropfindParameters { ApplyTo = ApplyTo.Propfind.ResourceOnly, CancellationToken = token };
        private static void Check(WebDavResponse response, string operation) => Assert.That(response.IsSuccessful, Is.True, operation + " failed: " + response.StatusCode);
        private static async Task Put(IWebDavClient transport, Uri uri, byte[] bytes, CancellationToken token)
        {
            using (var stream = new MemoryStream(bytes))
                Check(await transport.PutFile(uri, stream, new PutFileParameters { ContentType = new MediaTypeHeaderValue("text/plain"), CancellationToken = token }), "Upload owned acceptance file");
        }
        private static async Task RemoveAndVerify(IWebDavClient transport, Uri file, CancellationToken token)
        {
            var present = await transport.Propfind(file, ResourceOnly(token));
            if (present.StatusCode != 404)
            {
                Check(present, "Locate exact owned acceptance file");
                Assert.That(present.Resources.Count, Is.EqualTo(1));
                Assert.That(present.Resources.Single().IsCollection, Is.False, "Never recursively removes a collection.");
                Check(await transport.Delete(file, new DeleteParameters { CancellationToken = token }), "Remove exact owned acceptance file");
            }
            var absent = await transport.Propfind(file, ResourceOnly(token));
            Assert.That(absent.StatusCode, Is.EqualTo(404), "Owned acceptance file must be absent.");
        }

        private static async Task RemoveEmptyOwnedRoot(IWebDavClient transport, Uri root, CancellationToken token)
        {
            var contents = await transport.Propfind(root, new PropfindParameters { ApplyTo = ApplyTo.Propfind.ResourceAndChildren, CancellationToken = token });
            if (contents.StatusCode == 404) return;
            Check(contents, "Verify exact owned scope is empty");
            Assert.That(contents.Resources.Count, Is.EqualTo(1), "Never removes unknown children in the owned scope.");
            Assert.That(contents.Resources.Single().IsCollection, Is.True);
            Check(await transport.Delete(root, new DeleteParameters { CancellationToken = token }), "Remove empty owned scope");
            var absent = await transport.Propfind(root, ResourceOnly(token));
            Assert.That(absent.StatusCode, Is.EqualTo(404), "Owned acceptance scope must be absent.");
        }
    }
}
