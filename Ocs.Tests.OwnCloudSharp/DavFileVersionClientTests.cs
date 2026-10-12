using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using CompuMaster.Ocs.Core;
using CompuMaster.Ocs.Exceptions;
using NUnit.Framework;
using WebDav;

namespace CompuMaster.Ocs.OwnCloudSharpTests
{
    [TestFixture]
    public class DavFileVersionClientTests
    {
        private static readonly Uri Root = new Uri("https://fixture.test:8443/prefix/remote.php/dav/");
        private static readonly Uri FileUri = new Uri(Root, "files/protocol%20id/sub/report%2B%20one.txt");

        [TestCase(1, 2), TestCase(2, 3), TestCase(3, 3)]
        public void OnlyNextcloudOmitsTheSyntheticCurrentEntry(int protocol, int expectedCount)
        {
            var wire = new Wire { Protocol = (DavFileVersionProtocol)protocol, IncludeCurrentVersion = true };
            using (var http = new HttpClient(wire))
            using (var dav = new WebDavClient(http))
            {
                var versions = new DavFileVersionClient(dav, Root, wire.Protocol, "protocol id").ListVersions(FileUri);
                Assert.That(versions.Count, Is.EqualTo(expectedCount));
                Assert.That(versions.Any(v => v.VersionId == "current-revision"), Is.EqualTo(protocol != 1));
                Assert.That(wire.Requests.All(r => r.Method == "PROPFIND"), Is.True);
            }
        }

        [Test]
        public void NextcloudWithOnlyTheCurrentEntryHasNoHistoricalVersions()
        {
            using (var http = new HttpClient(new Wire { IncludeCurrentVersion = true, Empty = true }))
            using (var dav = new WebDavClient(http))
                Assert.That(new DavFileVersionClient(dav, Root, DavFileVersionProtocol.Nextcloud, "protocol id").ListVersions(FileUri), Is.Empty);
        }

        [TestCase(1), TestCase(2), TestCase(3)]
        public async Task HistoryPreservesOpaqueIdentityMetadataAndHistoricalBytes(int protocol)
        {
            var wire = new Wire { Protocol = (DavFileVersionProtocol)protocol };
            using (var http = new HttpClient(wire))
            using (var dav = new WebDavClient(http))
            using (var output = new MemoryStream())
            {
                var client = new DavFileVersionClient(dav, Root, wire.Protocol, "protocol id");
                var versions = await client.ListVersionsAsync(FileUri);
                Assert.That(versions.Count, Is.EqualTo(2));
                var version = versions[0];
                Assert.That(version.FileId, Is.EqualTo("space!file-id"));
                Assert.That(version.VersionId, Is.EqualTo("opaque-revision"));
                Assert.That(version.LastModifiedUtc, Is.EqualTo(new DateTime(2020, 3, 30, 8, 33, 27, DateTimeKind.Utc)));
                Assert.That(version.LastModifiedUtc.Value.Kind, Is.EqualTo(DateTimeKind.Utc));
                Assert.That(version.ContentLength, Is.EqualTo(0));
                Assert.That(versions[1].ContentLength, Is.Null, "Unknown differs from an empty file.");
                Assert.That(version.AuthorId, Is.EqualTo("author+id"));
                Assert.That(version.AuthorDisplayName, Is.EqualTo("Author Name"));
                Assert.That(version.Label, Is.EqualTo("Release & review"));
                Assert.That(versions[1].Label, Is.Null, "Failed propstats must not become metadata.");
                await client.DownloadVersionAsync(version, output);
                Assert.That(output.ToArray(), Is.EqualTo(new byte[] { 4, 9, 2 }));
                Assert.That(output.CanWrite, Is.True);
                Assert.That(wire.ResponseStream.Disposed, Is.True);
                Assert.That(wire.Requests.Last().Method, Is.EqualTo("GET"));
                Assert.That(wire.Requests.Last().Path, Does.EndWith("/opaque-revision"));
            }
        }

