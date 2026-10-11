using System;

namespace CompuMaster.Ocs.Core
{
    /// <summary>Identifies a server's DAV version protocol.</summary>
    public enum DavFileVersionProtocol
    {
        /// <summary>Uses the Nextcloud versions namespace and MOVE restoration.</summary>
        Nextcloud = 1,
        /// <summary>Uses the ownCloud Classic meta namespace and COPY restoration.</summary>
        OwnCloudServer = 2,
        /// <summary>Uses the Infinite Scale meta namespace with opaque resource IDs and COPY restoration.</summary>
        OwnCloudInfiniteScale = 3
    }

    /// <summary>Describes an available historical file version.</summary>
    /// <remarks>Instances belong to the client that listed them. IDs are opaque and are never inferred from dates, labels or display names. Missing metadata remains null.</remarks>
    public sealed class DavFileVersion
    {
        internal DavFileVersion(DavFileVersionClient owner, Uri file, string fileId, string versionId,
            DateTime? modified, long? length, string contentType, string etag, string authorId, string authorName, string label)
        {
            Owner = owner; FileUri = file; FileId = fileId; VersionId = versionId;
            LastModifiedUtc = modified.HasValue ? (modified.Value.Kind == DateTimeKind.Unspecified ? DateTime.SpecifyKind(modified.Value, DateTimeKind.Utc) : modified.Value.ToUniversalTime()) : (DateTime?)null;
            ContentLength = length; ContentType = contentType; ETag = etag;
            AuthorId = authorId; AuthorDisplayName = authorName; Label = label;
        }
        internal DavFileVersionClient Owner { get; }
        /// <summary>Gets the current-file URI observed during listing.</summary>
        public Uri FileUri { get; }
        /// <summary>Gets the stable server file ID.</summary>
        public string FileId { get; }
        /// <summary>Gets the opaque version ID.</summary>
        public string VersionId { get; }
        /// <summary>Gets the historical modification time in UTC, or null when unavailable.</summary>
        public DateTime? LastModifiedUtc { get; }
        /// <summary>Gets the historical length in bytes, or null when unavailable.</summary>
        public long? ContentLength { get; }
        /// <summary>Gets the historical media type, or null when unavailable.</summary>
        public string ContentType { get; }
        /// <summary>Gets the historical entity tag, or null when unavailable.</summary>
        public string ETag { get; }
        /// <summary>Gets the author's protocol user ID, or null when unavailable.</summary>
        public string AuthorId { get; }
        /// <summary>Gets the author's display name, or null when unavailable.</summary>
        public string AuthorDisplayName { get; }
        /// <summary>Gets the version label or tag, or null when unavailable.</summary>
        public string Label { get; }
    }
}
