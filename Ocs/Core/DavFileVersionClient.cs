using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Linq;
using CompuMaster.Ocs.Exceptions;
using WebDav;

namespace CompuMaster.Ocs.Core
{
    /// <summary>Lists, downloads, restores and, where advertised, removes historical DAV file versions.</summary>
    /// <remarks>The caller supplies the observed server protocol and retains authenticated transport ownership. Generic WebDAV is not sufficient evidence. No operation retries mutations or buffers a whole file. Server authorization and retention policies still apply.</remarks>
    public sealed class DavFileVersionClient
    {
        private readonly IWebDavClient transport;
        private readonly Uri davRoot;
        private readonly string userId;
        private readonly DavFileVersionProtocol protocol;
        private static readonly XNamespace Dav = "DAV:";
        private static readonly XNamespace Oc = "http://owncloud.org/ns";
        private static readonly XNamespace Nc = "http://nextcloud.org/ns";

        /// <summary>Initializes version access using a caller-owned authenticated DAV transport.</summary>
        /// <param name="transport">The authenticated transport, including redirect and request policies.</param>
        /// <param name="davRoot">The absolute DAV root ending in a slash, for example https://host/prefix/remote.php/dav/.</param>
        /// <param name="protocol">The observed protocol; product recognition alone does not establish that versioning is enabled.</param>
        /// <param name="userId">The protocol user ID for Nextcloud, separate from a display or login name. Other protocols do not use this value.</param>
        /// <param name="versionDeletionAdvertised">The authenticated Nextcloud files.version_deletion capability; null means unknown. Other protocols cannot remove individual versions through this client.</param>
        /// <exception cref="ArgumentException">The root, protocol or required user ID is invalid.</exception>
        /// <exception cref="ArgumentNullException">The transport or root is null.</exception>
        public DavFileVersionClient(IWebDavClient transport, Uri davRoot, DavFileVersionProtocol protocol,
            string userId = null, bool? versionDeletionAdvertised = null)
        {
            this.transport = transport ?? throw new ArgumentNullException(nameof(transport));
            this.davRoot = davRoot ?? throw new ArgumentNullException(nameof(davRoot));
            ValidateUri(davRoot);
            if (!davRoot.AbsolutePath.EndsWith("/", StringComparison.Ordinal)) throw new ArgumentException("The DAV root requires a trailing slash.", nameof(davRoot));
            if (!Enum.IsDefined(typeof(DavFileVersionProtocol), protocol)) throw new ArgumentException("An observed version protocol is required.", nameof(protocol));
            if (protocol == DavFileVersionProtocol.Nextcloud) ValidateId(userId);
            this.protocol = protocol; this.userId = userId;
            CanRemoveVersions = protocol == DavFileVersionProtocol.Nextcloud ? versionDeletionAdvertised : false;
        }

        /// <summary>Gets individual version-removal support, or null when the server has not advertised it.</summary>
        /// <remarks>True does not grant per-file authorization. False and unknown reject removal before sending a request.</remarks>
        public bool? CanRemoveVersions { get; }

        /// <summary>Lists the available historical versions of the selected current file.</summary>
        /// <param name="file">The current-file URI within this DAV root.</param>
        /// <param name="expectedFileId">The optional previously observed file ID; a replaced file is rejected even when history is empty.</param>
        /// <returns>Historical versions with their stable file/version IDs; an empty list means no retained versions.</returns>
        /// <exception cref="ResponseException">The file, namespace or permissions prevent listing.</exception>
        /// <exception cref="InvalidDataException">The server omits file identity or returns a malformed version response.</exception>
        public IReadOnlyList<DavFileVersion> ListVersions(Uri file, string expectedFileId = null) => ListVersionsAsync(file, expectedFileId: expectedFileId).GetAwaiter().GetResult();

