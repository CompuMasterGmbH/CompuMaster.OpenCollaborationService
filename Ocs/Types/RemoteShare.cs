using System;
using System.Xml.Linq;

namespace CompuMaster.Ocs.Types
{
    /// <summary>
    /// Provides information of a remote share.
    /// </summary>
	public class RemoteShare : UserShare
	{
        internal RemoteShare(Core.OcsShareType shareType, XElement data) : base(shareType, data)
        {
        }
	}
}

