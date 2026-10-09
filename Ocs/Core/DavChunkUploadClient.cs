using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Threading.Tasks;
using CompuMaster.Ocs.Exceptions;
using WebDav;

namespace CompuMaster.Ocs.Core
{
    /// <summary>Uploads files through the native ownCloud and Nextcloud DAV chunk namespace.</summary>
    /// <remarks>Requires an authenticated transport and a verified native upload namespace. Ordinary WebDAV does not define this protocol. The caller retains transport and source-stream ownership.</remarks>
    public sealed class DavChunkUploadClient
    {
        private readonly IWebDavClient transport;
        private readonly Uri uploadRoot;
        private const long DefaultChunkSize = 10L * 1024 * 1024;
        private const long MaximumChunkSize = 5L * 1024 * 1024 * 1024;

        /// <summary>Initializes a client using an existing authenticated DAV transport.</summary>
        /// <param name="transport">The caller-owned authenticated transport, including its request policy.</param>
        /// <param name="uploadRoot">The absolute native uploads URI for the authenticated protocol user ID, ending with a slash.</param>
        /// <exception cref="ArgumentException">The upload root is not an absolute HTTP or HTTPS URI, contains user information, a query or fragment, or lacks its trailing slash.</exception>
        /// <exception cref="ArgumentNullException">A required argument is null.</exception>
        public DavChunkUploadClient(IWebDavClient transport, Uri uploadRoot)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.uploadRoot = uploadRoot ?? throw new ArgumentNullException(nameof(uploadRoot));
            if (!uploadRoot.IsAbsoluteUri || (uploadRoot.Scheme != "https" && uploadRoot.Scheme != "http") ||
                uploadRoot.UserInfo.Length != 0 || uploadRoot.Query.Length != 0 || uploadRoot.Fragment.Length != 0 ||
                !uploadRoot.AbsolutePath.EndsWith("/", StringComparison.Ordinal))
                throw new ArgumentException("An absolute HTTP(S) upload namespace with a trailing slash is required.", nameof(uploadRoot));
        }

        /// <summary>Checks whether the authenticated native upload namespace is available.</summary>
        /// <param name="cancellationToken">Cancels the capability request.</param>
        /// <returns>True when the namespace returns a collection; false for a missing or unsupported namespace.</returns>
        /// <remarks>Authentication, permission, transport and other server failures remain exceptions. This check does not create files or establish support for arbitrary generic DAV servers.</remarks>
        /// <exception cref="ResponseException">The server rejects the request for a reason other than an unsupported namespace.</exception>
        public async Task<bool> IsSupportedAsync(CancellationToken cancellationToken = default(CancellationToken))
        {
            var response = await transport.Propfind(uploadRoot, ResourceOnly(cancellationToken)).ConfigureAwait(false);
            if (response.StatusCode == 404 || response.StatusCode == 405 || response.StatusCode == 501) return false;
            Check(response, "Native upload capability check failed.");
            return response.Resources.Any(resource => resource.IsCollection);
        }

