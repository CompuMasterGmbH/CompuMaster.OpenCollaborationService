using System;
using System.Xml.Linq;

namespace CompuMaster.Ocs.Types
{
    /// <summary>
    /// Provides information of a public ownCloud share.
    /// </summary>
	public class PublicShare : Share
	{
        internal PublicShare(Core.OcsShareType shareType, XElement data) : base(shareType, data)
        {
            XElement node;
            node = data.Element(XName.Get("url"));
            if (node != null)
                this.Url = node.Value;

            node = data.Element(XName.Get("token"));
            if (node != null)
                this.Token = node.Value;
        }

        /// <summary>
        /// Remote access URL
        /// </summary>
        public string Url { get; set; }
        /// <summary>
        /// The shares token
        /// </summary>
		public string Token { get; set; }
    }
}