        /// <inheritdoc cref="ListVersions"/>
        /// <param name="file">The current-file URI within this DAV root.</param>
        /// <param name="cancellationToken">Cancels identity resolution and listing.</param>
        /// <param name="expectedFileId">The optional previously observed file ID.</param>
        public async Task<IReadOnlyList<DavFileVersion>> ListVersionsAsync(Uri file, CancellationToken cancellationToken = default(CancellationToken), string expectedFileId = null)
        {
            var identity = await FileIdentity(file, cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(expectedFileId) && identity.Id != expectedFileId) throw new InvalidOperationException("The selected file identity changed before listing.");
            var root = VersionsRoot(identity.Id);
            var response = await transport.Propfind(root, new PropfindParameters {
                ApplyTo = ApplyTo.Propfind.ResourceAndChildren, CancellationToken = cancellationToken,
                RequestType = PropfindRequestType.NamedProperties,
                CustomProperties = new[] { Dav + "resourcetype", Dav + "getlastmodified", Dav + "getcontentlength", Dav + "getcontenttype", Dav + "getetag",
                    Nc + "version-author", Nc + "version-label", Oc + "meta-version-edited-by", Oc + "meta-version-edited-by-name", Oc + "meta-version-tag" }
            }).ConfigureAwait(false);
            Check(response, "Version listing failed.");
            var result = new List<DavFileVersion>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            bool collectionFound = false;
            foreach (var resource in response.Resources)
            {
                var address = new Uri(davRoot, resource.Uri);
                ValidateUri(address);
                if (address.AbsoluteUri.TrimEnd('/') == root.AbsoluteUri.TrimEnd('/')) { collectionFound = resource.IsCollection; continue; }
                if (!Within(address, root) || resource.IsCollection) throw new InvalidDataException("A version is outside the selected file's version collection.");
                var relative = address.AbsoluteUri.Substring(root.AbsoluteUri.Length);
                var versionId = Uri.UnescapeDataString(relative);
                ValidateId(versionId);
                if (relative.Contains("/") || !seen.Add(versionId)) throw new InvalidDataException("A version ID is nested or duplicated.");
                result.Add(new DavFileVersion(this, file, identity.Id, versionId, resource.LastModifiedDate,
                    resource.ContentLength, resource.ContentType, resource.ETag,
                    Text(resource, protocol == DavFileVersionProtocol.Nextcloud ? Nc + "version-author" : Oc + "meta-version-edited-by"),
                    Text(resource, Oc + "meta-version-edited-by-name"),
                    Text(resource, protocol == DavFileVersionProtocol.Nextcloud ? Nc + "version-label" : Oc + "meta-version-tag")));
            }
            if (!collectionFound) throw new InvalidDataException("The selected version collection is missing from the DAV response.");
            return result.AsReadOnly();
        }

        /// <summary>Streams the exact historical content into a caller-owned destination.</summary>
        /// <param name="version">A snapshot listed by this client.</param>
        /// <param name="destination">The writable caller-owned destination, left open on success or failure.</param>
        /// <remarks>A failed or cancelled download may leave partial content in the destination. The response stream is always disposed.</remarks>
        /// <exception cref="ArgumentException">The snapshot or destination is invalid.</exception>
        /// <exception cref="ResponseException">The historical content is absent or inaccessible.</exception>
        public void DownloadVersion(DavFileVersion version, Stream destination) => DownloadVersionAsync(version, destination).GetAwaiter().GetResult();

        /// <inheritdoc cref="DownloadVersion"/>
        /// <param name="version">A snapshot listed by this client.</param>
        /// <param name="destination">The writable caller-owned destination.</param>
        /// <param name="cancellationToken">Cancels the request and active content transfer.</param>
        public async Task DownloadVersionAsync(DavFileVersion version, Stream destination, CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidateVersion(version);
            if (destination == null) throw new ArgumentNullException(nameof(destination));
            if (!destination.CanWrite) throw new ArgumentException("A writable destination is required.", nameof(destination));
            cancellationToken.ThrowIfCancellationRequested();
            using (var response = await transport.GetRawFile(VersionUri(version), new GetFileParameters { CancellationToken = cancellationToken }).ConfigureAwait(false))
            {
                Check(response, "Historical download failed.");
                await response.Stream.CopyToAsync(destination, 81920, cancellationToken).ConfigureAwait(false);
            }
        }