        [TestCase(1, "MOVE"), TestCase(2, "COPY"), TestCase(3, "COPY")]
        public void SyncRestoreUsesProtocolAndGuardsDestinationIdentity(int protocol, string method)
        {
            var wire = new Wire { Protocol = (DavFileVersionProtocol)protocol };
            using (var http = new HttpClient(wire))
            using (var dav = new WebDavClient(http))
            {
                var client = new DavFileVersionClient(dav, Root, wire.Protocol, "protocol id");
                var version = client.ListVersions(FileUri)[0];
                client.RestoreVersion(version);
                var request = wire.Requests.Last();
                Assert.That(request.Method, Is.EqualTo(method));
                Assert.That(request.Destination, Is.EqualTo(protocol == 1 ? new Uri(Root, "versions/protocol%20id/restore/target").AbsoluteUri : FileUri.AbsoluteUri));
                Assert.That(request.Overwrite, Is.EqualTo("T"));
                if (protocol != 1) Assert.That(request.Condition, Is.EqualTo("<" + FileUri.AbsoluteUri + "> ([\"current-tag\"])"));
                wire.FileId = "replacement-id";
                Assert.Throws<InvalidOperationException>(() => client.RestoreVersion(version));
                Assert.That(wire.Requests.Count(r => r.Method == method), Is.EqualTo(1));
            }
        }

        [TestCase(1), TestCase(2), TestCase(3)]
        public void EmptyHistoryIsDistinctFromMissingFileOrUnavailableNamespace(int protocol)
        {
            var wire = new Wire { Protocol = (DavFileVersionProtocol)protocol, Empty = true };
            using (var http = new HttpClient(wire))
            using (var dav = new WebDavClient(http))
            {
                var client = new DavFileVersionClient(dav, Root, wire.Protocol, "protocol id");
                Assert.That(client.ListVersions(FileUri), Is.Empty);
                wire.ListStatus = 404;
                var ex = Assert.Throws<ResponseException>(() => client.ListVersions(FileUri));
                Assert.That(ex.HttpStatusCode, Is.EqualTo(HttpStatusCode.NotFound));
                wire.ListStatus = 207; wire.IdentityStatus = 404;
                Assert.Throws<ResponseException>(() => client.ListVersions(FileUri));
            }
        }

        [TestCase(401), TestCase(403), TestCase(405), TestCase(500)]
        public void ListingErrorsAreNeverReportedAsEmptyHistory(int status)
        {
            using (var http = new HttpClient(new Wire { ListStatus = status }))
            using (var dav = new WebDavClient(http))
            {
                var error = Assert.Throws<ResponseException>(() => new DavFileVersionClient(dav, Root, DavFileVersionProtocol.Nextcloud, "protocol id").ListVersions(FileUri));
                Assert.That((int)error.HttpStatusCode, Is.EqualTo(status));
            }
        }

        [TestCase(null), TestCase(false), TestCase(true)]
        public void RemovalRequiresExplicitCapabilityAndNeverTargetsCurrentFile(bool? advertised)
        {
            var wire = new Wire();
            using (var http = new HttpClient(wire))
            using (var dav = new WebDavClient(http))
            {
                var client = new DavFileVersionClient(dav, Root, DavFileVersionProtocol.Nextcloud, "protocol id", advertised);
                var version = client.ListVersions(FileUri)[0];
                Assert.That(client.CanRemoveVersions, Is.EqualTo(advertised));
                if (advertised == true)
                {
                    client.RemoveVersion(version);
                    Assert.That(wire.Requests.Last().Path, Is.EqualTo(wire.VersionRoot + "opaque-revision"));
                    wire.MutationStatus = 403;
                    Assert.Throws<ResponseException>(() => client.RemoveVersion(version));
                    Assert.That(wire.Requests.Count(r => r.Method == "DELETE"), Is.EqualTo(2));
                }
                else
                {
                    Assert.Throws<NotSupportedException>(() => client.RemoveVersion(version));
                    Assert.That(wire.Requests.Any(r => r.Method == "DELETE"), Is.False);
                }
            }
        }

        [TestCase(2), TestCase(3)]
        public void OwnCloudRemovalIsUnsupportedEvenWithNextcloudFlag(int protocol)
        {
            using (var http = new HttpClient(new Wire { Protocol = (DavFileVersionProtocol)protocol }))
            using (var dav = new WebDavClient(http))
            {
                var client = new DavFileVersionClient(dav, Root, (DavFileVersionProtocol)protocol, versionDeletionAdvertised: true);
                Assert.That(client.CanRemoveVersions, Is.False);
                Assert.Throws<NotSupportedException>(() => client.RemoveVersion(client.ListVersions(FileUri)[0]));
            }
        }

