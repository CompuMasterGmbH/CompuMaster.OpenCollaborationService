﻿using System;
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

namespace CompuMaster.Ocs
{
    /// <summary>
    /// ownCloud OCS and DAV access client
    /// </summary>
    public class Client
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
        /// Base URL (e.g. http://server.mydomain/, http://server.mydomain/owncloud)
        /// </summary>
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
        /// 
        /// </summary>
        public readonly char DirectorySeparatorChar = '/';
        #endregion

        #region Constructor
        /// <summary>
        /// Initializes a new instance of the <see cref="CompuMaster.Ocs.Client"/> class.
        /// </summary>
        /// <param name="url">ownCloud instance URL</param>
        /// <param name="user_id">User identifier</param>
        /// <param name="password">Password</param>
        public Client(string url, string user_id, string password)
        {
            // In case URL has a trailing slash remove it
            if ((url != null) && (url.EndsWith("/")))
                url = url.TrimEnd(new[] { '/' });

            // Store ownCloud base URL
            this.url = url;

            // RestSharp initialisation
            this.rest = new RestClient(new Uri(url + "/" + ocspath));
            // Set the base path as the OCS API root
            //this.rest.BaseUrl = new Uri(url + "/" + ocspath);
            // Configure RestSharp for BasicAuth
            this.rest.Authenticator = new HttpBasicAuthenticator(user_id, password);
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
        public string BaseUrl { get => this.url; }
        public string AuthorizedUserID { get => this.user_id; }
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
                return System.Net.WebUtility.UrlDecode( davPath.Substring(cutOffPath.Length));
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
            return dav.GetRawFile(GetDavUri(path)).Result.Stream;
        }

        /// <summary>
        /// Upload the specified file to the specified path
        /// </summary>
        /// <param name="path">remote Path</param>
        /// <param name="data">File contents</param>
        /// <param name="contentType">File content type</param>
        /// <returns><c>true</c>, if upload successful, <c>false</c> otherwise</returns>
        public bool Upload(string path, Stream data, string contentType)
        {
            return dav.PutFile(GetDavUri(path), data, contentType).Result.IsSuccessful;
        }

        /// <summary>
        /// Upload the specified file to the specified path
        /// </summary>
        /// <param name="path">remote Path</param>
        /// <param name="data">File contents</param>
        /// <returns><c>true</c>, if upload successful, <c>false</c> otherwise</returns>
        public bool Upload(string path, Stream data)
        {
            return dav.PutFile(GetDavUri(path), data).Result.IsSuccessful;
        }

        /// <summary>
        /// Checks if the specified remote path exists
        /// </summary>
        /// <param name="path">remote Path</param>
        /// <returns><c>true</c>, if remote path exists, <c>false</c> otherwise</returns>
        public bool Exists(string path)
        {
            var result = this.dav.Propfind(GetDavUri(path)).Result;

            return result.Resources.Count != 0;
        }

        /// <summary>
        /// Creates a new directory at remote path
        /// </summary>
        /// <returns><c>true</c>, if directory was created, <c>false</c> otherwise</returns>
        /// <param name="path">remote Path</param>
        public bool CreateDirectory(string path)
        {
            return dav.Mkcol(GetDavUri(path)).Result.IsSuccessful;
        }

        /// <summary>
        /// Delete resource at the specified remote path
        /// </summary>
        /// <param name="path">remote Path</param>
        /// <returns><c>true</c>, if resource was deleted, <c>false</c> otherwise</returns>
        public bool Delete(string path)
        {
            return dav.Delete(GetDavUri(path)).Result.IsSuccessful;
        }

        /// <summary>
        /// Copy the specified source to destination
        /// </summary>
        /// <param name="source">Source resoure path</param>
        /// <param name="destination">Destination resource path</param>
        /// <returns><c>true</c>, if resource was copied, <c>false</c> otherwise</returns>
        public bool Copy(string source, string destination)
        {
            return dav.Copy(GetDavUri(source), GetDavUri(destination)).Result.IsSuccessful;
        }

        /// <summary>
        /// Move the specified source and destination
        /// </summary>
        /// <param name="source">Source resource path</param>
        /// <param name="destination">Destination resource path</param>
        /// <returns><c>true</c>, if resource was moved, <c>false</c> otherwise</returns>
        public bool Move(string source, string destination)
        {
            return dav.Move(GetDavUri(source), GetDavUri(destination)).Result.IsSuccessful;
        }

        /// <summary>
        /// Downloads a remote directory as zip (might work only for OwnCloud/Nextcloud due to specialized behaviour)
        /// </summary>
        /// <returns>The directory as zip</returns>
        /// <param name="path">path to the remote directory to download</param>
        public Stream DownloadDirectoryAsZip(string path)
        {
            var uri = GetUri("index.php/apps/files/ajax/download.php?dir=" + WebUtility.UrlEncode(path));
            return dav.GetRawFile(uri).Result.Stream;
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
            request.AddHeader("OCS-APIREQUEST", "true");
            var response = rest.ExecuteAsync(request).Result;

            CheckOcsStatus(response);

            var content = response.Content;
            // TODO: Parse response
            return content;
        }

