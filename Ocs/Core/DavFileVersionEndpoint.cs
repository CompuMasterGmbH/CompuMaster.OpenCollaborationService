using System;
using System.Text.RegularExpressions;

namespace CompuMaster.Ocs.Core
{
    /// <summary>Resolves file-version DAV addresses for a selected personal folder or space.</summary>
    /// <remarks>Resolution does not discover capabilities or authenticate. Legacy endpoints require the authenticated protocol user ID, never a login or display name. The selected subfolder and server prefix are preserved.</remarks>
    public sealed class DavFileVersionEndpoint
    {
        private DavFileVersionEndpoint(Uri davRoot, Uri filesRoot, string userId)
        {
            DavRoot = davRoot;
            FilesRoot = filesRoot;
            UserId = userId;
        }

        /// <summary>Gets the DAV root containing the historical version namespace.</summary>
        public Uri DavRoot { get; }

        /// <summary>Gets the selected current-file folder, with a trailing slash.</summary>
        public Uri FilesRoot { get; }

        /// <summary>Gets the protocol user ID used for Nextcloud version access, or null when unnecessary.</summary>
        public string UserId { get; }

        /// <summary>Resolves a supported current-file folder without making network requests.</summary>
        /// <param name="filesRoot">The absolute HTTP(S) personal-file, space or legacy DAV folder address.</param>
        /// <param name="protocol">The independently observed server protocol.</param>
        /// <param name="authenticatedUserId">The protocol ID returned by authenticated user discovery, required for legacy paths.</param>
        /// <returns>The version DAV root, current-file folder and protocol user identity.</returns>
        /// <exception cref="ArgumentNullException">The address is null.</exception>
        /// <exception cref="ArgumentException">The address, protocol or identity contains invalid components.</exception>
        /// <exception cref="NotSupportedException">The endpoint cannot be mapped safely for the selected protocol.</exception>
        public static DavFileVersionEndpoint Resolve(Uri filesRoot, DavFileVersionProtocol protocol, string authenticatedUserId = null)
        {
            if (filesRoot == null) throw new ArgumentNullException(nameof(filesRoot));
            if (!Enum.IsDefined(typeof(DavFileVersionProtocol), protocol)) throw new ArgumentException("Unknown version protocol.", nameof(protocol));
            if (!filesRoot.IsAbsoluteUri || (filesRoot.Scheme != Uri.UriSchemeHttps && filesRoot.Scheme != Uri.UriSchemeHttp) ||
                filesRoot.UserInfo.Length != 0 || filesRoot.Query.Length != 0 || filesRoot.Fragment.Length != 0)
                throw new ArgumentException("A credential-free absolute HTTP(S) DAV folder is required.", nameof(filesRoot));
            // Inspect the original spelling as Uri normalization would otherwise hide dot segments.
            var original = filesRoot.OriginalString.Substring(filesRoot.OriginalString.IndexOf("://", StringComparison.Ordinal) + 3);
            var pathStart = original.IndexOf('/');
            if (pathStart >= 0) ValidatePath(original.Substring(pathStart));
            if (authenticatedUserId != null) ValidateSegment(authenticatedUserId);
            var path = filesRoot.AbsolutePath;
            var direct = Regex.Match(path, @"^(.*?/(?:remote\.php/)?dav/)(files|spaces)/([^/]+)(/.*)?$", RegexOptions.IgnoreCase);
            var legacy = Regex.Match(path, @"^(.*?/)(remote\.php/)?webdav(/.*)?$", RegexOptions.IgnoreCase);
            var directMarker = direct.Success ? direct.Groups[1].Value.Length - (direct.Groups[1].Value.EndsWith("/remote.php/dav/", StringComparison.OrdinalIgnoreCase) ? "/remote.php/dav/".Length : "/dav/".Length) : int.MaxValue;
            if (direct.Success && (!legacy.Success || directMarker < legacy.Groups[1].Value.Length - 1))
            {
                var id = Uri.UnescapeDataString(direct.Groups[3].Value);
                ValidateSegment(id);
                if (direct.Groups[2].Value.Equals("spaces", StringComparison.OrdinalIgnoreCase) && protocol != DavFileVersionProtocol.OwnCloudInfiniteScale)
                    throw new NotSupportedException("Space endpoints require the Infinite Scale protocol.");
                var userId = protocol == DavFileVersionProtocol.Nextcloud ? authenticatedUserId ?? id : null;
                return new DavFileVersionEndpoint(new Uri(filesRoot.GetLeftPart(UriPartial.Authority) + direct.Groups[1].Value),
                    new Uri(filesRoot.AbsoluteUri.TrimEnd('/') + "/"), userId);
            }
            if (!legacy.Success || authenticatedUserId == null)
                throw new NotSupportedException("Legacy DAV version access requires an authenticated protocol user ID.");
            var root = new Uri(filesRoot.GetLeftPart(UriPartial.Authority) + legacy.Groups[1].Value + legacy.Groups[2].Value + "dav/");
            var current = new Uri(root, "files/" + Uri.EscapeDataString(authenticatedUserId) + legacy.Groups[3].Value.TrimEnd('/') + "/");
            return new DavFileVersionEndpoint(root, current, protocol == DavFileVersionProtocol.Nextcloud ? authenticatedUserId : null);
        }

        private static void ValidatePath(string path)
        {
            foreach (var segment in path.Trim('/').Split('/')) ValidateSegment(Uri.UnescapeDataString(segment));
        }

        private static void ValidateSegment(string segment)
        {
            if (string.IsNullOrWhiteSpace(segment) || segment == "." || segment == ".." || segment.IndexOfAny(new[] { '/', '\\' }) >= 0)
                throw new ArgumentException("A DAV path or identity contains an invalid segment.");
            foreach (var character in segment)
                if (char.IsControl(character)) throw new ArgumentException("A DAV path or identity contains a control character.");
        }
    }
}
