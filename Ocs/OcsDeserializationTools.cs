using System;
using System.Collections.Generic;
using System.Net;
using System.Xml.Linq;
using RestSharp;
using CompuMaster.Ocs.Exceptions;
using CompuMaster.Ocs.Types;
using CompuMaster.Ocs.Core;
using System.Globalization;

namespace CompuMaster.Ocs
{
    /// <summary>
    /// Parsing tools for OCS response content
    /// </summary>
    internal static class OcsDeserializationTools
    {
        /// <summary>
        /// Returns a list of application attributes
        /// </summary>
        /// <returns>List of application attributes</returns>
        /// <param name="response">XML OCS Response</param>
        public static List<AppAttribute> GetAttributeList(string response)
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
        public static User GetUser(string response)
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

        public static AppInfo GetAppInfo(string response)
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
        /// Get element value from OCS Meta
        /// </summary>
        /// <returns>Element value</returns>
        /// <param name="response">XML OCS response</param>
        /// <param name="elementName">XML Element name</param>
        public static string GetFromMeta(string response, string elementName)
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
        public static string GetStringValueFromData(string response, string elementName)
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
        public static List<string> GetStringListFromDataElements(string response)
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
        /// Gets the data element values
        /// </summary>
        /// <returns>The data elements</returns>
        /// <param name="response">XML OCS Response</param>
        public static List<XElement> GetChildNodesFromData(string response)
        {
            List<XElement> result = new List<XElement>();
            XDocument xdoc = XDocument.Parse(response);

            XElement data = GetSingleChildNodeFromXElement(xdoc.Root, "data");
            return GetChildNodesFromXElement(data);
        }

        /// <summary>
        /// Gets the data element values
        /// </summary>
        /// <returns>The data elements</returns>
        /// <param name="response">XML OCS Response</param>
        public static List<XElement> GetChildNodesFromData(string response, string xname)
        {
            List<XElement> result = new List<XElement>();
            XDocument xdoc = XDocument.Parse(response);

            foreach (XElement data in xdoc.Descendants(XName.Get("data")))
            {
                foreach (XElement node in GetChildNodesFromXElement(data, xname))
                {
                    foreach (XElement enode in GetChildNodesFromXElement(node))
                    {
                        result.Add(enode);
                    }
                }
            }
            return result;
        }

        /// <summary>
        /// Gets a sub element which may appear 0 or 1 times (2 or more times throws exception)
        /// </summary>
        /// <returns>The parent element</returns>
        public static XElement GetSingleChildNodeFromXElement(XElement parent, string xname)
        {
            var result = new List<XElement>();
            foreach (XElement node in parent.Descendants(XName.Get(xname)))
            {
                if (node.Parent == parent)
                    result.Add(node);
            }
            if (result.Count > 1)
                throw new Exception("More than 1 child found");
            else if (result.Count == 1)
                return result[0];
            else
                return null;
        }

        /// <summary>
        /// Gets the sub element values
        /// </summary>
        /// <returns>The parent element</returns>
        public static List<XElement> GetChildNodesFromXElement(XElement parent, string xname)
        {
            var result = new List<XElement>();
            foreach (XElement node in parent.Descendants(XName.Get(xname)))
            {
                if (node.Parent == parent)
                    result.Add(node);
            }
            return result;
        }

        /// <summary>
        /// Gets the sub element values
        /// </summary>
        /// <returns>The parent element</returns>
        public static List<XElement> GetChildNodesFromXElement(XElement parent)
        {
            var result = new List<XElement>();
            foreach (XElement node in parent.Descendants())
            {
                if (node.Parent == parent)
                    result.Add(node);
            }
            return result;
        }

        /// <summary>
        /// Gets the data element values
        /// </summary>
        /// <returns>The data elements</returns>
        /// <param name="response">XML OCS Response</param>
        public static List<XElement> GetChildNodesFromData(string response, List<string> xnames)
        {
            List<XElement> result = new List<XElement>();
            XDocument xdoc = XDocument.Parse(response);

            foreach (XElement data in xdoc.Descendants(XName.Get("data")))
            {
                foreach (string xname in xnames)
                {
                    foreach (XElement node in GetChildNodesFromXElement(data, xname))
                    {
                        if (node.Parent == data)
                            result.Add(node);
                    }
                }
            }

            return result;
        }