        [TestCase("https://other.test/version"), TestCase("/prefix/remote.php/dav/meta/another/v/revision"), TestCase("nested/revision"), TestCase("%2Fescaped")]
        public void UntrustedVersionHrefsAreRejectedBeforeAnyContentOrMutationRequest(string href)
        {
            var wire = new Wire { BadHref = href };
            using (var http = new HttpClient(wire))
            using (var dav = new WebDavClient(http))
            {
                Assert.Catch(() => new DavFileVersionClient(dav, Root, DavFileVersionProtocol.Nextcloud, "protocol id").ListVersions(FileUri));
                Assert.That(wire.Requests.All(r => r.Method == "PROPFIND"), Is.True);
            }
        }

        [Test]
        public void ForeignAccountSnapshotsAndMissingIdentityAreRejected()
        {
            var wire = new Wire();
            using (var http = new HttpClient(wire))
            using (var dav = new WebDavClient(http))
            using (var output = new MemoryStream())
            {
                var client = new DavFileVersionClient(dav, Root, DavFileVersionProtocol.Nextcloud, "protocol id");
                var version = client.ListVersions(FileUri)[0];
                var other = new DavFileVersionClient(dav, Root, DavFileVersionProtocol.Nextcloud, "another-id");
                Assert.Throws<ArgumentException>(() => other.DownloadVersion(version, output));
                Assert.Throws<ArgumentException>(() => other.RestoreVersion(version));
                wire.FileId = null;
                Assert.Throws<InvalidDataException>(() => client.ListVersions(FileUri));
                Assert.That(wire.Requests.All(r => r.Method == "PROPFIND"), Is.True);
            }
        }

        [Test]
        public async Task ActiveDownloadCancellationAndWriteFailureDisposeResponseButPreserveDestination()
        {
            var wire = new Wire();
            using (var http = new HttpClient(wire))
            using (var dav = new WebDavClient(http))
            using (var cancellation = new CancellationTokenSource())
            using (var output = new CancelDestination(cancellation))
            {
                var client = new DavFileVersionClient(dav, Root, DavFileVersionProtocol.Nextcloud, "protocol id");
                var version = (await client.ListVersionsAsync(FileUri))[0];
                wire.LargeContent = true;
                Assert.CatchAsync<OperationCanceledException>(async () => await client.DownloadVersionAsync(version, output, cancellation.Token));
                Assert.That(wire.ResponseStream.Disposed, Is.True);
                Assert.That(output.CanWrite, Is.True);
                wire.LargeContent = false;
                using (var badOutput = new FailDestination())
                {
                    Assert.ThrowsAsync<IOException>(async () => await client.DownloadVersionAsync(version, badOutput));
                    Assert.That(wire.ResponseStream.Disposed, Is.True);
                    Assert.That(badOutput.CanWrite, Is.True);
                }
            }
        }

        [Test]
        public void StaleSelectedFileIsRejectedEvenWhenHistoryIsEmpty()
        {
            var wire = new Wire { Empty = true };
            using (var http = new HttpClient(wire))
            using (var dav = new WebDavClient(http))
            {
                var client = new DavFileVersionClient(dav, Root, DavFileVersionProtocol.Nextcloud, "protocol id");
                Assert.Throws<InvalidOperationException>(() => client.ListVersions(FileUri, "old-file-id"));
                Assert.That(wire.Requests.Count, Is.EqualTo(1));
            }
        }

        [TestCase(401), TestCase(403), TestCase(404), TestCase(500)]
        public void FailedHistoricalGetDisposesResponseAndPreservesOriginalStatus(int status)
        {
            var wire = new Wire { DownloadStatus = status };
            using (var http = new HttpClient(wire))
            using (var dav = new WebDavClient(http))
            using (var output = new MemoryStream())
            {
                var client = new DavFileVersionClient(dav, Root, DavFileVersionProtocol.Nextcloud, "protocol id");
                var error = Assert.Throws<ResponseException>(() => client.DownloadVersion(client.ListVersions(FileUri)[0], output));
                Assert.That((int)error.HttpStatusCode, Is.EqualTo(status));
                Assert.That(wire.ResponseStream.Disposed, Is.True);
                Assert.That(output.Length, Is.Zero);
                Assert.That(output.CanWrite, Is.True);
            }
        }

