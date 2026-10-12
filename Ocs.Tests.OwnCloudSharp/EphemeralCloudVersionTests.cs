using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using CompuMaster.Ocs.Core;
using CompuMaster.Ocs.Exceptions;
using NUnit.Framework;
using WebDav;

namespace CompuMaster.Ocs.OwnCloudSharpTests
{
    // Separate from shared-server fixtures: only the container workflow supplies these settings.
    [TestFixture, NonParallelizable, Category("EphemeralCloudVersions")]
    public sealed class EphemeralCloudVersionTests
    {
        [Test]
        public Task InfiniteScaleSpaceRoundTrip()
        {
            RequireEphemeral();
            return DavFileVersionLiveTests.RoundTrip("INFINITESCALE", DavFileVersionProtocol.OwnCloudInfiniteScale);
        }

        [Test]
        public Task NextcloudLegacyRoundTrip()
        {
            RequireEphemeral();
            return DavFileVersionLiveTests.RoundTrip("NEXTCLOUD", DavFileVersionProtocol.Nextcloud, legacy: true);
        }

        [Test]
        public async Task NextcloudReadOnlyShareKeepsHistoricalContentButRejectsRestore()
        {
            RequireEphemeral();
            var instance = Instance("NEXTCLOUD");
            string owner = Setting("TEST_NEXTCLOUD.STANDARDUSER_USERNAME");
            string recipient = Setting("OCS_EPHEMERAL_READONLY_USERNAME");
            using (var timeout = new CancellationTokenSource(TimeSpan.FromMinutes(3)))
            using (var http = Authenticated(owner, Setting("TEST_NEXTCLOUD.STANDARDUSER_PASSWORD")))
            using (var readerHttp = Authenticated(recipient, Setting("OCS_EPHEMERAL_READONLY_PASSWORD")))
            using (var dav = new WebDavClient(http))
            using (var readerDav = new WebDavClient(readerHttp))
            {
                var root = new Uri(instance, "remote.php/dav/files/" + Uri.EscapeDataString(owner) + "/ZZZ_UnitTests_OcsReadOnly/");
                var file = new Uri(root, "Historical.txt");
                string shareId = null;
                Exception failure = null;
                try
                {
                    Assert.That((await dav.Propfind(root)).StatusCode, Is.EqualTo(404), "Fresh container must contain no owned leftovers.");
                    Assert.That((await dav.Mkcol(root)).IsSuccessful, Is.True);
                    byte[] original = Encoding.UTF8.GetBytes("read-only historical original");
                    using (var stream = new MemoryStream(original)) Assert.That((await dav.PutFile(file, stream)).IsSuccessful, Is.True);
                    await Task.Delay(2100, timeout.Token);
                    using (var stream = new MemoryStream(Encoding.UTF8.GetBytes("replacement"))) Assert.That((await dav.PutFile(file, stream)).IsSuccessful, Is.True);
                    var shared = await Ocs(http, new Uri(instance, "ocs/v2.php/apps/files_sharing/api/v1/shares"), HttpMethod.Post,
                        new FormUrlEncodedContent(new[] { Pair("path", "/ZZZ_UnitTests_OcsReadOnly"), Pair("shareType", "0"), Pair("shareWith", recipient), Pair("permissions", "1") }), timeout.Token);
                    shareId = (string)shared.Element("id");
                    Assert.That(shareId, Is.Not.Null.And.Not.Empty);
                    var incoming = await Ocs(readerHttp, new Uri(instance, "ocs/v2.php/apps/files_sharing/api/v1/shares?shared_with_me=true"), HttpMethod.Get, null, timeout.Token);
                    var share = incoming.Elements().Single(e => (string)e.Element("id") == shareId);
                    string target = (string)share.Element("file_target");
                    Assert.That(target, Is.Not.Null.And.Not.Empty);
                    var readerRoot = new Uri(instance, "remote.php/dav/files/" + Uri.EscapeDataString(recipient) + "/");
                    var readerFile = new Uri(readerRoot, string.Join("/", target.Trim('/').Split('/').Select(Uri.EscapeDataString)) + "/Historical.txt");
                    var client = new DavFileVersionClient(readerDav, new Uri(instance, "remote.php/dav/"), DavFileVersionProtocol.Nextcloud, recipient);
                    var versions = await client.ListVersionsAsync(readerFile, timeout.Token);
                    Assert.That(versions, Is.Not.Empty);
                    using (var bytes = new MemoryStream())
                    {
                        await client.DownloadVersionAsync(versions[0], bytes, timeout.Token);
                        Assert.That(bytes.ToArray(), Is.EqualTo(original));
                    }
                    var denied = Assert.ThrowsAsync<ResponseException>(async () => await client.RestoreVersionAsync(versions[0], timeout.Token));
                    Assert.That(denied.HttpStatusCode, Is.EqualTo(HttpStatusCode.Forbidden));
                    using (var current = await dav.GetRawFile(file))
                    using (var bytes = new MemoryStream())
                    {
                        await current.Stream.CopyToAsync(bytes, 81920, timeout.Token);
                        Assert.That(Encoding.UTF8.GetString(bytes.ToArray()), Is.EqualTo("replacement"), "Denied restore must leave the owner file unchanged.");
                    }
                }
                catch (Exception ex) { failure = ex; throw; }
                finally
                {
                    try
                    {
                        using (var cleanup = new CancellationTokenSource(TimeSpan.FromSeconds(30)))
                        {
                            if (shareId != null) await Ocs(http, new Uri(instance, "ocs/v2.php/apps/files_sharing/api/v1/shares/" + Uri.EscapeDataString(shareId)), HttpMethod.Delete, null, cleanup.Token);
                            var present = await dav.Propfind(file, new PropfindParameters { CancellationToken = cleanup.Token, ApplyTo = ApplyTo.Propfind.ResourceOnly });
                            if (present.StatusCode != 404) Assert.That((await dav.Delete(file, new DeleteParameters { CancellationToken = cleanup.Token })).IsSuccessful, Is.True);
                            Assert.That((await dav.Propfind(file)).StatusCode, Is.EqualTo(404));
                            var collection = await dav.Propfind(root);
                            if (collection.StatusCode != 404)
                            {
                                Assert.That(collection.IsSuccessful, Is.True);
                                Assert.That(collection.Resources.Count, Is.EqualTo(1), "Only an empty owned scope may be deleted.");
                                Assert.That((await dav.Delete(root)).IsSuccessful, Is.True);
                            }
                            Assert.That((await dav.Propfind(root)).StatusCode, Is.EqualTo(404));
                        }
                    }
                    catch (Exception cleanup) { if (failure != null) throw new AggregateException(failure, cleanup); throw; }
                }
            }
        }