        /// <summary>Streams ordered chunks and assembles the destination file on the server.</summary>
        /// <param name="destination">The absolute destination URI on the same origin as the upload namespace.</param>
        /// <param name="source">The caller-owned readable source, positioned at the first byte to upload.</param>
        /// <param name="length">The exact number of bytes remaining in the source.</param>
        /// <param name="modificationTime">The optional source modification time. Local times are converted to UTC and unspecified times are interpreted as UTC.</param>
        /// <param name="cancellationToken">Cancels upload preparation, individual chunks and final assembly.</param>
        /// <returns>A task completed only after server-confirmed assembly and upload-session cleanup.</returns>
        /// <remarks>Uses a unique session, at most 10000 chunks, no whole-file buffering and no automatic replay of ambiguous writes. Empty files should use ordinary PUT. Cleanup has an independent deadline; a cleanup failure is attached to the original exception when one exists. Callers must verify native support before selecting this workflow.</remarks>
        /// <exception cref="ArgumentException">The destination or source is invalid.</exception>
        /// <exception cref="ArgumentOutOfRangeException">The length is nonpositive or cannot fit the native chunk limits.</exception>
        /// <exception cref="IOException">Session absence cannot be established or the source ends before its declared length.</exception>
        /// <exception cref="ResponseException">The server rejects a chunk, assembly or cleanup.</exception>
        public async Task UploadAsync(Uri destination, Stream source, long length, DateTime? modificationTime = null, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (!destination.IsAbsoluteUri || destination.Scheme != uploadRoot.Scheme || destination.IdnHost != uploadRoot.IdnHost ||
                destination.Port != uploadRoot.Port || destination.UserInfo.Length != 0 || destination.Query.Length != 0 || destination.Fragment.Length != 0)
                throw new ArgumentException("The destination must share the authenticated upload origin.", nameof(destination));
            if (!source.CanRead) throw new ArgumentException("The source must be readable.", nameof(source));
            if (length <= 0) throw new ArgumentOutOfRangeException(nameof(length));
            var chunkSize = Math.Max(DefaultChunkSize, length / 10000 + (length % 10000 == 0 ? 0 : 1));
            if (chunkSize > MaximumChunkSize) throw new ArgumentOutOfRangeException(nameof(length));
            cancellationToken.ThrowIfCancellationRequested();
            var session = new Uri(uploadRoot, "cm-ocs-" + Guid.NewGuid().ToString("N") + "/");
            var absence = await transport.Propfind(session, ResourceOnly(cancellationToken)).ConfigureAwait(false);
            if (absence.StatusCode != 404)
            {
                Check(absence, "Upload-session absence check failed.");
                throw new IOException("The owned upload session already exists.");
            }
            var headers = new List<KeyValuePair<string, string>>
            {
                new KeyValuePair<string, string>("Destination", destination.AbsoluteUri),
                new KeyValuePair<string, string>("OC-Total-Length", length.ToString(CultureInfo.InvariantCulture))
            };
            Exception failure = null;
            try
            {
                Check(await transport.Mkcol(session, new MkColParameters { Headers = headers, CancellationToken = cancellationToken }).ConfigureAwait(false), "Upload-session creation failed.");
                long remaining = length;
                int index = 1;
                while (remaining > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    long count = Math.Min(chunkSize, remaining);
                    using (var chunk = new SourceSegment(source, count))
                    {
                        Check(await transport.PutFile(new Uri(session, index.ToString("D5", CultureInfo.InvariantCulture)), chunk,
                            new PutFileParameters { Headers = headers, CancellationToken = cancellationToken }).ConfigureAwait(false), "Chunk upload failed.");
                        if (chunk.Position != count) throw new EndOfStreamException("The source ended before its declared length.");
                    }
                    remaining -= count;
                    index++;
                }
                if (modificationTime.HasValue)
                {
                    var utc = modificationTime.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(modificationTime.Value, DateTimeKind.Utc) : modificationTime.Value.ToUniversalTime();
                    headers.Add(new KeyValuePair<string, string>("X-OC-Mtime", new DateTimeOffset(utc).ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture)));
                }
                Check(await transport.Move(new Uri(session, ".file"), destination,
                    new MoveParameters { Headers = headers, CancellationToken = cancellationToken, Overwrite = true }).ConfigureAwait(false), "Chunk assembly failed.");
            }
            catch (Exception ex) { failure = ex; }
            try
            {
                //Preflight established absence; this unique session is owned by this attempt,
                //including when MKCOL's response was lost after the server created it.
                using (var cleanup = new CancellationTokenSource(TimeSpan.FromMinutes(2)))
                {
                    var removed = await transport.Delete(session, new DeleteParameters { CancellationToken = cleanup.Token }).ConfigureAwait(false);
                    if (removed.StatusCode != 404) Check(removed, "Upload-session cleanup failed.");
                    var absent = await transport.Propfind(session, ResourceOnly(cleanup.Token)).ConfigureAwait(false);
                    if (absent.StatusCode != 404)
                    {
                        Check(absent, "Upload-session cleanup verification failed.");
                        throw new IOException("The owned upload session remains after cleanup.");
                    }
                }
            }
            catch (Exception cleanup)
            {
                if (failure == null) failure = cleanup;
                //Exception.Data requires serializable values on .NET Framework;
                //retain the complete cleanup diagnostic without replacing the original failure.
                else failure.Data["UploadSessionCleanupFailure"] = cleanup.ToString();
            }
            if (failure != null) ExceptionDispatchInfo.Capture(failure).Throw();
        }

        private static PropfindParameters ResourceOnly(CancellationToken token)
            => new PropfindParameters { ApplyTo = ApplyTo.Propfind.ResourceOnly, CancellationToken = token };

        private static void Check(WebDavResponse response, string message)
        {
            if (!response.IsSuccessful) throw new ResponseException(message + " " + response.Description, (HttpStatusCode)response.StatusCode, null);
        }

        private sealed class SourceSegment : Stream
        {
            private readonly Stream source;
            private readonly long size;
            private readonly long start;
            private long consumed;
            internal SourceSegment(Stream source, long size)
            {
                this.source = source;
                this.size = size;
                start = source.CanSeek ? source.Position : 0;
            }
            public override int Read(byte[] buffer, int offset, int count)
            {
                int amount = source.Read(buffer, offset, (int)Math.Min(count, size - consumed));
                consumed += amount;
                return amount;
            }
            public override async Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken)
            {
                int amount = await source.ReadAsync(buffer, offset, (int)Math.Min(count, size - consumed), cancellationToken).ConfigureAwait(false);
                consumed += amount;
                return amount;
            }
            public override bool CanRead => true;
            public override bool CanSeek => source.CanSeek;
            public override bool CanWrite => false;
            public override long Length => size;
            public override long Position
            {
                get => consumed;
                set
                {
                    if (!source.CanSeek) throw new NotSupportedException();
                    if (value < 0 || value > size) throw new ArgumentOutOfRangeException(nameof(value));
                    source.Position = start + value;
                    consumed = value;
                }
            }
            public override long Seek(long offset, SeekOrigin origin)
            {
                Position = offset + (origin == SeekOrigin.Begin ? 0 : origin == SeekOrigin.Current ? consumed : size);
                return Position;
            }
            public override void Flush() => source.Flush();
            public override void SetLength(long value) => throw new NotSupportedException();
            public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();
            //Disposing a chunk leaves the caller-owned source open for the next chunk.
        }
    }
}