        /// <summary>Restores historical content to the same current file identity.</summary>
        /// <param name="version">A snapshot listed by this client.</param>
        /// <remarks>Rejects a replaced or missing current file. Restoration changes current content; it does not rename a file or remove unrelated versions. Refresh current metadata and history afterwards. A transport failure may leave an ambiguous server result and must not be replayed automatically.</remarks>
        /// <exception cref="InvalidOperationException">The current file identity changed since listing.</exception>
        /// <exception cref="ResponseException">The server rejects restoration.</exception>
        public void RestoreVersion(DavFileVersion version) => RestoreVersionAsync(version).GetAwaiter().GetResult();

        /// <inheritdoc cref="RestoreVersion"/>
        /// <param name="version">A snapshot listed by this client.</param>
        /// <param name="cancellationToken">Cancels validation and the restore request; cancellation does not undo a server mutation.</param>
        public async Task RestoreVersionAsync(DavFileVersion version, CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidateVersion(version);
            var current = await VerifyIdentity(version, cancellationToken).ConfigureAwait(false);
            WebDavResponse response;
            if (protocol == DavFileVersionProtocol.Nextcloud)
                response = await transport.Move(VersionUri(version), new Uri(davRoot, "versions/" + Segment(userId) + "/restore"),
                    new MoveParameters { CancellationToken = cancellationToken, Overwrite = true }).ConfigureAwait(false);
            else
            {
                if (string.IsNullOrEmpty(current.ETag) || !current.ETag.StartsWith("\"", StringComparison.Ordinal) || !current.ETag.EndsWith("\"", StringComparison.Ordinal) || current.ETag.Any(char.IsControl) || current.ETag.Contains("]"))
                    throw new InvalidDataException("A strong current-file ETag is required for conditional restoration.");
                response = await transport.Copy(VersionUri(version), version.FileUri, new CopyParameters {
                    CancellationToken = cancellationToken, Overwrite = true,
                    Headers = new[] { new KeyValuePair<string, string>("If", "<" + version.FileUri.AbsoluteUri + "> ([" + current.ETag + "])") }
                }).ConfigureAwait(false);
            }
            Check(response, "Version restoration failed.");
        }

        /// <summary>Removes one historical version when the server explicitly advertises individual removal.</summary>
        /// <param name="version">A snapshot listed by this client.</param>
        /// <exception cref="NotSupportedException">Removal is unsupported or unknown.</exception>
        /// <exception cref="ResponseException">The server denies removal or the version no longer exists.</exception>
        public void RemoveVersion(DavFileVersion version) => RemoveVersionAsync(version).GetAwaiter().GetResult();

        /// <inheritdoc cref="RemoveVersion"/>
        /// <param name="version">A snapshot listed by this client.</param>
        /// <param name="cancellationToken">Cancels validation and removal; cancellation does not undo a server mutation.</param>
        public async Task RemoveVersionAsync(DavFileVersion version, CancellationToken cancellationToken = default(CancellationToken))
        {
            ValidateVersion(version);
            if (CanRemoveVersions != true) throw new NotSupportedException("Individual version removal is not advertised for this protocol.");
            await VerifyIdentity(version, cancellationToken).ConfigureAwait(false);
            Check(await transport.Delete(VersionUri(version), new DeleteParameters { CancellationToken = cancellationToken }).ConfigureAwait(false), "Version removal failed.");
        }

