using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using CompuMaster.Ocs.Core;
using CompuMaster.Ocs.Exceptions;
using NUnit.Framework;
using WebDav;

namespace CompuMaster.Ocs.OwnCloudSharpTests
{
    [TestFixture]
    public class DavChunkUploadClientTests
    {
        private static readonly Uri Root = new Uri("https://fixture.test/remote.php/dav/uploads/protocol-id/");
        private static readonly Uri Destination = new Uri("https://fixture.test/remote.php/dav/files/protocol-id/Reports/payload.bin");

        [Test]
        public async Task OrderedChunksPreserveBytesAndCallerStreamAndAssembleOnlyAfterConfirmation()
        {
            var wire = new Wire();
            using (var http = new HttpClient(wire))
            using (var dav = new WebDavClient(http))
            using (var input = new MemoryStream(Enumerable.Range(0, 10 * 1024 * 1024 + 17).Select(i => (byte)(i % 251)).ToArray()))
            {
                var client = new DavChunkUploadClient(dav, Root);
                Assert.That(await client.IsSupportedAsync(), Is.True);
                var date = new DateTime(2026, 10, 7, 19, 7, 0, DateTimeKind.Utc);
                await client.UploadAsync(Destination, input, input.Length, date);
                Assert.That(input.CanRead, Is.True, "The caller retains ownership.");
                Assert.That(wire.Chunks.Select(c => c.Length), Is.EqualTo(new[] { 10 * 1024 * 1024, 17 }));
                Assert.That(wire.Chunks.SelectMany(c => c), Is.EqualTo(input.ToArray()));
                Assert.That(wire.Requests.Where(r => r.Method == "PUT").Select(r => new Uri(r.Uri).Segments.Last()), Is.EqualTo(new[] { "00001", "00002" }));
                Assert.That(wire.Requests.Where(r => r.Method == "MKCOL" || r.Method == "PUT" || r.Method == "MOVE").All(r => r.Destination == Destination.AbsoluteUri), Is.True);
                Assert.That(wire.Requests.Single(r => r.Method == "MOVE").Time, Is.EqualTo(new DateTimeOffset(date).ToUnixTimeSeconds().ToString()));
                Assert.That(wire.Requests.Last().Method, Is.EqualTo("PROPFIND"));
                Assert.That(wire.Requests.Last().Uri, Does.StartWith(Root.AbsoluteUri + "cm-ocs-"));
                Assert.That(wire.Requests.Count(r => r.Method == "DELETE"), Is.EqualTo(1));
                Assert.That(wire.SessionExists, Is.False);
            }
        }

        [TestCase(404, false), TestCase(405, false), TestCase(501, false)]
        public async Task UnsupportedNamespaceDoesNotCreateAnUpload(int status, bool supported)
        {
            var wire = new Wire { ProbeStatus = status };
            using (var http = new HttpClient(wire))
            using (var dav = new WebDavClient(http))
            {
                Assert.That(await new DavChunkUploadClient(dav, Root).IsSupportedAsync(), Is.EqualTo(supported));
                Assert.That(wire.Requests.Count, Is.EqualTo(1));
            }
        }

        [TestCase(401), TestCase(403), TestCase(507), TestCase(503)]
        public void ProbeErrorsRemainErrorsInsteadOfClaimingUnsupported(int status)
        {
            using (var http = new HttpClient(new Wire { ProbeStatus = status }))
            using (var dav = new WebDavClient(http))
                Assert.ThrowsAsync<ResponseException>(async () => await new DavChunkUploadClient(dav, Root).IsSupportedAsync());
        }

        [TestCase("PUT"), TestCase("MOVE"), TestCase("MKCOL")]
        public void FailedWritesAreNotReplayedAndOwnedSessionIsRemoved(string failing)
        {
            var wire = new Wire { FailMethod = failing };
            using (var http = new HttpClient(wire))
            using (var dav = new WebDavClient(http))
            using (var input = new MemoryStream(new byte[32]))
            {
                Assert.ThrowsAsync<ResponseException>(async () => await new DavChunkUploadClient(dav, Root).UploadAsync(Destination, input, input.Length));
                Assert.That(wire.Requests.Count(r => r.Method == failing), Is.EqualTo(1));
                Assert.That(wire.SessionExists, Is.False);
                Assert.That(wire.Requests.Count(r => r.Method == "DELETE"), Is.EqualTo(1));
                if (failing != "MOVE") Assert.That(wire.Requests.Any(r => r.Method == "MOVE"), Is.False);
            }
        }

