using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Net;
using System.Xml;
using System.Xml.Linq;
using RestSharp;
using RestSharp.Authenticators;
using WebDav;
using CompuMaster.Ocs.Exceptions;
using CompuMaster.Ocs.Types;
using CompuMaster.Ocs.Core;
using System.Linq;
using System.Diagnostics;

namespace CompuMaster.Ocs
{
    /// <summary>
    /// Open Collaboration Services OCS and WebDAV access client for OwnCloud, Nextcloud, etc.
    /// </summary>
    public class OcsClient
    {
        #region Members
        /// <summary>
        /// RestSharp instance
        /// </summary>
        private RestClient rest;
        /// <summary>
        /// WebDavNet instance
        /// </summary>
        private IWebDavClient dav;
        /// <summary>
        /// Base URL (e.g. http://server.mydomain, http://server.mydomain/owncloud)
        /// </summary>
        /// <remarks>Url is always without ending slash</remarks>
        private string url;
        /// <summary>
        /// Username of authorized user
        /// </summary>
        private string user_id;
        /// <summary>
        /// WebDAV access path
        /// </summary>
        private const string davpath = "remote.php/webdav";
        /// <summary>
        /// OCS API access path
        /// </summary>
        private const string ocspath = "ocs/v1.php/";
        /// <summary>
        /// OCS Share API path
        /// </summary>
        private const string ocsServiceShare = "apps/files_sharing/api/v1";
        private const string ocsServiceData = "privatedata";
        /// <summary>
        /// OCS Provisioning API path
        /// </summary>
        private const string ocsServiceCloud = "cloud";
        /// <summary>
        /// The directory separator char
        /// </summary>
        public readonly char DirectorySeparatorChar = '/';
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the <see cref="CompuMaster.Ocs.OcsClient"/> class.
        /// </summary>
        /// <param name="url">ownCloud instance URL</param>
        /// <param name="user_id">User identifier</param>
        /// <param name="password">Password</param>
        public OcsClient(string url, string user_id, string password)
        {
            // In case URL has a trailing slash remove it
            if ((url != null) && (url.EndsWith("/")))
                url = url.TrimEnd(new[] { '/' });

            // Store ownCloud base URL
            this.url = url;

            // RestSharp initialisation
            var options = new RestClientOptions
            {
                // Set the base path as the OCS API root
                //this.rest.BaseUrl = new Uri(url + "/" + ocspath);
                BaseUrl = new Uri(url + "/" + ocspath),
                // Configure RestSharp for BasicAuth
                Authenticator = new RestSharp.Authenticators.HttpBasicAuthenticator(user_id, password)
            };
            this.rest = new RestClient(options);

            // Add default parameter 
            this.rest.AddDefaultParameter("format", "xml");
            this.user_id = user_id;

            var clientParams = new WebDavClientParams
            {
                BaseAddress = new Uri(url + "/" + ocspath),
                Credentials = new NetworkCredential(user_id, password)
            };
            // WebDavNet initialisation
            this.dav = new WebDavClient(clientParams);

        }
        #endregion

        #region Authorized credentials
        /// <summary>
        /// Server base URL (e.g. http://server.mydomain, http://server.mydomain/owncloud)
        /// </summary>
        /// <remarks>Url is always without ending slash</remarks>
        public string BaseUrl { get => this.url; }
        /// <summary>
        /// The authorization user
        /// </summary>
        public string AuthorizedUserID { get => this.user_id; }
        /// <summary>
        /// WebDav base URL (e.g. http://server.mydomain/remote.php/webdav, http://server.mydomain/owncloud/remote.php/webdav)
        /// </summary>
        /// <remarks>Url is always without ending slash</remarks>
        public string WebDavBaseUrl { get => this.url + "/" + davpath; }
        #endregion

        #region DAV
        /// <summary>
        /// List the children elements of the specified remote path
        /// </summary>
        /// <param name="path">remote Path</param>
        /// <returns>List of Resources</returns>
        public List<ResourceInfo> List(string path)
        {
            if (path == null || path == "") throw new ArgumentNullException(nameof(path));

            List<ResourceInfo> resources = new List<ResourceInfo>();
            var result = this.dav.Propfind(GetDavUri(path)).Result;
            OcsDeserializationTools.CheckDavStatus(result);

            foreach (var item in result.Resources)
            {
                ResourceInfo res = new ResourceInfo(this.ConvertDavPathToPath(item.Uri), item.IsCollection);
                if (res.FullPath == path)
                {
                    //skip element, since it's the list item itself
                    continue;
                }
                else
                {
                    //regular child item 
                    if (item.IsCollection) // if resource is a directory set special content type
                        res.ContentType = "dav/directory";
                    else
                        res.ContentType = item.ContentType;
                    res.Created = item.CreationDate ?? DateTime.MinValue;
                    res.ETag = item.ETag;
                    res.LastModified = item.LastModifiedDate ?? DateTime.MinValue;
                    res.DisplayName = item.DisplayName;
                    res.Size = item.ContentLength;
                    resources.Add(res);
                }
            }

            return resources;
        }

        /// <summary>
        /// Gets the resource info for the remote path
        /// </summary>
        /// <returns>The resource info</returns>
        /// <param name="path">remote Path</param>
        public ResourceInfo GetResourceInfo(string path)
        {
            if (path == null || path == "") throw new ArgumentNullException(nameof(path));

            var result = this.dav.Propfind(GetDavUri(path)).Result;
            OcsDeserializationTools.CheckDavStatus(result);

            if (result.Resources.Count > 0)
            {
                var item = result.Resources.FirstOrDefault();
                ResourceInfo res = new ResourceInfo(this.ConvertDavPathToPath(item.Uri), item.IsCollection);
                if (item.IsCollection) // if resource is a directory set special content type
                    res.ContentType = "dav/directory";
                else
                    res.ContentType = item.ContentType;
                res.Created = item.CreationDate ?? DateTime.MinValue;
                res.ETag = item.ETag;
                res.LastModified = item.LastModifiedDate ?? DateTime.MinValue;
                res.DisplayName = item.DisplayName;
                res.Size = item.ContentLength;
                return res;
            }

            return null;
        }