        [Test]
        public async Task DisabledNextcloudVersioningIsNotEmptyHistory()
        {
            RequireEphemeral();
            var instance = Instance("NEXTCLOUD");
            string owner = Setting("TEST_NEXTCLOUD.STANDARDUSER_USERNAME");
            using (var http = Authenticated(owner, Setting("TEST_NEXTCLOUD.STANDARDUSER_PASSWORD")))
            using (var dav = new WebDavClient(http))
            {
                var capabilities = await Ocs(http, new Uri(instance, "ocs/v2.php/cloud/capabilities"), HttpMethod.Get, null, CancellationToken.None);
                var enabled = capabilities.Element("capabilities")?.Element("files")?.Element("versioning");
                Assert.That(enabled == null || enabled.Value == "false" || enabled.Value == "0", Is.True);
                var root = new Uri(instance, "remote.php/dav/files/" + Uri.EscapeDataString(owner) + "/");
                var file = new Uri(root, "DisabledVersioning.txt");
                Exception failure = null;
                try
                {
                    Assert.That((await dav.Propfind(file)).StatusCode, Is.EqualTo(404));
                    using (var bytes = new MemoryStream(new byte[] { 1 })) Assert.That((await dav.PutFile(file, bytes)).IsSuccessful, Is.True);
                    var client = new DavFileVersionClient(dav, new Uri(instance, "remote.php/dav/"), DavFileVersionProtocol.Nextcloud, owner);
                    var unavailable = Assert.ThrowsAsync<ResponseException>(async () => await client.ListVersionsAsync(file));
                    Assert.That(unavailable.HttpStatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                }
                catch (Exception ex) { failure = ex; throw; }
                finally
                {
                    try
                    {
                        var existing = await dav.Propfind(file);
                        if (existing.StatusCode != 404) Assert.That((await dav.Delete(file)).IsSuccessful, Is.True);
                        Assert.That((await dav.Propfind(file)).StatusCode, Is.EqualTo(404));
                    }
                    catch (Exception cleanup) { if (failure != null) throw new AggregateException(failure, cleanup); throw; }
                }
            }
        }

        private static void RequireEphemeral()
        {
            if (Environment.GetEnvironmentVariable("OCS_EPHEMERAL_ACCEPTANCE") != "1") Assert.Ignore("Requires the independent ephemeral container workflow.");
        }
        private static string Setting(string name) => Environment.GetEnvironmentVariable(name) ?? throw new InvalidOperationException("Missing ephemeral setting: " + name);
        private static Uri Instance(string family)
        {
            var uri = new Uri(Setting("TEST_" + family + ".STANDARDUSER_SERVERURL"));
            Assert.That(uri.IsLoopback, Is.True, "This fixture is confined to owned loopback containers.");
            return uri;
        }
        private static HttpClient Authenticated(string user, string password)
        {
            var http = new HttpClient(new HttpClientHandler { AllowAutoRedirect = false }) { Timeout = TimeSpan.FromSeconds(30) };
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(user + ":" + password)));
            http.DefaultRequestHeaders.Add("OCS-APIRequest", "true");
            return http;
        }
        private static System.Collections.Generic.KeyValuePair<string, string> Pair(string key, string value) => new System.Collections.Generic.KeyValuePair<string, string>(key, value);
        private static async Task<XElement> Ocs(HttpClient http, Uri uri, HttpMethod method, HttpContent content, CancellationToken token)
        {
            var address = new Uri(uri + (uri.Query.Length == 0 ? "?" : "&") + "format=xml");
            using (var request = new HttpRequestMessage(method, address) { Content = content })
            using (var response = await http.SendAsync(request, token))
            {
                Assert.That(response.IsSuccessStatusCode, Is.True, "Ephemeral OCS operation failed: " + (int)response.StatusCode);
                var envelope = XDocument.Parse(await response.Content.ReadAsStringAsync()).Root;
                Assert.That((int?)envelope.Element("meta")?.Element("statuscode"), Is.EqualTo(100).Or.EqualTo(200));
                return envelope.Element("data");
            }
        }
    }
}