        /// <summary>
        /// Gets the data element values
        /// </summary>
        /// <returns>The data elements</returns>
        /// <param name="response">XML OCS Response</param>
        public static List<string> GetStringListFromDataElements(string response, string elementName)
        {
            List<string> result = new List<string>();
            XDocument xdoc = XDocument.Parse(response);

            foreach (XElement data in xdoc.Descendants(XName.Get("data")))
            {
                foreach (XElement node in data.Descendants(XName.Get(elementName)))
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
        public static List<Share> GetShareList(string response)
        {
            List<Share> shares = new List<Share>();
            XDocument xdoc = XDocument.Parse(response);

            foreach (XElement data in xdoc.Descendants(XName.Get("element")))
            {
                shares.Add(GetShareItem(data));
            }

            return shares;
        }

        /// <summary>
        /// Gets the share list from a OCS Data response
        /// </summary>
        /// <returns>The share list</returns>
        /// <param name="response">XML OCS Response</param>
        public static List<Share> GetShareItem(string response)
        {
            List<Share> shares = new List<Share>();
            XDocument xdoc = XDocument.Parse(response);

            foreach (XElement data in xdoc.Descendants(XName.Get("data")))
            {
                shares.Add(GetShareItem(data));
            }

            return shares;
        }

        private static Share GetShareItem(XElement data)
        {
            Share share = null;
            var node = data.Element(XName.Get("share_type"));
            if (node != null)
            {
                var foundShareType = (Core.OcsShareType)Convert.ToInt32(node.Value);
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
            }
            else
                return null;
        }

        /// <summary>
        /// Checks the validity of the OCS Request. If invalid a exception is thrown
        /// </summary>
        /// <param name="response">OCS Response</param>
        public static void CheckOcsResponseStatus(RestResponse<OcsResponseResult> response)
        {
            if (response.Data != null)
            {
                if (response.Data.Meta.StatusCode == 100)
                    //successful
                    return;
                else
                    throw new OcsResponseException(response.Data.Meta.Message, response.Data.Meta.StatusCode, response.Data.Meta.Status, response.StatusCode);
            }
            else if (String.IsNullOrEmpty(response.Content))
                throw new OcsResponseException("Missing OCS response content (" + response.StatusDescription + ")", 0, null, response.StatusCode);
            else
                throw new OcsResponseException("OCS failure (invalid OCS response content))", 0, null, 0);
        }

        /// <summary>
        /// Checks the validity of the OCS Request. If invalid a exception is thrown
        /// </summary>
        /// <param name="response">OCS Response</param>
        public static void CheckOcsStatus(RestResponse response)
        {
            if (response.Content == null || response.Content == "")
            {
                if (response.ErrorException != null)
                    throw new ResponseException("REST request failed", response.StatusCode, response.ErrorException, response.Content);
                else if (response.ErrorMessage != null)
                    throw new ResponseException(response.ErrorMessage, response.StatusCode, response.Content);
                else
                    throw new ResponseException("Empty response content", response.StatusCode, response.Content);
            }
            else
            {
                var ocsStatus = GetFromMeta(response.Content, "statuscode");
                var ocsStatusText = GetFromMeta(response.Content, "status");
                if (ocsStatus == null)
                    throw new ResponseException("Empty OCS status or invalid response data", response.StatusCode, response.Content);
                if (!ocsStatus.Equals("100"))
                    throw new OcsResponseException(GetFromMeta(response.Content, "message"), int.Parse(ocsStatus), ocsStatusText, response.StatusCode);
            }
        }

 

        /// <summary>
        /// Returns the elements of a XML Element as a List
        /// </summary>
        /// <returns>The elements as list</returns>
        /// <param name="element">XML Element</param>
        public static List<string> XmlElementsToList(XElement element)
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
        public static Dictionary<string, string> XmlElementsToDict(XElement element)
        {
            Dictionary<string, string> result = new Dictionary<string, string>();

            foreach (XElement node in element.Descendants())
            {
                result.Add(node.Name.ToString(), node.Value);
            }

            return result;
        }


        /// <summary>
        /// Check WebDavResult and throw exceptions if it failed
        /// </summary>
        /// <param name="result"></param>
        /// <exception cref="Ocs.Exceptions.OcsResponseException"></exception>
        public static void CheckDavStatus(WebDav.WebDavResponse result)
        {
            if (result.IsSuccessful == false)
            {
                if (result.StatusCode != 0)
                    throw new Ocs.Exceptions.OcsResponseException(result.Description, 0, null, (HttpStatusCode)result.StatusCode);
                else
                    throw new Ocs.Exceptions.OcsResponseException(result.Description, 0, null, 0);
            }
        }

        public static List<Sharee> GetShareesFromResponse(string responseContent)
        {
            var dataChildren = OcsDeserializationTools.GetChildNodesFromData(responseContent);
            var result = new List<Sharee>();
            foreach (XElement ShareeType in dataChildren)
            {
                if (ShareeType.Name.LocalName == "exact")
                {
                    foreach (XElement ExactShareeType in OcsDeserializationTools.GetChildNodesFromXElement(ShareeType))
                    {
                        //collect all exact matches
                        foreach (XElement ShareeItem in OcsDeserializationTools.GetChildNodesFromXElement(ExactShareeType, "element"))
                        {
                            var shareType = (OcsShareType)int.Parse(OcsDeserializationTools.GetSingleChildNodeFromXElement(OcsDeserializationTools.GetSingleChildNodeFromXElement(ShareeItem, "value"), "shareType").Value);
                            result.Add(new Sharee(shareType, ShareeItem) { IsExactResult = true });
                        }
                    }
                }

                //collect all non-exact matches
                foreach (XElement ShareeItem in OcsDeserializationTools.GetChildNodesFromXElement(ShareeType, "element"))
                {
                    var shareType = (OcsShareType)int.Parse(OcsDeserializationTools.GetSingleChildNodeFromXElement(OcsDeserializationTools.GetSingleChildNodeFromXElement(ShareeItem, "value"), "shareType").Value);
                    result.Add(new Sharee(shareType, ShareeItem));
                }
            }
            return result;
        }
    }
}