        [TestCase(1, 403), TestCase(2, 412), TestCase(3, 500)]
        public void RestoreFailuresRetainStatusAndAreNeverReplayed(int protocol, int status)
        {
            var wire = new Wire { Protocol = (DavFileVersionProtocol)protocol, MutationStatus = status };
            using (var http = new HttpClient(wire))
            using (var dav = new WebDavClient(http))
            {
                var client = new DavFileVersionClient(dav, Root, wire.Protocol, "protocol id");
                var error = Assert.Throws<ResponseException>(() => client.RestoreVersion(client.ListVersions(FileUri)[0]));
                Assert.That((int)error.HttpStatusCode, Is.EqualTo(status));
                Assert.That(wire.Requests.Count(r => r.Method == "MOVE" || r.Method == "COPY"), Is.EqualTo(1));
            }
        }

        [Test]
        public void PreCancelledCallsDoNotSendRequests()
        {
            var wire = new Wire();
            using (var http = new HttpClient(wire))
            using (var dav = new WebDavClient(http))
            using (var cancellation = new CancellationTokenSource())
            {
                cancellation.Cancel();
                Assert.CatchAsync<OperationCanceledException>(async () => await new DavFileVersionClient(dav, Root, DavFileVersionProtocol.Nextcloud, "protocol id").ListVersionsAsync(FileUri, cancellation.Token));
                Assert.That(wire.Requests, Is.Empty);
            }
        }

        [Test]
        public void InfiniteScaleReservedResourceIdSpellingsReferToTheSameFileAndVersions()
        {
            var current = new Uri(Root, "spaces/storage%24space/report.txt");
            var wire = new Wire { Protocol = DavFileVersionProtocol.OwnCloudInfiniteScale,
                CurrentFile = current, FileId = "storage$space!file", LiteralReservedHrefs = true };
            using (var http = new HttpClient(wire))
            using (var dav = new WebDavClient(http))
            {
                var client = new DavFileVersionClient(dav, Root, wire.Protocol);
                var version = client.ListVersions(current)[0];
                Assert.That(version.FileId, Is.EqualTo(wire.FileId));
                client.RestoreVersion(version);
                Assert.That(wire.Requests.Last().Destination, Is.EqualTo(current.AbsoluteUri));
            }
        }