        private string DavPathPreRootDir { get => GetDavUri("").AbsolutePath; }
        private string ConvertDavPathToPath(string davPath)
        {
            string cutOffPath = this.DavPathPreRootDir;
            if (davPath.StartsWith(cutOffPath))
            {
                return System.Net.WebUtility.UrlDecode(davPath.Substring(cutOffPath.Length));
            }
            else
            {
                throw new InvalidOperationException(@"DAV path """ + davPath + @""" can't be converted to regular path with prefixPath=""" + cutOffPath + @"""");
            }
        }

        /// <summary>
        /// Download the specified file
        /// </summary>
        /// <param name="path">File remote Path</param>
        /// <returns>File contents</returns>
        public Stream Download(string path)
        {
            var result = dav.GetRawFile(GetDavUri(path)).Result;
            OcsDeserializationTools.CheckDavStatus(result);
            return result.Stream;
        }

        /// <summary>
        /// Upload the specified file to the specified path
        /// </summary>
        /// <param name="path">remote Path</param>
        /// <param name="data">File contents</param>
        /// <param name="contentType">File content type</param>
        /// <returns><c>true</c>, if upload successful, <c>false</c> otherwise</returns>
        public void Upload(string path, Stream data, string contentType)
        {
            var result = dav.PutFile(GetDavUri(path), data, contentType).Result;
            OcsDeserializationTools.CheckDavStatus(result);
        }

        /// <summary>
        /// Upload the specified file to the specified path
        /// </summary>
        /// <param name="path">remote Path</param>
        /// <param name="data">File contents</param>
        /// <returns><c>true</c>, if upload successful, <c>false</c> otherwise</returns>
        public void Upload(string path, Stream data)
        {
            var result = dav.PutFile(GetDavUri(path), data).Result;
            OcsDeserializationTools.CheckDavStatus(result);
        }

        /// <summary>
        /// Checks if the specified remote path exists
        /// </summary>
        /// <param name="path">remote Path</param>
        /// <returns><c>true</c>, if remote path exists, <c>false</c> otherwise</returns>
        public bool Exists(string path)
        {
            var result = this.dav.Propfind(GetDavUri(path)).Result;
            if (result.StatusCode == 404)
                return false;
            else
            {
                OcsDeserializationTools.CheckDavStatus(result);
                return result.Resources.Count != 0;
            }
        }

        /// <summary>
        /// Creates a new directory at remote path
        /// </summary>
        /// <returns><c>true</c>, if directory was created, <c>false</c> otherwise</returns>
        /// <param name="path">remote Path</param>
        public void CreateDirectory(string path)
        {
            var result = dav.Mkcol(GetDavUri(path)).Result;
            OcsDeserializationTools.CheckDavStatus(result);
        }

        /// <summary>
        /// Delete resource at the specified remote path
        /// </summary>
        /// <param name="path">remote Path</param>
        /// <returns><c>true</c>, if resource was deleted, <c>false</c> otherwise</returns>
        public void Delete(string path)
        {
            var result = dav.Delete(GetDavUri(path)).Result;
            OcsDeserializationTools.CheckDavStatus(result);
        }

        /// <summary>
        /// Copy the specified source to destination
        /// </summary>
        /// <param name="source">Source resoure path</param>
        /// <param name="destination">Destination resource path</param>
        /// <returns><c>true</c>, if resource was copied, <c>false</c> otherwise</returns>
        public void Copy(string source, string destination)
        {
            var result = dav.Copy(GetDavUri(source), GetDavUri(destination)).Result;
            OcsDeserializationTools.CheckDavStatus(result);
        }

        /// <summary>
        /// Move the specified source and destination
        /// </summary>
        /// <param name="source">Source resource path</param>
        /// <param name="destination">Destination resource path</param>
        /// <returns><c>true</c>, if resource was moved, <c>false</c> otherwise</returns>
        public void Move(string source, string destination)
        {
            var result = dav.Move(GetDavUri(source), GetDavUri(destination)).Result;
            OcsDeserializationTools.CheckDavStatus(result);
        }

        /// <summary>
        /// Downloads a remote directory as zip (might work only for OwnCloud/Nextcloud due to specialized behaviour)
        /// </summary>
        /// <returns>The directory as zip</returns>
        /// <param name="path">path to the remote directory to download</param>
        public Stream DownloadDirectoryAsZip(string path)
        {
            var uri = GetUri("/index.php/apps/files/ajax/download.php?dir=" + WebUtility.UrlEncode(path));
            var result = dav.GetRawFile(uri).Result;
            OcsDeserializationTools.CheckDavStatus(result);
            return result.Stream;
        }
        #endregion

        #region OCS
        #region Remote Shares
        /// <summary>
        /// List all remote shares.
        /// </summary>
        /// <returns>List of remote shares</returns>
        public object ListOpenRemoteShare()
        {
            var request = new RestRequest(GetOcsPath(ocsServiceShare, "remote_shares"), Method.Get);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");
            var response = rest.ExecuteAsync(request).Result;

            OcsDeserializationTools.CheckOcsStatus(response);

            var content = response.Content;
            // TODO: Parse response
            return content;
        }

        /// <summary>
        /// Accepts a remote share
        /// </summary>
        /// <returns><c>true</c>, if remote share was accepted, <c>false</c> otherwise</returns>
        /// <param name="shareId">Share identifier</param>
        public void AcceptRemoteShare(int shareId)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceShare, "remote_shares") + "/{id}", Method.Post);
            request.AddHeader("Accept", "text/xml, application/xml");
            //request.AddUrlSegment("id", "" + shareId);
            ApplyUrlSegment(request, "id", "" + shareId);
            request.AddHeader("OCS-APIREQUEST", "true");

            var response = rest.ExecuteAsync<OcsResponseResult>(request).Result;
            OcsDeserializationTools.CheckOcsResponseStatus(response);
        }

        /// <summary>
        /// Declines a remote share.
        /// </summary>
        /// <returns><c>true</c>, if remote share was declined, <c>false</c> otherwise</returns>
        /// <param name="shareId">Share identifier</param>
        public void DeclineRemoteShare(int shareId)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceShare, "remote_shares") + "/{id}", Method.Delete);
            request.AddHeader("Accept", "text/xml, application/xml");
            //request.AddUrlSegment("id", "" + shareId);
            ApplyUrlSegment(request, "id", "" + shareId);
            request.AddHeader("OCS-APIREQUEST", "true");

            var response = rest.ExecuteAsync<OcsResponseResult>(request).Result;
            OcsDeserializationTools.CheckOcsResponseStatus(response);
        }
        #endregion

        #region Shares
        /// <summary>
        /// Unshares a file or directory
        /// </summary>
        /// <returns><c>true</c>, if share was deleted, <c>false</c> otherwise</returns>
        /// <param name="shareId">Share identifier</param>
        public void DeleteShare(int shareId)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceShare, "shares") + "/{id}", Method.Delete);
            request.AddHeader("Accept", "text/xml, application/xml");
            //request.AddUrlSegment("id", "" + shareId);
            ApplyUrlSegment(request, "id", "" + shareId);
            request.AddHeader("OCS-APIREQUEST", "true");

            var response = rest.ExecuteAsync<OcsResponseResult>(request).Result;
            OcsDeserializationTools.CheckOcsResponseStatus(response);
        }

        /// <summary>
        /// Updates a given share. NOTE: Only one of the update parameters can be specified at once
        /// </summary>
        /// <returns><c>true</c>, if share was updated, <c>false</c> otherwise</returns>
        /// <param name="shareId">Share identifier</param>
        /// <param name="perms">(optional) update permissions</param>
        /// <param name="password">(optional) updated password for public link Share</param>
        /// <param name="public_upload">(optional) If set to <c>true</c> enables public upload for public shares</param>
        [Obsolete("Use overloaded method instead"), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public void UpdateShare(int shareId, OcsPermission perms, string password, OcsBoolParam public_upload)
        {
            UpdateShare(shareId, perms, public_upload, (string)null, (DateTime?)null, password, (string)null);
        }

        /// <summary>
        /// Updates a given share. NOTE: Only one of the update parameters can be specified at once
        /// </summary>
        /// <returns><c>true</c>, if share was updated, <c>false</c> otherwise</returns>
        /// <param name="shareId">Share identifier</param>
        /// <param name="perms">(optional) update permissions</param>
        /// <param name="password">(optional) updated password for public link Share</param>
        /// <param name="public_upload">(optional) If set to <c>true</c> enables public upload for public shares</param>
        /// <param name="expireDate">(optional) An updated expiration date for the share (use DateTime.Min to reset an existing expireDate)</param>
        /// <param name="note">(optional) A note for the share recipient</param>
        /// <param name="name">(optional) A display name for a share</param>
        public Share UpdateShare(int shareId, OcsPermission perms = OcsPermission.None, OcsBoolParam public_upload = OcsBoolParam.None, string name = null, DateTime? expireDate = null, string password = null, string note = null)
        {
            //if (perms == OcsPermission.None) throw new ArgumentOutOfRangeException(nameof(perms));
            if (Convert.ToInt32(perms) == 0) throw new ArgumentOutOfRangeException(nameof(perms));
            if (Convert.ToInt32(perms) < Convert.ToInt32(OcsPermission.None)) throw new ArgumentOutOfRangeException(nameof(perms));
            if (Convert.ToInt32(perms) > Convert.ToInt32(OcsPermission.All)) throw new ArgumentOutOfRangeException(nameof(perms));
            //if ((perms == Convert.ToInt32(OcsPermission.None)) && (password == null) && (public_upload == OcsBoolParam.None))
            //    return false;
            if (shareId == 0) throw new ArgumentNullException(nameof(shareId));
            if (perms == OcsPermission.None && public_upload == OcsBoolParam.None && expireDate.HasValue == false && password == null && note == null)
                throw new ArgumentException("At least one parameter must be specified for update");

            var request = new RestRequest(GetOcsPath(ocsServiceShare, "shares") + "/{id}", Method.Put);
            request.AddHeader("Accept", "text/xml, application/xml");
            //request.AddUrlSegment("id", "" + shareId);
            ApplyUrlSegment(request, "id", "" + shareId);
            request.AddHeader("OCS-APIREQUEST", "true");

            if (perms != OcsPermission.None)
                request.AddParameter("permissions", Convert.ToInt32(perms).ToString());
            if (password != null)
                request.AddParameter("password", password);
            if (public_upload == OcsBoolParam.True)
                request.AddParameter("publicUpload", "true");
            else if (public_upload == OcsBoolParam.False)
                request.AddParameter("publicUpload", "false");
            if (expireDate.HasValue)
                if (expireDate.Value.Equals(DateTime.MinValue))
                    request.AddParameter("expireDate", "");
                else
                    request.AddParameter("expireDate", expireDate.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            if (note != null)
                request.AddParameter("note", note);
            if (name != null)
            {
                request.AddParameter("name", name);
                request.AddParameter("label", name);
            }

            var response = rest.ExecuteAsync<OcsResponseResult>(request).Result;
            OcsDeserializationTools.CheckOcsResponseStatus(response);

            List<Share> result = OcsDeserializationTools.GetShareItem(response.Content);
            if (result.Count == 0)
                throw new KeyNotFoundException("Share " + shareId + " doesn't exist");
            else if (result.Count == 1)
                return result[0];
            else
                throw new Exception("Share ID exists " + result.Count + " times, 1 time expected");
        }

        /// <summary>
        /// Share a file/folder with a user/group or as public link
        /// </summary>
        /// <param name="path">Path to the file/folder which should be shared</param>
        /// <param name="shareType">0 = user; 1 = group; 3 = public link; 4 = email; 6 = federated cloud share; 7 = circle; 10 = Talk conversation</param>
        /// <param name="shareWith">user / group id / email address / circleID / conversation name with which the file should be shared</param>
        /// <param name="perms">1 = read; 2 = update; 4 = create; 8 = delete; 16 = share; 31 = all (default: 31, for public shares: 1)</param>
        /// <param name="public_upload">Allow public upload to a public shared folder</param>
        /// <param name="password">Password to protect public link Share with</param>
        /// <param name="expireDate">An expire date for public link shares. This argument expects a well formatted date string, e.g. ‘YYYY-MM-DD’</param>
        /// <param name="name">A display name for a share (only available for PublicShare aka share with link)</param>
        /// <param name="note">Adds a note for the share recipient (only available for PublicShare aka share with link)</param>
        /// <returns>Share data containing the share ID (int) of the newly created share</returns>
        public Share CreateShare(string path, OcsShareType shareType, string shareWith, OcsPermission perms, OcsBoolParam public_upload, string password, DateTime? expireDate, string name, string note)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentOutOfRangeException(nameof(path));
            if (shareType != OcsShareType.Link && string.IsNullOrEmpty(shareWith)) throw new ArgumentNullException(nameof(shareWith));
            if ((Convert.ToInt32(perms) <= 0) || (Convert.ToInt32(perms) > Convert.ToInt32(OcsPermission.All))) throw new ArgumentOutOfRangeException(nameof(perms));

            var request = new RestRequest(GetOcsPath(ocsServiceShare, "shares"), Method.Post);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            request.AddParameter("shareType", Convert.ToInt32(shareType));
            request.AddParameter("path", path);
            if (!String.IsNullOrEmpty(shareWith))
                request.AddParameter("shareWith", shareWith);
            if (!String.IsNullOrEmpty(name))
            {
                request.AddParameter("name", name.ToString());
                request.AddParameter("label", name.ToString());
            }
            request.AddParameter("permissions", Convert.ToInt32(perms).ToString());
            if (password != null)
                request.AddParameter("password", password);
            if (public_upload == OcsBoolParam.True)
                request.AddParameter("publicUpload", "true");
            else if (public_upload == OcsBoolParam.False)
                request.AddParameter("publicUpload", "false");
            if (expireDate.HasValue) 
                request.AddParameter("expireDate", expireDate.Value.ToString("yyyy-MM-dd HH:mm:ss"));
            if (note != null)
                request.AddParameter("note", note);

            var response = rest.ExecuteAsync(request).Result;

            OcsDeserializationTools.CheckOcsStatus(response);

            List<Share> result = OcsDeserializationTools.GetShareItem(response.Content);
            if (result.Count == 0)
                throw new Exception("Failed to create share");
            else if (result.Count == 1)
            {
                return result[0];
            }
            else
                throw new Exception("Share ID exists " + result.Count + " times, 1 time expected");

            /*
            XElement data = GetOcsResponseData(response);
            var node = data.Element(XName.Get("share_type"));
            if (node == null) throw new Ocs.Exceptions.OcsResponseException("Result data not in expected format", 0, "", response.StatusCode);
            Core.OcsShareType foundShareType = (Core.OcsShareType)Convert.ToInt32(node.Value);

            Share share;
            switch (foundShareType)
            {
                case OcsShareType.Group:
                    share = new GroupShare(foundShareType, data);
                    break;
                case OcsShareType.User:
                    share = new UserShare(foundShareType, data);
                    break;
                case OcsShareType.Remote:
                    share = new RemoteShare(foundShareType, data);
                    break;
                case OcsShareType.Link:
                    share = new PublicShare(foundShareType, data);
                    break;
                case OcsShareType.EMail:
                case OcsShareType.Circle:
                case OcsShareType.TalkConversation:
                    share = new Share(foundShareType, data); //ToDo: add support for more specific return share types, maybe required to transport additional valuable information/properties
                    break;
                default:
                    throw new NotImplementedException("Unexpected share type returned: " + foundShareType.ToString());
            }
            return share;
            */
        }

        /// <summary>
        /// Shares a remote file with link
        /// </summary>
        /// <returns>instance of PublicShare with the share info</returns>
        /// <param name="path">path to the remote file to share</param>
        /// <param name="perms">(optional) permission of the shared object</param>
        /// <param name="password">(optional) sets a password</param>
        /// <param name="public_upload">(optional) allows users to upload files or folders (only allowed for targetting folders, forbidden for files)</param>
        [Obsolete("Use CreateShareWithLink instead"), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public PublicShare ShareWithLink(string path, OcsPermission perms, string password = null, OcsBoolParam public_upload = OcsBoolParam.None)
        {
            return CreateShareWithLink(path, perms, public_upload, "", (DateTime?)null, password);
        }

        /// <summary>
        /// Shares a remote file with link
        /// </summary>
        /// <returns>instance of PublicShare with the share info</returns>
        /// <param name="path">path to the remote file to share</param>
        /// <param name="perms">(optional) permission of the shared object</param>
        /// <param name="password">(optional) sets a password</param>
        /// <param name="public_upload">(optional) allows users to upload files or folders (only allowed for targetting folders, forbidden for files)</param>
        public PublicShare CreateShareWithLink(string path, OcsPermission perms, OcsBoolParam public_upload, string name, DateTime? expiration, string password)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentOutOfRangeException(nameof(path));
            if ((Convert.ToInt32(perms) <= 0) || (Convert.ToInt32(perms) > Convert.ToInt32(OcsPermission.All))) throw new ArgumentOutOfRangeException(nameof(perms));

            return (PublicShare)this.CreateShare(path, OcsShareType.Link, null, perms, public_upload, password, expiration, name, (string)null);

            /*
            var request = new RestRequest(GetOcsPath(ocsServiceShare, "shares"), Method.Post);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            request.AddParameter("shareType", Convert.ToInt32(OcsShareType.Link));
            request.AddParameter("path", path);

            //if (perms != Convert.ToInt32(OcsPermission.None))
            request.AddParameter("name", name.ToString());
            request.AddParameter("permissions", Convert.ToInt32(perms).ToString());
            if (password != null)
                request.AddParameter("password", password);
            if (public_upload == OcsBoolParam.True)
                request.AddParameter("publicUpload", "true");
            else if (public_upload == OcsBoolParam.False)
                request.AddParameter("publicUpload", "false");
            if (expiration.HasValue) request.AddParameter("expireDate", expiration.Value.ToString("yyyy-MM-dd HH:mm:ss"));

            var response = rest.ExecuteAsync(request).Result;

            CheckOcsStatus(response);

            XElement data = GetOcsResponseData(response);
            var node = data.Element(XName.Get("share_type"));
            if (node == null) throw new Ocs.Exceptions.OcsResponseException("Result data not in expected format", 0, "", response.StatusCode);
            Core.OcsShareType shareType = (Core.OcsShareType)Convert.ToInt32(node.Value);

            PublicShare share;
            switch (shareType)
            {
                case OcsShareType.Link:
                    share = new PublicShare(shareType, data);
                    break;
                default:
                    throw new NotImplementedException("Unexpected share type returned: " + shareType.ToString());
            }
            return share;  
            */
        }

        /// <summary>
        /// Shares a remote file with specified user
        /// </summary>
        /// <returns>instance of UserShare with the share info</returns>
        /// <param name="path">path to the remote file to share</param>
        /// <param name="username">name of the user whom we want to share a file/folder</param>
        /// <param name="perms">permissions of the shared object</param>
        /// <param name="remoteUser">Remote user</param>
        [Obsolete("Use CreateShareWithUser instead"), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public UserShare ShareWithUser(string path, string username, OcsPermission perms, OcsBoolParam remoteUser = OcsBoolParam.None)
        {
            return ShareWithUser(path, username, perms, (DateTime?)null, remoteUser);
        }

        /// <summary>
        /// Shares a remote file with specified user
        /// </summary>
        /// <returns>instance of UserShare with the share info</returns>
        /// <param name="path">path to the remote file to share</param>
        /// <param name="username">name of the user whom we want to share a file/folder</param>
        /// <param name="perms">permissions of the shared object</param>
        /// <param name="expiration">An optional expiration date</param>
        public UserShare CreateShareWithUser(string path, string username, OcsPermission perms, DateTime? expiration)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentOutOfRangeException(nameof(path));
            if (String.IsNullOrEmpty(username)) throw new ArgumentOutOfRangeException(nameof(username));
            if ((Convert.ToInt32(perms) <= 0) || (Convert.ToInt32(perms) > Convert.ToInt32(OcsPermission.All))) throw new ArgumentOutOfRangeException(nameof(perms));

            return (UserShare)this.CreateShare(path, OcsShareType.User, username, perms, OcsBoolParam.None, (string)null, expiration, (string)null, (string)null);
        }

        /// <summary>
        /// Shares a remote file with specified remote user
        /// </summary>
        /// <returns>instance of UserShare with the share info</returns>
        /// <param name="path">path to the remote file to share</param>
        /// <param name="username">name of the user whom we want to share a file/folder</param>
        /// <param name="perms">permissions of the shared object</param>
        /// <param name="expiration">An optional expiration date</param>
        public RemoteShare CreateShareWithRemoteUser(string path, string username, OcsPermission perms, DateTime? expiration)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentOutOfRangeException(nameof(path));
            if (String.IsNullOrEmpty(username)) throw new ArgumentOutOfRangeException(nameof(username));
            if ((Convert.ToInt32(perms) <= 0) || (Convert.ToInt32(perms) > Convert.ToInt32(OcsPermission.All))) throw new ArgumentOutOfRangeException(nameof(perms));

            return (RemoteShare)this.CreateShare(path, OcsShareType.Remote, username, perms, OcsBoolParam.None, (string)null, expiration, (string)null, (string)null);
        }

        /// <summary>
        /// Shares a remote file with specified user
        /// </summary>
        /// <returns>instance of UserShare with the share info</returns>
        /// <param name="path">path to the remote file to share</param>
        /// <param name="username">name of the user whom we want to share a file/folder</param>
        /// <param name="perms">permissions of the shared object</param>
        /// <param name="remoteUser">Remote user</param>
        [Obsolete("Use CreateShareWithUser or CreateShareWithRemoteUser instead"), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public UserShare ShareWithUser(string path, string username, OcsPermission perms, DateTime? expiration, OcsBoolParam remoteUser)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentOutOfRangeException(nameof(path));
            if (String.IsNullOrEmpty(username)) throw new ArgumentOutOfRangeException(nameof(username));
            if ((Convert.ToInt32(perms) <= 0) || (Convert.ToInt32(perms) > Convert.ToInt32(OcsPermission.All))) throw new ArgumentOutOfRangeException(nameof(perms));

            if (remoteUser == OcsBoolParam.True)
                return (RemoteShare)this.CreateShare(path, OcsShareType.Remote, username, perms, OcsBoolParam.None, (string)null, expiration, (string)null, (string)null);
            else
                return (UserShare)this.CreateShare(path, OcsShareType.User, username, perms, OcsBoolParam.None, (string)null, expiration, (string)null, (string)null);
            /*
            var request = new RestRequest(GetOcsPath(ocsServiceShare, "shares"), Method.Post);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            if (remoteUser == OcsBoolParam.True)
                request.AddParameter("shareType", Convert.ToInt32(OcsShareType.Remote));
            else
                request.AddParameter("shareType", Convert.ToInt32(OcsShareType.User));
            request.AddParameter("path", path);
            request.AddParameter("permissions", Convert.ToInt32(perms).ToString());
            request.AddParameter("shareWith", username);
            if (expiration.HasValue) request.AddParameter("expireDate", expiration.Value.ToString("yyyy-MM-dd HH:mm:ss"));

            var response = rest.ExecuteAsync(request).Result;

            CheckOcsStatus(response);

            XElement data = GetOcsResponseData(response);
            var node = data.Element(XName.Get("share_type"));
            if (node == null) throw new Ocs.Exceptions.OcsResponseException("Result data not in expected format", 0, "", response.StatusCode);
            Core.OcsShareType shareType = (Core.OcsShareType)Convert.ToInt32(node.Value);

            UserShare share;
            switch (shareType)
            {
                case OcsShareType.User:
                    share = new UserShare(shareType, data);
                    break;
                case OcsShareType.Remote:
                    share = new RemoteShare(shareType, data);
                    break;
                default:
                    throw new NotImplementedException("Unexpected share type returned: " + shareType.ToString());
            }
            return share;
            */
        }
        /// <summary>
        /// Shares a remote file with specified group
        /// </summary>
        /// <returns>instance of GroupShare with the share info</returns>
        /// <param name="path">path to the remote file to share</param>
        /// <param name="groupName">name of the group whom we want to share a file/folder</param>
        /// <param name="perms">permissions of the shared object</param>
        [Obsolete("Use CreateShareWithGroup instead"), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public GroupShare ShareWithGroup(string path, string groupName, OcsPermission perms)
        {
            return CreateShareWithGroup(path, groupName, perms, (DateTime?)null);
        }

        /// <summary>
        /// Shares a remote file with specified group
        /// </summary>
        /// <returns>instance of GroupShare with the share info</returns>
        /// <param name="path">path to the remote file to share</param>
        /// <param name="groupName">name of the group whom we want to share a file/folder</param>
        /// <param name="perms">permissions of the shared object</param>
        /// <param name="expiration">An optional expiration date</param>
        public GroupShare CreateShareWithGroup(string path, string groupName, OcsPermission perms, DateTime? expiration)
        {
            if (String.IsNullOrEmpty(path)) throw new ArgumentOutOfRangeException(nameof(path));
            if (String.IsNullOrEmpty(groupName)) throw new ArgumentOutOfRangeException(nameof(groupName));
            if (perms == OcsPermission.None) throw new ArgumentOutOfRangeException(nameof(perms));
            if (Convert.ToInt32(perms) == 0) throw new ArgumentOutOfRangeException(nameof(perms));
            if (Convert.ToInt32(perms) < Convert.ToInt32(OcsPermission.None)) throw new ArgumentOutOfRangeException(nameof(perms));
            if (Convert.ToInt32(perms) > Convert.ToInt32(OcsPermission.All)) throw new ArgumentOutOfRangeException(nameof(perms));

            return (GroupShare)this.CreateShare(path, OcsShareType.Group, groupName, perms, OcsBoolParam.None, (string)null, expiration, (string)null, (string)null);

            /*
            var request = new RestRequest(GetOcsPath(ocsServiceShare, "shares"), Method.Post);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            request.AddParameter("shareType", Convert.ToInt32(OcsShareType.Group));
            request.AddParameter("path", path);
            request.AddParameter("permissions", Convert.ToInt32(perms).ToString());
            request.AddParameter("shareWith", groupName);
            if (expiration.HasValue) request.AddParameter("expireDate", expiration.Value.ToString("yyyy-MM-dd HH:mm:ss"));

            var response = rest.ExecuteAsync(request).Result;

            CheckOcsStatus(response);

            XElement data = GetOcsResponseData(response);
            var node = data.Element(XName.Get("share_type"));
            if (node == null) throw new Ocs.Exceptions.OcsResponseException("Result data not in expected format", 0, "", response.StatusCode);
            Core.OcsShareType shareType = (Core.OcsShareType)Convert.ToInt32(node.Value);

            GroupShare share;
            switch (shareType)
            {
                case OcsShareType.Group:
                    share = new GroupShare(shareType, data);
                    break;
                default:
                    throw new NotImplementedException("Unexpected share type returned: " + shareType.ToString());
            }
            return share;
            */
        }

        private XElement GetOcsResponseData(RestResponse ocsResponse)
        {
            XDocument xdoc = XDocument.Parse(ocsResponse.Content);
            XElement data = xdoc.Element(XName.Get("ocs")).Element(XName.Get("data"));
            return data;
        }

        /// <summary>
        /// Checks whether a path is already shared
        /// </summary>
        /// <returns><c>true</c> if this instance is shared the specified path; otherwise, <c>false</c></returns>
        /// <param name="path">path to the share to be checked</param>
        public bool IsShared(string path)
        {
            var result = GetShares(path);
            return result.Count > 0;
        }

        /// <summary>
        /// Get a share
        /// </summary>
        /// <param name="shareId">The share ID</param>
        public Share GetShare(int shareId)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceShare, "shares/" + shareId.ToString()), Method.Get);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            var response = rest.ExecuteAsync(request).Result;

            OcsDeserializationTools.CheckOcsStatus(response);

            List<Share> result = OcsDeserializationTools.GetShareList(response.Content);
            if (result.Count == 0)
                throw new KeyNotFoundException("Share " + shareId + " doesn't exist");
            else if (result.Count == 1)
                return result[0];
            else
                throw new Exception("Share ID exists " + result.Count + " times, 1 time expected");
        }

        /// <summary>
        /// Gets all shares for the current user when <c>path</c> is not set, otherwise it gets shares for the specific file or folder
        /// </summary>
        /// <returns>array of shares or empty array if the operation failed</returns>
        /// <param name="path">(optional) path to the share to be checked</param>
        /// <param name="reshares">(optional) returns not only the shares from	the current user but all shares from the given file</param>
        /// <param name="subfiles">(optional) returns all shares within	a folder, given that path defines a folder</param>
        public List<Share> GetShares(string path = "", OcsBoolParam reshares = OcsBoolParam.None, OcsBoolParam subfiles = OcsBoolParam.None)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceShare, "shares"), Method.Get);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            if ((path != null) && (!path.Equals("")))
                request.AddQueryParameter("path", path);
            if (reshares == OcsBoolParam.True)
                request.AddQueryParameter("reshares", "true");
            else if (reshares == OcsBoolParam.False)
                request.AddQueryParameter("reshares", "false");
            if (subfiles == OcsBoolParam.True)
                request.AddQueryParameter("subfiles", "true");
            else if (subfiles == OcsBoolParam.False)
                request.AddQueryParameter("subfiles", "false");

            var response = rest.ExecuteAsync(request).Result;

            OcsDeserializationTools.CheckOcsStatus(response);

            return OcsDeserializationTools.GetShareList(response.Content);
        }
        #endregion

        #region Users
        /// <summary>
        /// Create a new user with an initial password via provisioning API.
        /// </summary>
        /// <returns><c>true</c>, if user was created, <c>false</c> otherwise</returns>
        /// <param name="username">name of user to be created</param>
        /// <param name="initialPassword">password for user being created</param> 
        public void CreateUser(string username, string initialPassword)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users"), Method.Post);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            request.AddParameter("userid", username);
            request.AddParameter("password", initialPassword);

            var response = rest.ExecuteAsync<OcsResponseResult>(request).Result;
            OcsDeserializationTools.CheckOcsResponseStatus(response);
        }

        /// <summary>
        /// Deletes a user via provisioning API.
        /// </summary>
        /// <returns><c>true</c>, if user was deleted, <c>false</c> otherwise</returns>
        /// <param name="username">name of user to be deleted</param>
        public void DeleteUser(string username)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users") + "/{userid}", Method.Delete);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);

            var response = rest.ExecuteAsync<OcsResponseResult>(request).Result;
            OcsDeserializationTools.CheckOcsResponseStatus(response);
        }

        /// <summary>
        /// Checks a user via provisioning API.
        /// </summary>
        /// <returns><c>true</c>, if exists was usered, <c>false</c> otherwise</returns>
        /// <param name="username">name of user to be checked</param>
        public bool UserExists(string username)
        {
            var result = SearchUsers(username);
            return result.Contains(username);
        }

        /// <summary>
        /// Searches for users via provisioning API.
        /// </summary>
        /// <returns>list of users</returns>
        /// <param name="username">name of user to be searched for</param>
        public List<string> SearchUsers()
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users"), Method.Get);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            var response = rest.ExecuteAsync(request).Result;

            OcsDeserializationTools.CheckOcsStatus(response);

            return OcsDeserializationTools.GetStringListFromDataElements(response.Content);
        }

        /// <summary>
        /// Searches for users via provisioning API.
        /// </summary>
        /// <returns>list of users</returns>
        /// <param name="username">name of user to be searched for</param>
        public List<string> SearchUsers(string username)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users") + "?search={userid}", Method.Get);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);

            var response = rest.ExecuteAsync(request).Result;

            OcsDeserializationTools.CheckOcsStatus(response);

            return OcsDeserializationTools.GetStringListFromDataElements(response.Content);
        }

        /// <summary>
        /// Searches for users via sharee API
        /// </summary>
        /// <returns>list of users</returns>
        /// <param name="search">name of user to be searched for (empty values might lead to result with 0 or all users depending on remote server software version (NextCloud vs. OwnCloud)</param>
        public List<Sharee> Sharees(string search, bool lookupGlobally, string itemType)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceShare, "sharees"), Method.Get);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            request.AddParameter("search", search);
            request.AddParameter("lookup", lookupGlobally ? "true" : "false");
            request.AddParameter("itemType", itemType);

            var response = rest.ExecuteAsync(request).Result;

            OcsDeserializationTools.CheckOcsStatus(response);

            return OcsDeserializationTools.GetShareesFromResponse(response.Content);
        }

        /// <summary>
        /// Searches for users via provisioning API.
        /// </summary>
        /// <returns>list of users</returns>
        /// <param name="username">name of user to be searched for</param>
        public List<Sharee> ShareesRecommended(string itemType)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceShare, "sharees_recommended"), Method.Get);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            request.AddParameter("itemType", itemType);

            var response = rest.ExecuteAsync(request).Result;

            OcsDeserializationTools.CheckOcsStatus(response);

            return OcsDeserializationTools.GetShareesFromResponse(response.Content);
        }

        private void ApplyUrlSegment(RestRequest request, string key, string value)
        {
            //origin code: syntax doesn't work any more (won't be replaced effectively + throws ArgumentNullException on empty string values)
            //request.AddUrlSegment(key, value);

            //as of RestSharp v107, above code line doesn't work, therefore a WORKAROUND below
            request.Resource = request.Resource.Replace("{" + key + "}", System.Net.WebUtility.UrlEncode(value));
            var ParametersRequiringUpdate = new List<RestSharp.Parameter>();
            foreach (RestSharp.Parameter p in request.Parameters)
            {
                switch (p.Type)
                {
                    case ParameterType.UrlSegment:
                    case ParameterType.QueryString:
                        if (p.Value != null && p.Value.GetType() == typeof(string))
                            ParametersRequiringUpdate.Add(p);
                        break;
                }
            }
            foreach (RestSharp.Parameter p in ParametersRequiringUpdate)
            {
                string paramValue = (string)(p.Value);
                string newValue = paramValue.Replace("{" + key + "}", System.Net.WebUtility.UrlEncode(value));
                request.AddOrUpdateParameter(p.Name, newValue, p.Type, p.Encode);
            }
        }

        /// <summary>
        /// Gets the user's attributes.
        /// </summary>
        /// <returns>The user attributes</returns>
        /// <param name="username">Username</param>
        public User GetUserAttributes(string username)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users") + "/{userid}", Method.Get);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);

            var response = rest.ExecuteAsync(request).Result;

            OcsDeserializationTools.CheckOcsStatus(response);

            return OcsDeserializationTools.GetUser(response.Content);
        }

        /// <summary>
        /// Sets a user attribute. See https://doc.owncloud.com/server/7.0EE/admin_manual/configuration_auth_backends/user_provisioning_api.html#users-edituser for reference.
        /// </summary>
        /// <returns><c>true</c>, if user attribute was set, <c>false</c> otherwise</returns>
        /// <param name="username">name of user to modify</param>
        /// <param name="key">key of the attribute to set</param>
        /// <param name="value">value to set</param>
        public void SetUserAttribute(string username, OCSUserAttributeKey key, string value)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users") + "/{userid}", Method.Put);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);
            request.AddParameter("key", OCSUserAttributeKeyName[Convert.ToInt32(key)]);
            request.AddParameter("value", value);

            var response = rest.ExecuteAsync<OcsResponseResult>(request).Result;
            OcsDeserializationTools.CheckOcsStatus(response);
        }

        /// <summary>
        /// Adds a user to a group.
        /// </summary>
        /// <returns><c>true</c>, if user was added to group, <c>false</c> otherwise</returns>
        /// <param name="username">name of user to be added</param>
        /// <param name="groupName">name of group user is to be added to</param>
        public void AddUserToGroup(string username, string groupName)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users") + "/{userid}/groups", Method.Post);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);
            request.AddParameter("groupid", groupName);

            var response = rest.ExecuteAsync<OcsResponseResult>(request).Result;
            OcsDeserializationTools.CheckOcsResponseStatus(response);
        }

        /// <summary>
        /// Get a list of groups associated to a user.
        /// </summary>
        /// <returns>list of groups</returns>
        /// <param name="username">name of user to list groups</param>
        public List<string> GetUserGroups(string username)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users") + "/{userid}/groups", Method.Get);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);

            var response = rest.ExecuteAsync(request).Result;

            OcsDeserializationTools.CheckOcsStatus(response);

            return OcsDeserializationTools.GetStringListFromDataElements(response.Content);
        }

        /// <summary>
        /// Check if a user is in a group.
        /// </summary>
        /// <returns><c>true</c>, if user is in group, <c>false</c> otherwise</returns>
        /// <param name="username">name of user</param>
        /// <param name="groupName">name of group</param>
        public bool IsUserInGroup(string username, string groupName)
        {
            var groups = GetUserGroups(username);
            return groups.Contains(groupName);
        }

        /// <summary>
        /// Removes a user from a group.
        /// </summary>
        /// <returns><c>true</c>, if user was removed from group, <c>false</c> otherwise</returns>
        /// <param name="username">name of user to be removed</param>
        /// <param name="groupName">name of group user is to be removed from</param>
        public void RemoveUserFromGroup(string username, string groupName)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users") + "/{userid}/groups", Method.Delete);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);
            request.AddParameter("groupid", groupName);

            var response = rest.ExecuteAsync<OcsResponseResult>(request).Result;
            OcsDeserializationTools.CheckOcsResponseStatus(response);
        }

        /// <summary>
        /// Adds a user to a subadmin group.
        /// </summary>
        /// <returns><c>true</c>, if user was added to sub admin group, <c>false</c> otherwise</returns>
        /// <param name="username">name of user to be added to subadmin group</param>
        /// <param name="groupName">name of subadmin group</param>
        public void AddUserToSubAdminGroup(string username, string groupName)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users") + "/{userid}/subadmins", Method.Post);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);
            request.AddParameter("groupid", groupName);

            var response = rest.ExecuteAsync<OcsResponseResult>(request).Result;
            OcsDeserializationTools.CheckOcsResponseStatus(response);
        }

        /// <summary>
        /// Get a list of subadmin groups associated to a user.
        /// </summary>
        /// <returns>list of subadmin groups</returns>
        /// <param name="username">name of user</param>
        public List<string> GetUserSubAdminGroups(string username)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users") + "/{userid}/subadmins", Method.Get);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);

            var response = rest.ExecuteAsync(request).Result;

            try
            {
                OcsDeserializationTools.CheckOcsStatus(response);
            }
            catch (OcsResponseException ocserr)
            {
                if (ocserr.OcsStatusCode.Equals("102")) // empty response results in a OCS 102 Error
                    return new List<string>();
            }

            return OcsDeserializationTools.GetStringListFromDataElements(response.Content);
        }

        /// <summary>
        /// Check if a user is in a subadmin group.
        /// </summary>
        /// <returns><c>true</c>, if user is in sub admin group, <c>false</c> otherwise</returns>
        /// <param name="username">name of user</param>
        /// <param name="groupNname">name of subadmin group</param>
        public bool IsUserInSubAdminGroup(string username, string groupNname)
        {
            var groups = GetUserSubAdminGroups(username);
            return groups.Contains(groupNname);
        }

        /// <summary>
        /// Removes the user from sub admin group.
        /// </summary>
        /// <returns><c>true</c>, if user from sub admin group was removed, <c>false</c> otherwise</returns>
        /// <param name="username">Username</param>
        /// <param name="groupName">Group name</param>
        public void RemoveUserFromSubAdminGroup(string username, string groupName)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users") + "/{userid}/subadmins", Method.Delete);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);
            request.AddParameter("groupid", groupName);

            var response = rest.ExecuteAsync<OcsResponseResult>(request).Result;
            OcsDeserializationTools.CheckOcsResponseStatus(response);
        }
        #endregion

        #region Groups
        /// <summary>
        /// Create a new group via provisioning API.
        /// </summary>
        /// <returns><c>true</c>, if group was created, <c>false</c> otherwise</returns>
        /// <param name="groupName">name of group to be created</param>
        public void CreateGroup(string groupName)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "groups"), Method.Post);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            request.AddParameter("groupid", groupName);

            var response = rest.ExecuteAsync<OcsResponseResult>(request).Result;
            OcsDeserializationTools.CheckOcsResponseStatus(response);
        }

        /// <summary>
        /// Deletes the group.
        /// </summary>
        /// <returns><c>true</c>, if group was deleted, <c>false</c> otherwise</returns>
        /// <param name="groupName">Group name</param>
        public void DeleteGroup(string groupName)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "groups") + "/{groupid}", Method.Delete);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("groupid", groupName);
            ApplyUrlSegment(request, "groupid", groupName);

            var response = rest.ExecuteAsync<OcsResponseResult>(request).Result;
            OcsDeserializationTools.CheckOcsResponseStatus(response);
        }

        /// <summary>
        /// Checks a group via provisioning API.
        /// </summary>
        /// <returns><c>true</c>, if group exists, <c>false</c> otherwise</returns>
        /// <param name="groupName">name of group to be checked</param>
        public bool GroupExists(string groupName)
        {
            var results = SearchGroups(groupName);
            return results.Contains(groupName);
        }

        /// <summary>
		/// Searches for groups via provisioning API.
		/// </summary>
		/// <returns>list of groups</returns>
		/// <param name="name">name of group to be searched for</param>
		public List<string> SearchGroups(string name)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "groups") + "?search={groupid}", Method.Get);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("groupid", name);
            ApplyUrlSegment(request, "groupid", name);

            var response = rest.ExecuteAsync(request).Result;

            OcsDeserializationTools.CheckOcsStatus(response);

            return OcsDeserializationTools.GetStringListFromDataElements(response.Content);
        }
        #endregion

        #region Config
        /// <summary>
        /// Returns ownCloud config information
        /// </summary>
        /// <returns>The config</returns>
        public Config GetConfig()
        {
            var request = new RestRequest(GetOcsPath("", "config"), Method.Get);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            var response = rest.ExecuteAsync(request).Result;

            OcsDeserializationTools.CheckOcsStatus(response);

            Config cfg = new Config();
            cfg.Contact = OcsDeserializationTools.GetStringValueFromData(response.Content, "contact");
            cfg.Host = OcsDeserializationTools.GetStringValueFromData(response.Content, "host");
            cfg.Ssl = OcsDeserializationTools.GetStringValueFromData(response.Content, "ssl");
            cfg.Version = OcsDeserializationTools.GetStringValueFromData(response.Content, "version");
            cfg.Website = OcsDeserializationTools.GetStringValueFromData(response.Content, "website");

            return cfg;
        }
        #endregion

        #region Application attributes
        /// <summary>
        /// Returns an application attribute
        /// </summary>
        /// <returns>App Attribute List</returns>
        /// <param name="app">application id</param>
        /// <param name="key">attribute key or None to retrieve all values for the given application</param>
        [Obsolete("Use GetAppAttribute instead"), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public List<AppAttribute> GetAttribute(string app = "", string key = "")
        {
            return this.GetAppAttribute(app, key);
        }

        /// <summary>
        /// Returns an application attribute
        /// </summary>
        /// <returns>App Attribute List</returns>
        /// <param name="app">application id</param>
        /// <param name="key">attribute key or None to retrieve all values for the given application</param>
        public List<AppAttribute> GetAppAttribute(string app = "", string key = "")
        {
            var path = "getattribute";
            if (!app.Equals(""))
            {
                path += "/" + app;
                if (!key.Equals(""))
                    path += "/" + WebUtility.UrlEncode(key);
            }

            var request = new RestRequest(GetOcsPath(ocsServiceData, path), Method.Get);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            var response = rest.ExecuteAsync(request).Result;

            OcsDeserializationTools.CheckOcsStatus(response);

            return OcsDeserializationTools.GetAttributeList(response.Content);
        }

        /// <summary>
        /// Sets an application attribute
        /// </summary>
        /// <returns><c>true</c>, if attribute was set, <c>false</c> otherwise</returns>
        /// <param name="app">application id</param>
        /// <param name="key">key of the attribute to set</param>
        /// <param name="value">value to set</param>
        [Obsolete("Use SetAppAttribute instead"), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public void SetAttribute(string app, string key, string value)
        {
            this.SetAppAttribute(app, key, value);
        }

        /// <summary>
        /// Sets an application attribute
        /// </summary>
        /// <returns><c>true</c>, if attribute was set, <c>false</c> otherwise</returns>
        /// <param name="app">application id</param>
        /// <param name="key">key of the attribute to set</param>
        /// <param name="value">value to set</param>
        public void SetAppAttribute(string app, string key, string value)
        {
            var path = "setattribute" + "/" + app + "/" + WebUtility.UrlEncode(key);

            var request = new RestRequest(GetOcsPath(ocsServiceData, path), Method.Post);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");
            request.AddParameter("value", value);

            var response = rest.ExecuteAsync<OcsResponseResult>(request).Result;
            OcsDeserializationTools.CheckOcsResponseStatus(response);
        }

        /// <summary>
        /// Deletes an application attribute
        /// </summary>
        /// <returns><c>true</c>, if attribute was deleted, <c>false</c> otherwise</returns>
        /// <param name="app">application id</param>
        /// <param name="key">key of the attribute to delete</param>
        [Obsolete("Use DeleteAppAttribute instead"), System.ComponentModel.EditorBrowsable(System.ComponentModel.EditorBrowsableState.Never)]
        public void DeleteAttribute(string app, string key)
        {
            this.DeleteAppAttribute(app, key);
        }

        /// <summary>
        /// Deletes an application attribute
        /// </summary>
        /// <returns><c>true</c>, if attribute was deleted, <c>false</c> otherwise</returns>
        /// <param name="app">application id</param>
        /// <param name="key">key of the attribute to delete</param>
        public void DeleteAppAttribute(string app, string key)
        {
            var path = "deleteattribute" + "/" + app + "/" + WebUtility.UrlEncode(key);

            var request = new RestRequest(GetOcsPath(ocsServiceData, path), Method.Delete);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            var response = rest.ExecuteAsync<OcsResponseResult>(request).Result;
            OcsDeserializationTools.CheckOcsResponseStatus(response);
        }
        #endregion

        #region Apps
        /// <summary>
        /// List all enabled apps through the provisioning api
        /// </summary>
        /// <returns>a list of apps and their enabled state</returns>
        public List<string> GetApps()
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "apps"), Method.Get);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            var response = rest.ExecuteAsync(request).Result;

            OcsDeserializationTools.CheckOcsStatus(response);

            return OcsDeserializationTools.GetStringListFromDataElements(response.Content);
        }

        /// <summary>
        /// Gets information about the specified app
        /// </summary>
        /// <returns>App information</returns>
        /// <param name="appName">App name</param>
        public AppInfo GetApp(string appName)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "apps") + "/{appid}", Method.Get);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");
            //request.AddUrlSegment("appid", appName);
            ApplyUrlSegment(request, "appid", appName);

            var response = rest.ExecuteAsync(request).Result;

            OcsDeserializationTools.CheckOcsStatus(response);

            return OcsDeserializationTools.GetAppInfo(response.Content);
        }

        /// <summary>
        /// Enable an app through provisioning_api
        /// </summary>
        /// <returns><c>true</c>, if app was enabled, <c>false</c> otherwise</returns>
        /// <param name="appName">Name of app to be enabled</param>
        public void EnableApp(string appName)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "apps") + "/{appid}", Method.Post);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("appid", appName);
            ApplyUrlSegment(request, "appid", appName);

            var response = rest.ExecuteAsync<OcsResponseResult>(request).Result;
            OcsDeserializationTools.CheckOcsResponseStatus(response);
        }

        /// <summary>
        /// Disable an app through provisioning_api
        /// </summary>
        /// <returns><c>true</c>, if app was disabled, <c>false</c> otherwise</returns>
        /// <param name="appName">Name of app to be disabled</param>
        public void DisableApp(string appName)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "apps") + "/{appid}", Method.Delete);
            request.AddHeader("Accept", "text/xml, application/xml");
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("appid", appName);
            ApplyUrlSegment(request, "appid", appName);

            var response = rest.ExecuteAsync<OcsResponseResult>(request).Result;
            OcsDeserializationTools.CheckOcsResponseStatus(response);
        }
        #endregion
        #endregion

        #region Url Handling
        /// <summary>
        /// Gets the full URI
        /// </summary>
        /// <returns>The URI</returns>
        /// <param name="path">remote Path</param>
        private Uri GetUri(string path)
        {
            return new Uri(this.url + path);
        }

        /// <summary>
        /// Gets the DAV request URI
        /// </summary>
        /// <returns>The DAV URI</returns>
        /// <param name="path">remote Path</param>
        private Uri GetDavUri(string path)
        {
            return new Uri(this.url + "/" + davpath + path);
        }

        /// <summary>
        /// Gets the remote path for OCS API
        /// </summary>
        /// <returns>The ocs path</returns>
        /// <param name="service">Service</param>
        /// <param name="action">Action</param>
        private string GetOcsPath(string service, string action)
        {
            var slash = (!service.Equals("")) ? "/" : "";
            return service + slash + action;
        }
        #endregion

        #region Helpers
        /// <summary>
        /// Provides the string values for the OCSUserAttributeKey enum
        /// </summary>
        public static string[] OCSUserAttributeKeyName = new string[] {
        "display",
        "quota",
        "password",
        "email"
        };
        #endregion
    }
}