        private sealed class Identity
        {
            internal string Id;
            internal string ETag;
        }
        private async Task<Identity> FileIdentity(Uri file, CancellationToken token)
        {
            ValidateUri(file);
            if (!Within(file, davRoot)) throw new ArgumentException("The current file must be within the authenticated DAV root.", nameof(file));
            token.ThrowIfCancellationRequested();
            var response = await transport.Propfind(file, new PropfindParameters {
                ApplyTo = ApplyTo.Propfind.ResourceOnly, CancellationToken = token, RequestType = PropfindRequestType.NamedProperties,
                CustomProperties = new[] { Oc + "fileid", Oc + "id", Dav + "resourcetype", Dav + "getetag" }
            }).ConfigureAwait(false);
            Check(response, "Current file identity lookup failed.");
            if (response.Resources.Count != 1) throw new InvalidDataException("The current file is missing or ambiguous.");
            var item = response.Resources.Single();
            if (item == null || item.IsCollection || new Uri(davRoot, item.Uri) != file)
                throw new InvalidDataException("The current file is missing or ambiguous.");
            var id = Text(item, Oc + "fileid") ?? Text(item, Oc + "id");
            if (string.IsNullOrEmpty(id)) throw new InvalidDataException("The server did not supply a stable file ID.");
            ValidateId(id);
            return new Identity { Id = id, ETag = item.ETag };
        }
        private async Task<Identity> VerifyIdentity(DavFileVersion version, CancellationToken token)
        {
            var current = await FileIdentity(version.FileUri, token).ConfigureAwait(false);
            if (current.Id != version.FileId) throw new InvalidOperationException("The current file identity changed since listing.");
            return current;
        }
        private Uri VersionsRoot(string fileId) => new Uri(davRoot, protocol == DavFileVersionProtocol.Nextcloud
            ? "versions/" + Segment(userId) + "/versions/" + Segment(fileId) + "/" : "meta/" + Segment(fileId) + "/v/");
        private Uri VersionUri(DavFileVersion version) => new Uri(VersionsRoot(version.FileId), Segment(version.VersionId));
        private void ValidateVersion(DavFileVersion version)
        {
            if (version == null) throw new ArgumentNullException(nameof(version));
            if (!ReferenceEquals(version.Owner, this)) throw new ArgumentException("The version belongs to another client or account.", nameof(version));
        }
        private static string Text(WebDavResource item, XName name)
        {
            if (item.PropertyStatuses != null && item.PropertyStatuses.Any(p => p.Name == name && !p.IsSuccessful)) return null;
            var property = item.Properties.FirstOrDefault(p => p.Name == name);
            if (property == null) return null;
            var value = XElement.Parse("<root>" + property.Value + "</root>");
            if (value.HasElements) return null;
            return string.IsNullOrWhiteSpace(value.Value) ? null : value.Value;
        }
        private static void Check(WebDavResponse response, string message)
        {
            if (!response.IsSuccessful) throw new ResponseException(message, (HttpStatusCode)response.StatusCode, null);
        }
        private static string Segment(string value) { ValidateId(value); return Uri.EscapeDataString(value); }
        private static void ValidateId(string value)
        {
            if (string.IsNullOrWhiteSpace(value) || value == "." || value == ".." || value.IndexOfAny(new[] { '/', '\\' }) >= 0 || value.Any(char.IsControl))
                throw new ArgumentException("A nonempty opaque path-segment ID is required.");
        }
        private static void ValidateUri(Uri value)
        {
            if (value == null) throw new ArgumentNullException(nameof(value));
            if (!value.IsAbsoluteUri || (value.Scheme != "https" && value.Scheme != "http") || value.UserInfo.Length != 0 || value.Query.Length != 0 || value.Fragment.Length != 0)
                throw new ArgumentException("An absolute HTTP(S) URI without user information, query or fragment is required.");
        }
        private static bool Within(Uri value, Uri root) => value.Scheme == root.Scheme && value.IdnHost == root.IdnHost && value.Port == root.Port && value.AbsolutePath.StartsWith(root.AbsolutePath, StringComparison.Ordinal);
    }
}
