using System;
using System.Xml.Linq;

namespace CompuMaster.Ocs.Types
{
    /// <summary>
    /// Provides information of a group ownCloud share.
    /// </summary>
	public class GroupShare : Share
	{
        internal GroupShare(Core.OcsShareType shareType, XElement data) : base(shareType, data)
        {
            XElement node;
            node = data.Element(XName.Get("share_with"));
            if (node != null)
                this.SharedWith = node.Value;
        }
        
        /// <summary>
        /// Name of the group the target is being shared with
        /// </summary>
        public string SharedWith { get; set; }
	}
}