        [Test]
        public void CleanupFailureDoesNotHideOriginalChunkFailure()
        {
            var wire = new Wire { FailMethod = "PUT", FailCleanup = true };
            using (var http = new HttpClient(wire))
            using (var dav = new WebDavClient(http))
            using (var input = new MemoryStream(new byte[32]))
            {
                var ex = Assert.ThrowsAsync<ResponseException>(async () => await new DavChunkUploadClient(dav, Root).UploadAsync(Destination, input, input.Length));
                Assert.That(ex.Message, Does.Contain("Chunk upload failed"));
                Assert.That(ex.Data["UploadSessionCleanupFailure"], Is.InstanceOf<string>());
                Assert.That((string)ex.Data["UploadSessionCleanupFailure"], Does.Contain("Upload-session cleanup failed"));
            }
        }

        [Test]
        public void CancellationUsesIndependentCleanupToken()
        {
            using (var cancellation = new CancellationTokenSource())
            {
                var wire = new Wire { CancelAtPut = cancellation };
                using (var http = new HttpClient(wire))
                using (var dav = new WebDavClient(http))
                using (var input = new MemoryStream(new byte[32]))
                {
                    Assert.CatchAsync<OperationCanceledException>(async () => await new DavChunkUploadClient(dav, Root).UploadAsync(Destination, input, input.Length, cancellationToken: cancellation.Token));
                    Assert.That(wire.SessionExists, Is.False);
                    Assert.That(wire.Requests.Count(r => r.Method == "DELETE"), Is.EqualTo(1));
                }
            }
        }

        [Test]
        public void ExistingSessionAndForeignOriginAreRejectedBeforeWriting()
        {
            var wire = new Wire { SessionExists = true };
            using (var http = new HttpClient(wire))
            using (var dav = new WebDavClient(http))
            using (var input = new MemoryStream(new byte[32]))
            {
                var client = new DavChunkUploadClient(dav, Root);
                Assert.ThrowsAsync<ArgumentException>(async () => await client.UploadAsync(new Uri("https://other.test/target"), input, input.Length));
                Assert.That(wire.Requests, Is.Empty);
                Assert.ThrowsAsync<IOException>(async () => await client.UploadAsync(Destination, input, input.Length));
                Assert.That(wire.Requests.All(r => r.Method == "PROPFIND"), Is.True);
                Assert.That(wire.SessionExists, Is.True, "A pre-existing session is not owned by this attempt.");
            }
        }

        private sealed class Request
        {
            internal string Method, Uri, Destination, Time;
        }

        private sealed class Wire : HttpMessageHandler
        {
            internal readonly List<Request> Requests = new List<Request>();
            internal readonly List<byte[]> Chunks = new List<byte[]>();
            internal int ProbeStatus = 207;
            internal bool SessionExists, FailCleanup;
            internal string FailMethod;
            internal CancellationTokenSource CancelAtPut;
            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage message, CancellationToken token)
            {
                token.ThrowIfCancellationRequested();
                var request = new Request { Method = message.Method.Method, Uri = message.RequestUri.AbsoluteUri };
                IEnumerable<string> values;
                if (message.Headers.TryGetValues("Destination", out values)) request.Destination = values.Single();
                if (message.Headers.TryGetValues("X-OC-Mtime", out values)) request.Time = values.Single();
                Requests.Add(request);
                int status = 201;
                if (request.Method == "PROPFIND")
                {
                    status = request.Uri == Root.AbsoluteUri ? ProbeStatus : SessionExists ? 207 : 404;
                    return new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent(status == 207 ?
                        "<d:multistatus xmlns:d=\"DAV:\"><d:response><d:href>" + message.RequestUri.AbsolutePath + "</d:href><d:propstat><d:prop><d:resourcetype><d:collection/></d:resourcetype></d:prop><d:status>HTTP/1.1 200 OK</d:status></d:propstat></d:response></d:multistatus>" : "") };
                }
                if (request.Method == "MKCOL") SessionExists = true;
                if (request.Method == "PUT")
                {
                    if (CancelAtPut != null)
                    {
                        CancelAtPut.Cancel();
                        token.ThrowIfCancellationRequested();
                    }
                    Chunks.Add(await message.Content.ReadAsByteArrayAsync().ConfigureAwait(false));
                }
                if (request.Method == FailMethod) status = 503;
                else if (request.Method == "MOVE") SessionExists = false;
                if (request.Method == "DELETE")
                {
                    status = FailCleanup ? 503 : 204;
                    if (!FailCleanup) SessionExists = false;
                }
                return new HttpResponseMessage((HttpStatusCode)status) { Content = new StringContent("") };
            }
        }
    }
}