        internal sealed class Request
        {
            internal string Method, Path, Destination, Condition, Overwrite;
        }
        internal sealed class Wire : HttpMessageHandler
        {
            internal DavFileVersionProtocol Protocol = DavFileVersionProtocol.Nextcloud;
            internal string FileId = "space!file-id", BadHref;
            internal int IdentityStatus = 207, ListStatus = 207, MutationStatus = 204, DownloadStatus = 200;
            internal bool Empty, LargeContent;
            internal readonly List<Request> Requests = new List<Request>();
            internal TrackedStream ResponseStream;
            internal bool IncludeCurrentVersion;
            internal Uri CurrentFile = FileUri;
            internal bool LiteralReservedHrefs;
            internal string VersionRoot => Root.AbsolutePath + (Protocol == DavFileVersionProtocol.Nextcloud ? "versions/protocol%20id/versions/" + Uri.EscapeDataString(FileId ?? "") + "/" : "meta/" + Uri.EscapeDataString(FileId ?? "") + "/v/");
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                Requests.Add(new Request { Method = request.Method.Method, Path = request.RequestUri.AbsolutePath,
                    Destination = Header(request, "Destination"), Condition = Header(request, "If"), Overwrite = Header(request, "Overwrite") });
                if (request.Method.Method == "PROPFIND")
                {
                    bool file = request.RequestUri == CurrentFile;
                    int status = file ? IdentityStatus : ListStatus;
                    return Task.FromResult(new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(status == 207 ? Xml(file) : "DAV failure") });
                }
                if (request.Method == HttpMethod.Get)
                {
                    ResponseStream = new TrackedStream(LargeContent ? new byte[200000] : new byte[] { 4, 9, 2 });
                    return Task.FromResult(new HttpResponseMessage((HttpStatusCode)DownloadStatus) { Content = new StreamContent(ResponseStream) });
                }
                return Task.FromResult(new HttpResponseMessage((HttpStatusCode)MutationStatus));
            }
            private static string Header(HttpRequestMessage r, string name) => r.Headers.TryGetValues(name, out var values) ? values.Single() : null;
            private string Xml(bool file)
            {
                XNamespace d = "DAV:", oc = "http://owncloud.org/ns", nc = "http://nextcloud.org/ns";
                Func<string, XElement, XElement> response = (href, prop) => new XElement(d + "response", new XElement(d + "href", LiteralReservedHrefs ? href.Replace("%24", "$").Replace("%21", "!") : href),
                    new XElement(d + "propstat", new XElement(d + "prop", prop.Elements()), new XElement(d + "status", "HTTP/1.1 200 OK")));
                var root = new XElement(d + "multistatus");
                if (file)
                    root.Add(response(CurrentFile.AbsolutePath, new XElement("properties", new XElement(oc + "fileid", FileId), new XElement(d + "getetag", "\"current-tag\""), new XElement(d + "resourcetype"),
                        IncludeCurrentVersion ? new XElement(d + "getlastmodified", "Mon, 30 Mar 2020 10:33:27 GMT") : null)));
                else
                {
                    root.Add(response(VersionRoot, new XElement("properties", new XElement(d + "resourcetype", new XElement(d + "collection")))));
                    if (IncludeCurrentVersion)
                        root.Add(response(VersionRoot + "current-revision", new XElement("properties", new XElement(d + "resourcetype"), new XElement(d + "getlastmodified", "Mon, 30 Mar 2020 10:33:27 GMT"))));
                    if (!Empty)
                    {
                        root.Add(response(BadHref == null ? VersionRoot + "opaque-revision" : (BadHref.StartsWith("http") || BadHref.StartsWith("/") ? BadHref : VersionRoot + BadHref), new XElement("properties",
                            new XElement(d + "resourcetype"), new XElement(d + "getlastmodified", "Mon, 30 Mar 2020 08:33:27 GMT"), new XElement(d + "getcontentlength", 0),
                            new XElement(d + "getcontenttype", "text/plain"), new XElement(nc + "version-author", "author+id"), new XElement(oc + "meta-version-edited-by", "author+id"),
                            new XElement(oc + "meta-version-edited-by-name", "Author Name"), new XElement(nc + "version-label", "Release & review"), new XElement(oc + "meta-version-tag", "Release & review"))));
                        var unknown = response(VersionRoot + "older-revision", new XElement("properties", new XElement(d + "resourcetype")));
                        unknown.Add(new XElement(d + "propstat", new XElement(d + "prop", new XElement(nc + "version-label", "must ignore"), new XElement(oc + "meta-version-tag", "must ignore")), new XElement(d + "status", "HTTP/1.1 404 Not Found")));
                        root.Add(unknown);
                    }
                }
                return root.ToString();
            }
        }
        internal sealed class TrackedStream : MemoryStream
        {
            internal bool Disposed;
            internal TrackedStream(byte[] data) : base(data) { }
            protected override void Dispose(bool disposing) { Disposed = true; base.Dispose(disposing); }
        }
        private abstract class DestinationStream : Stream
        {
            public override bool CanRead => false;
            public override bool CanSeek => false;
            public override bool CanWrite => true;
            public override long Length => throw new NotSupportedException();
            public override long Position { get => throw new NotSupportedException(); set => throw new NotSupportedException(); }
            public override void Flush() { }
            public override int Read(byte[] b, int o, int c) => throw new NotSupportedException();
            public override long Seek(long o, SeekOrigin origin) => throw new NotSupportedException();
            public override void SetLength(long length) => throw new NotSupportedException();
            public override void Write(byte[] b, int o, int c) => throw new IOException("Unexpected synchronous write.");
        }
        private sealed class CancelDestination : DestinationStream
        {
            private readonly CancellationTokenSource cancellation;
            internal CancelDestination(CancellationTokenSource cancellation) { this.cancellation = cancellation; }
            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token)
            {
                cancellation.Cancel();
                token.ThrowIfCancellationRequested();
                return base.WriteAsync(buffer, offset, count, token);
            }
        }
        private sealed class FailDestination : DestinationStream
        {
            public override Task WriteAsync(byte[] buffer, int offset, int count, CancellationToken token) => throw new IOException("Owned destination failed.");
        }
    }
}