        /// <summary>
        /// Accepts a remote share
        /// </summary>
        /// <returns><c>true</c>, if remote share was accepted, <c>false</c> otherwise</returns>
        /// <param name="shareId">Share identifier</param>
        public bool AcceptRemoteShare(int shareId)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceShare, "remote_shares") + "/{id}", Method.Post);
            //request.AddUrlSegment("id", "" + shareId);
            ApplyUrlSegment(request, "id", "" + shareId);
            request.AddHeader("OCS-APIREQUEST", "true");

            var response = rest.ExecuteAsync<OCS>(request).Result;
            if (response.Data != null)
            {
                if (response.Data.Meta.StatusCode == 100)
                    return true;
                else
                    throw new OCSResponseError(response.Data.Meta.Message, response.Data.Meta.StatusCode + "", response.StatusCode);
            }

            return false;
        }

        /// <summary>
        /// Declines a remote share.
        /// </summary>
        /// <returns><c>true</c>, if remote share was declined, <c>false</c> otherwise</returns>
        /// <param name="shareId">Share identifier</param>
        public bool DeclineRemoteShare(int shareId)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceShare, "remote_shares") + "/{id}", Method.Delete);
            //request.AddUrlSegment("id", "" + shareId);
            ApplyUrlSegment(request, "id", "" + shareId);
            request.AddHeader("OCS-APIREQUEST", "true");

            var response = rest.ExecuteAsync<OCS>(request).Result;
            if (response.Data != null)
            {
                if (response.Data.Meta.StatusCode == 100)
                    return true;
                else
                    throw new OCSResponseError(response.Data.Meta.Message, response.Data.Meta.StatusCode + "", response.StatusCode);
            }

            return false;
        }
        #endregion

        #region SHares
        /// <summary>
        /// Unshares a file or directory
        /// </summary>
        /// <returns><c>true</c>, if share was deleted, <c>false</c> otherwise</returns>
        /// <param name="shareId">Share identifier</param>
        public bool DeleteShare(int shareId)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceShare, "shares") + "/{id}", Method.Delete);
            //request.AddUrlSegment("id", "" + shareId);
            ApplyUrlSegment(request, "id", "" + shareId);
            request.AddHeader("OCS-APIREQUEST", "true");

            var response = rest.ExecuteAsync<OCS>(request).Result;
            if (response.Data != null)
            {
                if (response.Data.Meta.StatusCode == 100)
                    return true;
                else
                    throw new OCSResponseError(response.Data.Meta.Message, response.Data.Meta.StatusCode + "", response.StatusCode);
            }

            return false;
        }

        /// <summary>
        /// Updates a given share. NOTE: Only one of the update parameters can be specified at once
        /// </summary>
        /// <returns><c>true</c>, if share was updated, <c>false</c> otherwise</returns>
        /// <param name="shareId">Share identifier</param>
        /// <param name="perms">(optional) update permissions</param>
        /// <param name="password">(optional) updated password for public link Share</param>
        /// <param name="public_upload">(optional) If set to <c>true</c> enables public upload for public shares</param>
        public bool UpdateShare(int shareId, int perms = -1, string password = null, OcsBoolParam public_upload = OcsBoolParam.None)
        {
            if ((perms == Convert.ToInt32(OcsPermission.None)) && (password == null) && (public_upload == OcsBoolParam.None))
                return false;

            var request = new RestRequest(GetOcsPath(ocsServiceShare, "shares") + "/{id}", Method.Put);
            //request.AddUrlSegment("id", "" + shareId);
            ApplyUrlSegment(request, "id", "" + shareId);
            request.AddHeader("OCS-APIREQUEST", "true");

            if (perms != Convert.ToInt32(OcsPermission.None))
                request.AddQueryParameter("permissions", Convert.ToInt32(perms) + "");
            if (password != null)
                request.AddQueryParameter("password", password);
            if (public_upload == OcsBoolParam.True)
                request.AddQueryParameter("publicUpload", "true");
            else if (public_upload == OcsBoolParam.False)
                request.AddQueryParameter("publicUpload", "false");

            var response = rest.ExecuteAsync<OCS>(request).Result;
            if (response.Data != null)
            {
                if (response.Data.Meta.StatusCode == 100)
                    return true;
                else
                    throw new OCSResponseError(response.Data.Meta.Message, response.Data.Meta.StatusCode + "", response.StatusCode);
            }

            return false;
        }

        /// <summary>
        /// Shares a remote file with link
        /// </summary>
        /// <returns>instance of PublicShare with the share info</returns>
        /// <param name="path">path to the remote file to share</param>
        /// <param name="perms">(optional) permission of the shared object</param>
        /// <param name="password">(optional) sets a password</param>
        /// <param name="public_upload">(optional) allows users to upload files or folders</param>
        public PublicShare ShareWithLink(string path, int perms = -1, string password = null, OcsBoolParam public_upload = OcsBoolParam.None)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceShare, "shares"), Method.Post);
            request.AddHeader("OCS-APIREQUEST", "true");

            request.AddParameter("shareType", Convert.ToInt32(OcsShareType.Link));
            request.AddParameter("path", path);

            if (perms != Convert.ToInt32(OcsPermission.None))
                request.AddParameter("permissions", Convert.ToInt32(perms) + "");
            if (password != null)
                request.AddParameter("password", password);
            if (public_upload == OcsBoolParam.True)
                request.AddParameter("publicUpload", "true");
            else if (public_upload == OcsBoolParam.False)
                request.AddParameter("publicUpload", "false");

            var response = rest.ExecuteAsync(request).Result;

            CheckOcsStatus(response);

            PublicShare share = new PublicShare();
            share.ShareId = Convert.ToInt32(GetFromData(response.Content, "id"));
            share.Url = GetFromData(response.Content, "url");
            share.Token = GetFromData(response.Content, "token");
            share.TargetPath = path;
            share.Perms = (perms > -1) ? perms : Convert.ToInt32(OcsPermission.Read);

            return share;
        }

        /// <summary>
        /// Shares a remote file with specified user
        /// </summary>
        /// <returns>instance of UserShare with the share info</returns>
        /// <param name="path">path to the remote file to share</param>
        /// <param name="username">name of the user whom we want to share a file/folder</param>
        /// <param name="perms">permissions of the shared object</param>
        /// <param name="remoteUser">Remote user</param>
        public UserShare ShareWithUser(string path, string username, int perms = -1, OcsBoolParam remoteUser = OcsBoolParam.None)
        {
            if ((perms == -1) || (perms > Convert.ToInt32(OcsPermission.All)) || (username == null) || (username.Equals("")))
                return null;

            var request = new RestRequest(GetOcsPath(ocsServiceShare, "shares"), Method.Post);
            request.AddHeader("OCS-APIREQUEST", "true");

            if (remoteUser == OcsBoolParam.True)
                request.AddParameter("shareType", Convert.ToInt32(OcsShareType.Remote));
            else
                request.AddParameter("shareType", Convert.ToInt32(OcsShareType.User));
            request.AddParameter("path", path);
            if (perms != Convert.ToInt32(OcsPermission.None))
                request.AddParameter("permissions", perms + "");
            else
                request.AddParameter("permissions", Convert.ToInt32(OcsPermission.Read) + "");
            request.AddParameter("shareWith", username);

            var response = rest.ExecuteAsync(request).Result;

            CheckOcsStatus(response);

            var share = new UserShare();
            share.ShareId = Convert.ToInt32(GetFromData(response.Content, "id"));
            share.TargetPath = path;
            share.Perms = perms;
            share.SharedWith = username;

            return share;
        }

        /// <summary>
        /// Shares a remote file with specified group
        /// </summary>
        /// <returns>instance of GroupShare with the share info</returns>
        /// <param name="path">path to the remote file to share</param>
        /// <param name="groupName">name of the group whom we want to share a file/folder</param>
        /// <param name="perms">permissions of the shared object</param>
        public GroupShare ShareWithGroup(string path, string groupName, int perms = -1)
        {
            if ((perms == -1) || (perms > Convert.ToInt32(OcsPermission.All)) || (groupName == null) || (groupName.Equals("")))
                return null;

            var request = new RestRequest(GetOcsPath(ocsServiceShare, "shares"), Method.Post);
            request.AddHeader("OCS-APIREQUEST", "true");

            request.AddParameter("shareType", Convert.ToInt32(OcsShareType.Group));
            request.AddParameter("path", path);
            if (perms != Convert.ToInt32(OcsPermission.None))
                request.AddParameter("permissions", perms + "");
            else
                request.AddParameter("permissions", Convert.ToInt32(OcsPermission.Read) + "");
            request.AddParameter("shareWith", groupName);

            var response = rest.ExecuteAsync(request).Result;

            CheckOcsStatus(response);

            var share = new GroupShare();
            share.ShareId = Convert.ToInt32(GetFromData(response.Content, "id"));
            share.TargetPath = path;
            share.Perms = perms;
            share.SharedWith = groupName;

            return share;
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
        /// Gets all shares for the current user when <c>path</c> is not set, otherwise it gets shares for the specific file or folder
        /// </summary>
        /// <returns>array of shares or empty array if the operation failed</returns>
        /// <param name="path">(optional) path to the share to be checked</param>
        /// <param name="reshares">(optional) returns not only the shares from	the current user but all shares from the given file</param>
        /// <param name="subfiles">(optional) returns all shares within	a folder, given that path defines a folder</param>
        public List<Share> GetShares(string path, OcsBoolParam reshares = OcsBoolParam.None, OcsBoolParam subfiles = OcsBoolParam.None)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceShare, "shares"), Method.Get);
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

            CheckOcsStatus(response);

            return GetShareList(response.Content);
        }
        #endregion

        #region Users
        /// <summary>
        /// Create a new user with an initial password via provisioning API.
        /// </summary>
        /// <returns><c>true</c>, if user was created, <c>false</c> otherwise</returns>
        /// <param name="username">name of user to be created</param>
        /// <param name="initialPassword">password for user being created</param> 
        public bool CreateUser(string username, string initialPassword)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users"), Method.Post);
            request.AddHeader("OCS-APIREQUEST", "true");

            request.AddParameter("userid", username);
            request.AddParameter("password", initialPassword);

            var response = rest.ExecuteAsync<OCS>(request).Result;
            if (response.Data != null)
            {
                if (response.Data.Meta.StatusCode == 100)
                    return true;
                else
                    throw new OCSResponseError(response.Data.Meta.Message, response.Data.Meta.StatusCode + "", response.StatusCode);
            }

            return false;
        }

        /// <summary>
        /// Deletes a user via provisioning API.
        /// </summary>
        /// <returns><c>true</c>, if user was deleted, <c>false</c> otherwise</returns>
        /// <param name="username">name of user to be deleted</param>
        public bool DeleteUser(string username)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users") + "/{userid}", Method.Delete);
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);

            var response = rest.ExecuteAsync<OCS>(request).Result;
            if (response.Data != null)
            {
                if (response.Data.Meta.StatusCode == 100)
                    return true;
                else
                    throw new OCSResponseError(response.Data.Meta.Message, response.Data.Meta.StatusCode + "", response.StatusCode);
            }

            return false;
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
            request.AddHeader("OCS-APIREQUEST", "true");

            var response = rest.ExecuteAsync(request).Result;

            CheckOcsStatus(response);

            return GetDataElements(response.Content);
        }

        /// <summary>
        /// Searches for users via provisioning API.
        /// </summary>
        /// <returns>list of users</returns>
        /// <param name="username">name of user to be searched for</param>
        public List<string> SearchUsers(string username)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users") + "?search={userid}", Method.Get);
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);

            var response = rest.ExecuteAsync(request).Result;

            CheckOcsStatus(response);

            return GetDataElements(response.Content);
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
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);

            var response = rest.ExecuteAsync(request).Result;

            CheckOcsStatus(response);

            return GetUser(response.Content);
        }

        /// <summary>
        /// Sets a user attribute. See https://doc.owncloud.com/server/7.0EE/admin_manual/configuration_auth_backends/user_provisioning_api.html#users-edituser for reference.
        /// </summary>
        /// <returns><c>true</c>, if user attribute was set, <c>false</c> otherwise</returns>
        /// <param name="username">name of user to modify</param>
        /// <param name="key">key of the attribute to set</param>
        /// <param name="value">value to set</param>
        public bool SetUserAttribute(string username, OCSUserAttributeKey key, string value)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users") + "/{userid}", Method.Put);
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);
            request.AddParameter("key", OCSUserAttributeKeyName[Convert.ToInt32(key)]);
            request.AddParameter("value", value);

            var response = rest.ExecuteAsync<OCS>(request).Result;
            CheckOcsStatus(response);
            return true;
        }

        /// <summary>
        /// Adds a user to a group.
        /// </summary>
        /// <returns><c>true</c>, if user was added to group, <c>false</c> otherwise</returns>
        /// <param name="username">name of user to be added</param>
        /// <param name="groupName">name of group user is to be added to</param>
        public bool AddUserToGroup(string username, string groupName)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users") + "/{userid}/groups", Method.Post);
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);
            request.AddParameter("groupid", groupName);

            var response = rest.ExecuteAsync<OCS>(request).Result;
            if (response.Data != null)
            {
                if (response.Data.Meta.StatusCode == 100)
                    return true;
                else
                    throw new OCSResponseError(response.Data.Meta.Message, response.Data.Meta.StatusCode + "", response.StatusCode);
            }

            return false;
        }

        /// <summary>
        /// Get a list of groups associated to a user.
        /// </summary>
        /// <returns>list of groups</returns>
        /// <param name="username">name of user to list groups</param>
        public List<string> GetUserGroups(string username)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users") + "/{userid}/groups", Method.Get);
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);

            var response = rest.ExecuteAsync(request).Result;

            CheckOcsStatus(response);

            return GetDataElements(response.Content);
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
        public bool RemoveUserFromGroup(string username, string groupName)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users") + "/{userid}/groups", Method.Delete);
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);
            request.AddParameter("groupid", groupName);

            var response = rest.ExecuteAsync<OCS>(request).Result;
            if (response.Data != null)
            {
                if (response.Data.Meta.StatusCode == 100)
                    return true;
                else
                    throw new OCSResponseError(response.Data.Meta.Message, response.Data.Meta.StatusCode + "", response.StatusCode);
            }

            return false;
        }

        /// <summary>
        /// Adds a user to a subadmin group.
        /// </summary>
        /// <returns><c>true</c>, if user was added to sub admin group, <c>false</c> otherwise</returns>
        /// <param name="username">name of user to be added to subadmin group</param>
        /// <param name="groupName">name of subadmin group</param>
        public bool AddUserToSubAdminGroup(string username, string groupName)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users") + "/{userid}/subadmins", Method.Post);
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);
            request.AddParameter("groupid", groupName);

            var response = rest.ExecuteAsync<OCS>(request).Result;
            if (response.Data != null)
            {
                if (response.Data.Meta.StatusCode == 100)
                    return true;
                else
                    throw new OCSResponseError(response.Data.Meta.Message, response.Data.Meta.StatusCode + "", response.StatusCode);
            }

            return false;
        }

        /// <summary>
        /// Get a list of subadmin groups associated to a user.
        /// </summary>
        /// <returns>list of subadmin groups</returns>
        /// <param name="username">name of user</param>
        public List<string> GetUserSubAdminGroups(string username)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users") + "/{userid}/subadmins", Method.Get);
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);

            var response = rest.ExecuteAsync(request).Result;

            try
            {
                CheckOcsStatus(response);
            }
            catch (OCSResponseError ocserr)
            {
                if (ocserr.OcsStatusCode.Equals("102")) // empty response results in a OCS 102 Error
                    return new List<string>();
            }

            return GetDataElements(response.Content);
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
        public bool RemoveUserFromSubAdminGroup(string username, string groupName)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "users") + "/{userid}/subadmins", Method.Delete);
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("userid", username);
            ApplyUrlSegment(request, "userid", username);
            request.AddParameter("groupid", groupName);

            var response = rest.ExecuteAsync<OCS>(request).Result;
            if (response.Data != null)
            {
                if (response.Data.Meta.StatusCode == 100)
                    return true;
                else
                    throw new OCSResponseError(response.Data.Meta.Message, response.Data.Meta.StatusCode + "", response.StatusCode);
            }

            return false;
        }
        #endregion

        #region Groups
        /// <summary>
        /// Create a new group via provisioning API.
        /// </summary>
        /// <returns><c>true</c>, if group was created, <c>false</c> otherwise</returns>
        /// <param name="groupName">name of group to be created</param>
        public bool CreateGroup(string groupName)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "groups"), Method.Post);
            request.AddHeader("OCS-APIREQUEST", "true");

            request.AddParameter("groupid", groupName);

            var response = rest.ExecuteAsync<OCS>(request).Result;
            if (response.Data != null)
            {
                if (response.Data.Meta.StatusCode == 100)
                    return true;
                else
                    throw new OCSResponseError(response.Data.Meta.Message, response.Data.Meta.StatusCode + "", response.StatusCode);
            }

            return false;
        }

        /// <summary>
        /// Deletes the group.
        /// </summary>
        /// <returns><c>true</c>, if group was deleted, <c>false</c> otherwise</returns>
        /// <param name="groupName">Group name</param>
        public bool DeleteGroup(string groupName)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "groups") + "/{groupid}", Method.Delete);
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("groupid", groupName);
            ApplyUrlSegment(request, "groupid", groupName);

            var response = rest.ExecuteAsync<OCS>(request).Result;
            if (response.Data != null)
            {
                if (response.Data.Meta.StatusCode == 100)
                    return true;
                else
                    throw new OCSResponseError(response.Data.Meta.Message, response.Data.Meta.StatusCode + "", response.StatusCode);
            }

            return false;
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
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("groupid", name);
            ApplyUrlSegment(request, "groupid", name);

            var response = rest.ExecuteAsync(request).Result;

            CheckOcsStatus(response);

            return GetDataElements(response.Content);
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
            request.AddHeader("OCS-APIREQUEST", "true");

            var response = rest.ExecuteAsync(request).Result;

            CheckOcsStatus(response);

            Config cfg = new Config();
            cfg.Contact = GetFromData(response.Content, "contact");
            cfg.Host = GetFromData(response.Content, "host");
            cfg.Ssl = GetFromData(response.Content, "ssl");
            cfg.Version = GetFromData(response.Content, "version");
            cfg.Website = GetFromData(response.Content, "website");

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
        public List<AppAttribute> GetAttribute(string app = "", string key = "")
        {
            var path = "getattribute";
            if (!app.Equals(""))
            {
                path += "/" + app;
                if (!key.Equals(""))
                    path += "/" + WebUtility.UrlEncode(key);
            }

            var request = new RestRequest(GetOcsPath(ocsServiceData, path), Method.Get);
            request.AddHeader("OCS-APIREQUEST", "true");

            var response = rest.ExecuteAsync(request).Result;

            CheckOcsStatus(response);

            return GetAttributeList(response.Content);
        }

        /// <summary>
        /// Sets an application attribute
        /// </summary>
        /// <returns><c>true</c>, if attribute was set, <c>false</c> otherwise</returns>
        /// <param name="app">application id</param>
        /// <param name="key">key of the attribute to set</param>
        /// <param name="value">value to set</param>
        public bool SetAttribute(string app, string key, string value)
        {
            var path = "setattribute" + "/" + app + "/" + WebUtility.UrlEncode(key);

            var request = new RestRequest(GetOcsPath(ocsServiceData, path), Method.Post);
            request.AddHeader("OCS-APIREQUEST", "true");
            request.AddParameter("value", value);

            var response = rest.ExecuteAsync<OCS>(request).Result;
            if (response.Data != null)
            {
                if (response.Data.Meta.StatusCode == 100)
                    return true;
                else
                    throw new OCSResponseError(response.Data.Meta.Message, response.Data.Meta.StatusCode + "", response.StatusCode);
            }

            return false;
        }

        /// <summary>
        /// Deletes an application attribute
        /// </summary>
        /// <returns><c>true</c>, if attribute was deleted, <c>false</c> otherwise</returns>
        /// <param name="app">application id</param>
        /// <param name="key">key of the attribute to delete</param>
        public bool DeleteAttribute(string app, string key)
        {
            var path = "deleteattribute" + "/" + app + "/" + WebUtility.UrlEncode(key);

            var request = new RestRequest(GetOcsPath(ocsServiceData, path), Method.Delete);
            request.AddHeader("OCS-APIREQUEST", "true");

            var response = rest.ExecuteAsync<OCS>(request).Result;
            if (response.Data != null)
            {
                if (response.Data.Meta.StatusCode == 100)
                    return true;
                else
                    throw new OCSResponseError(response.Data.Meta.Message, response.Data.Meta.StatusCode + "", response.StatusCode);
            }

            return false;
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
            request.AddHeader("OCS-APIREQUEST", "true");

            var response = rest.ExecuteAsync(request).Result;

            CheckOcsStatus(response);

            return GetDataElements(response.Content);
        }

        /// <summary>
        /// Gets information about the specified app
        /// </summary>
        /// <returns>App information</returns>
        /// <param name="appName">App name</param>
        public AppInfo GetApp(string appName)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "apps") + "/{appid}", Method.Get);
            request.AddHeader("OCS-APIREQUEST", "true");
            //request.AddUrlSegment("appid", appName);
            ApplyUrlSegment(request, "appid", appName);

            var response = rest.ExecuteAsync(request).Result;

            CheckOcsStatus(response);

            return GetAppInfo(response.Content);
        }

        /// <summary>
        /// Enable an app through provisioning_api
        /// </summary>
        /// <returns><c>true</c>, if app was enabled, <c>false</c> otherwise</returns>
        /// <param name="appName">Name of app to be enabled</param>
        public bool EnableApp(string appName)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "apps") + "/{appid}", Method.Post);
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("appid", appName);
            ApplyUrlSegment(request, "appid", appName);

            var response = rest.ExecuteAsync<OCS>(request).Result;
            if (response.Data != null)
            {
                if (response.Data.Meta.StatusCode == 100)
                    return true;
                else
                    throw new OCSResponseError(response.Data.Meta.Message, response.Data.Meta.StatusCode + "", response.StatusCode);
            }

            return false;
        }

        /// <summary>
        /// Disable an app through provisioning_api
        /// </summary>
        /// <returns><c>true</c>, if app was disabled, <c>false</c> otherwise</returns>
        /// <param name="appName">Name of app to be disabled</param>
        public bool DisableApp(string appName)
        {
            var request = new RestRequest(GetOcsPath(ocsServiceCloud, "apps") + "/{appid}", Method.Delete);
            request.AddHeader("OCS-APIREQUEST", "true");

            //request.AddUrlSegment("appid", appName);
            ApplyUrlSegment(request, "appid", appName);

            var response = rest.ExecuteAsync<OCS>(request).Result;
            if (response.Data != null)
            {
                if (response.Data.Meta.StatusCode == 100)
                    return true;
                else
                    throw new OCSResponseError(response.Data.Meta.Message, response.Data.Meta.StatusCode + "", response.StatusCode);
            }

            return false;
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

        #region OCS Response parsing
        /// <summary>
        /// Get element value from OCS Meta
        /// </summary>
        /// <returns>Element value</returns>
        /// <param name="response">XML OCS response</param>
        /// <param name="elementName">XML Element name</param>
        private string GetFromMeta(string response, string elementName)
        {
            XDocument xdoc = XDocument.Parse(response);

            foreach (XElement data in xdoc.Descendants(XName.Get("meta")))
            {
                var node = data.Element(XName.Get(elementName));
                if (node != null)
                    return node.Value;
            }

            return null;
        }

        /// <summary>
        /// Get element value from OCS Data
        /// </summary>
        /// <returns>Element value</returns>
        /// <param name="response">XML OCS response</param>
        /// <param name="elementName">XML Element name</param>
        private string GetFromData(string response, string elementName)
        {
            XDocument xdoc = XDocument.Parse(response);

            foreach (XElement data in xdoc.Descendants(XName.Get("data")))
            {
                var node = data.Element(XName.Get(elementName));
                if (node != null)
                    return node.Value;
            }

            return null;
        }

        /// <summary>
        /// Gets the data element values
        /// </summary>
        /// <returns>The data elements</returns>
        /// <param name="response">XML OCS Response</param>
        private List<string> GetDataElements(string response)
        {
            List<string> result = new List<string>();
            XDocument xdoc = XDocument.Parse(response);

            foreach (XElement data in xdoc.Descendants(XName.Get("data")))
            {
                foreach (XElement node in data.Descendants(XName.Get("element")))
                {
                    result.Add(node.Value);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the share list from a OCS Data response
        /// </summary>
        /// <returns>The share list</returns>
        /// <param name="response">XML OCS Response</param>
        private List<Share> GetShareList(string response)
        {
            List<Share> shares = new List<Share>();
            XDocument xdoc = XDocument.Parse(response);

            foreach (XElement data in xdoc.Descendants(XName.Get("element")))
            {
                Share share = null;
                var node = data.Element(XName.Get("share_type"));
                if (node != null)
                {
                    #region Share Type
                    var shareType = Convert.ToInt32(node.Value);
                    if (shareType == Convert.ToInt32(OcsShareType.Link))
                        share = new PublicShare();
                    else if (shareType == Convert.ToInt32(OcsShareType.User))
                        share = new UserShare();
                    else if (shareType == Convert.ToInt32(OcsShareType.Group))
                        share = new GroupShare();
                    else
                        share = new Share();
                    share.AdvancedProperties = new AdvancedShareProperties();
                    #endregion

                    #region General Properties
                    node = data.Element(XName.Get("id"));
                    if (node != null)
                        share.ShareId = Convert.ToInt32(node.Value);

                    node = data.Element(XName.Get("file_target"));
                    if (node != null)
                        share.TargetPath = node.Value;

                    node = data.Element(XName.Get("permissions"));
                    if (node != null)
                        share.Perms = Convert.ToInt32(node.Value);
                    #endregion

                    #region Advanced Properties
                    node = data.Element(XName.Get("item_type"));
                    if (node != null)
                        share.AdvancedProperties.ItemType = node.Value;

                    node = data.Element(XName.Get("item_source"));
                    if (node != null)
                        share.AdvancedProperties.ItemSource = node.Value;

                    node = data.Element(XName.Get("parent"));
                    if (node != null)
                        share.AdvancedProperties.Parent = node.Value;

                    node = data.Element(XName.Get("file_source"));
                    if (node != null)
                        share.AdvancedProperties.FileSource = node.Value;

                    node = data.Element(XName.Get("stime"));
                    if (node != null)
                        share.AdvancedProperties.STime = node.Value;

                    node = data.Element(XName.Get("expiration"));
                    if (node != null)
                        share.AdvancedProperties.Expiration = node.Value;

                    node = data.Element(XName.Get("mail_send"));
                    if (node != null)
                        share.AdvancedProperties.MailSend = node.Value;

                    node = data.Element(XName.Get("uid_owner"));
                    if (node != null)
                        share.AdvancedProperties.Owner = node.Value;

                    node = data.Element(XName.Get("storage_id"));
                    if (node != null)
                        share.AdvancedProperties.StorageId = node.Value;

                    node = data.Element(XName.Get("storage"));
                    if (node != null)
                        share.AdvancedProperties.Storage = node.Value;

                    node = data.Element(XName.Get("file_parent"));
                    if (node != null)
                        share.AdvancedProperties.FileParent = node.Value;

                    node = data.Element(XName.Get("share_with_displayname"));
                    if (node != null)
                        share.AdvancedProperties.ShareWithDisplayname = node.Value;

                    node = data.Element(XName.Get("displayname_owner"));
                    if (node != null)
                        share.AdvancedProperties.DisplaynameOwner = node.Value;
                    #endregion

                    #region ShareType specific
                    if (shareType == Convert.ToInt32(OcsShareType.Link))
                    {
                        node = data.Element(XName.Get("url"));
                        if (node != null)
                            ((PublicShare)share).Url = node.Value;

                        node = data.Element(XName.Get("token"));
                        if (node != null)
                            ((PublicShare)share).Token = node.Value;
                    }
                    else if (shareType == Convert.ToInt32(OcsShareType.User))
                    {
                        node = data.Element(XName.Get("share_with"));
                        if (node != null)
                            ((UserShare)share).SharedWith = node.Value;
                    }
                    else if (shareType == Convert.ToInt32(OcsShareType.Group))
                    {
                        node = data.Element(XName.Get("share_with"));
                        if (node != null)
                            ((GroupShare)share).SharedWith = node.Value;
                    }
                    #endregion

                    shares.Add(share);
                }
            }

            return shares;
        }

        /// <summary>
        /// Checks the validity of the OCS Request. If invalid a exception is thrown
        /// </summary>
        /// <param name="response">OCS Response</param>
        private void CheckOcsStatus(RestResponse response)
        {
            if (response.Content == null || response.Content == "")
            {
                if (response.ErrorException != null)
                    throw new ResponseError("REST request failed", response.StatusCode, response.ErrorException, response.Content);
                else if (response.ErrorMessage != null)
                    throw new ResponseError(response.ErrorMessage, response.StatusCode, response.Content);
                else 
                    throw new ResponseError("Empty response content", response.StatusCode, response.Content);
            }
            else
            {
                var ocsStatus = GetFromMeta(response.Content, "statuscode");
                if (ocsStatus == null)
                    throw new ResponseError("Empty OCS status or invalid response data", response.StatusCode, response.Content);
                if (!ocsStatus.Equals("100"))
                    throw new OCSResponseError(GetFromMeta(response.Content, "message"), ocsStatus, response.StatusCode);
            }
        }

        /// <summary>
        /// Returns a list of application attributes
        /// </summary>
        /// <returns>List of application attributes</returns>
        /// <param name="response">XML OCS Response</param>
        private List<AppAttribute> GetAttributeList(string response)
        {
            List<AppAttribute> result = new List<AppAttribute>();
            XDocument xdoc = XDocument.Parse(response);

            foreach (XElement data in xdoc.Descendants(XName.Get("data")))
            {
                foreach (XElement element in data.Descendants(XName.Get("element")))
                {
                    AppAttribute attr = new AppAttribute();

                    var node = element.Element(XName.Get("app"));
                    if (node != null)
                        attr.App = node.Value;

                    node = element.Element(XName.Get("key"));
                    if (node != null)
                        attr.Key = node.Value;

                    node = element.Element(XName.Get("value"));
                    if (node != null)
                        attr.value = node.Value;

                    result.Add(attr);
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the user attributes from a OCS XML Response
        /// </summary>
        /// <returns>The user attributes</returns>
        /// <param name="response">OCS XML Response</param>
        private User GetUser(string response)
        {
            var user = new User();
            XDocument xdoc = XDocument.Parse(response);

            foreach (XElement data in xdoc.Descendants(XName.Get("data")))
            {
                var node = data.Element(XName.Get("displayname"));
                if (node != null)
                    user.DisplayName = node.Value;

                node = data.Element(XName.Get("email"));
                if (node != null)
                    user.EMail = node.Value;

                node = data.Element(XName.Get("enabled"));
                if (node != null)
                    user.Enabled = (node.Value.Equals("true") || node.Value.Equals("1")) ? true : false;

                foreach (XElement element in data.Descendants(XName.Get("quota")))
                {
                    if (element.Parent.Name == "data") //don't find grand children which might exist under the very same name
                    {
                        var quota = new Quota();

                        node = element.Element(XName.Get("free"));
                        if (node != null)
                            quota.Free = double.Parse(node.Value, CultureInfo.InvariantCulture);

                        node = element.Element(XName.Get("used"));
                        if (node != null)
                            quota.Used = double.Parse(node.Value, CultureInfo.InvariantCulture);

                        node = element.Element(XName.Get("total"));
                        if (node != null)
                            quota.Total = double.Parse(node.Value, CultureInfo.InvariantCulture);

                        node = element.Element(XName.Get("relative"));
                        if (node != null)
                            quota.Relative = double.Parse(node.Value, CultureInfo.InvariantCulture);

                        user.Quota = quota;
                    }
                }
            }

            return user;
        }

        private AppInfo GetAppInfo(string response)
        {
            AppInfo app = new AppInfo();
            XDocument xdoc = XDocument.Parse(response);

            foreach (XElement data in xdoc.Descendants(XName.Get("data")))
            {
                var node = data.Element(XName.Get("id"));
                if (node != null)
                    app.Id = node.Value;

                node = data.Element(XName.Get("name"));
                if (node != null)
                    app.DisplayName = node.Value;

                node = data.Element(XName.Get("description"));
                if (node != null)
                    app.Description = node.Value;

                node = data.Element(XName.Get("licence"));
                if (node != null)
                    app.Licence = node.Value;

                node = data.Element(XName.Get("author"));
                if (node != null)
                    app.Author = node.Value;

                node = data.Element(XName.Get("requiremin"));
                if (node != null)
                    app.RequireMin = node.Value;

                node = data.Element(XName.Get("shipped"));
                if (node != null)
                    app.Shipped = (node.Value.Equals("true")) ? true : false;

                node = data.Element(XName.Get("standalone"));
                if (node != null)
                    app.Standalone = true;
                else
                    app.Standalone = false;

                node = data.Element(XName.Get("default_enable"));
                if (node != null)
                    app.DefaultEnable = true;
                else
                    app.DefaultEnable = false;

                node = data.Element(XName.Get("types"));
                if (node != null)
                    app.Types = XmlElementsToList(node);

                node = data.Element(XName.Get("remote"));
                if (node != null)
                    app.Remote = XmlElementsToDict(node);

                node = data.Element(XName.Get("documentation"));
                if (node != null)
                    app.Documentation = XmlElementsToDict(node);

                node = data.Element(XName.Get("info"));
                if (node != null)
                    app.Info = XmlElementsToDict(node);

                node = data.Element(XName.Get("public"));
                if (node != null)
                    app.Public = XmlElementsToDict(node);
            }

            return app;
        }

        /// <summary>
        /// Returns the elements of a XML Element as a List
        /// </summary>
        /// <returns>The elements as list</returns>
        /// <param name="element">XML Element</param>
        private List<string> XmlElementsToList(XElement element)
        {
            List<string> result = new List<string>();

            foreach (XElement node in element.Descendants(XName.Get("element")))
            {
                result.Add(node.Value);
            }

            return result;
        }

        /// <summary>
        /// Returns the elements of a XML Element as a Dictionary
        /// </summary>
        /// <returns>The elements as dictionary</returns>
        /// <param name="element">XML Element</param>
        private Dictionary<string, string> XmlElementsToDict(XElement element)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            foreach (XElement node in element.Descendants())
            {
                result.Add(node.Name.ToString(), node.Value);
            }

            return result;
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

