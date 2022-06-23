using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Linq;

namespace CompuMaster.Ocs.Types
{
    /// <summary>
    /// Provides basic information of a ownCloud Share
    /// </summary>
    public class Sharee
    {
		internal Sharee(Core.OcsShareType shareType, XElement data)
		{
			XElement node;
			this.ShareType = shareType;

			node = data.Element(XName.Get("value")).Element(XName.Get("shareType"));
			if ((node != null) && !String.IsNullOrEmpty(node.Value) && (int.Parse(node.Value) != (int)shareType))
				throw new ArgumentException(nameof(shareType));

			node = data.Element(XName.Get("value")).Element(XName.Get("shareWith"));
			if (node != null)
				this.ShareWith = node.Value;

			node = data.Element(XName.Get("shareWithDisplayNameUnique"));
			if (node != null)
				this.ShareWithDisplayName = node.Value;

			node = data.Element(XName.Get("icon"));
			if (node != null)
				this.Icon = node.Value;

			node = data.Element(XName.Get("label"));
			if (node != null)
				this.Label = node.Value;

		}

		/// <summary>
		/// The shares type Id assigned by ownCloud
		/// </summary>
		public Core.OcsShareType ShareType { get; set; }
		/// <summary>
		/// The share recipient icon
		/// </summary>
		public string Icon { get; set; }
		/// <summary>
		/// The share recipient label
		/// </summary>
		public string Label { get; set; }
		/// <summary>
		/// The share recipient name
		/// </summary>
		public string ShareWith { get; set; }
		/// <summary>
		/// The share recipient display name
		/// </summary>
		public string ShareWithDisplayName { get; set; }

		/// <summary>
		/// Share summary
		/// </summary>
		/// <returns></returns>
        public override string ToString()
        {
			return "Sharee (" + ShareType.ToString() + "): " + this.Label.ToString() + " (" + this.ShareWith + "|" + this.ShareWithDisplayName + ")";
        }
    }
}
