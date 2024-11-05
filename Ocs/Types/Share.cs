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
	public class Share
	{
		internal Share(Core.OcsShareType shareType, XElement data)
		{
			this.Type = shareType;
			this.AdvancedProperties = new AdvancedShareProperties();

			XElement node;
			#region General Basic Properties
			node = data.Element(XName.Get("id"));
			if (node != null)
				this.ShareId = Convert.ToInt32(node.Value);

			node = data.Element(XName.Get("file_target"));
			if (node != null)
				this.TargetPath = node.Value;

			node = data.Element(XName.Get("permissions"));
			if (node != null)
				this.Permissions = (CompuMaster.Ocs.Core.OcsPermission)Convert.ToInt32(node.Value);

			node = data.Element(XName.Get("expiration"));
			if (node != null && !String.IsNullOrEmpty(node.Value))
				this.Expiration = DateTime.Parse(node.Value);

            node = data.Element(XName.Get("name"));
            if (node != null && !String.IsNullOrEmpty(node.Value))
                this.Name = node.Value;
			else
			{
                node = data.Element(XName.Get("label"));
                if (node != null && !String.IsNullOrEmpty(node.Value))
                    this.Name = node.Value;
            }

            node = data.Element(XName.Get("note"));
            if (node != null && !String.IsNullOrEmpty(node.Value))
                this.Note = node.Value;
            #endregion

            #region Advanced Properties
            node = data.Element(XName.Get("item_type"));
			if (node != null)
				this.AdvancedProperties.ItemType = node.Value;

			node = data.Element(XName.Get("item_source"));
			if (node != null)
				this.AdvancedProperties.ItemSource = node.Value;

			node = data.Element(XName.Get("parent"));
			if (node != null)
				this.AdvancedProperties.Parent = node.Value;

			node = data.Element(XName.Get("file_source"));
			if (node != null)
				this.AdvancedProperties.FileSource = node.Value;

			node = data.Element(XName.Get("stime"));
			if (node != null)
				this.AdvancedProperties.STime = node.Value;

			node = data.Element(XName.Get("expiration"));
			if (node != null)
				this.AdvancedProperties.Expiration = node.Value;

			node = data.Element(XName.Get("mail_send"));
			if (node != null)
				this.AdvancedProperties.MailSend = node.Value;

			node = data.Element(XName.Get("uid_owner"));
			if (node != null)
				this.AdvancedProperties.Owner = node.Value;

			node = data.Element(XName.Get("storage_id"));
			if (node != null)
				this.AdvancedProperties.StorageId = node.Value;

			node = data.Element(XName.Get("storage"));
			if (node != null)
				this.AdvancedProperties.Storage = node.Value;

			node = data.Element(XName.Get("file_parent"));
			if (node != null)
				this.AdvancedProperties.FileParent = node.Value;

			node = data.Element(XName.Get("uid_file_owner"));
			if (node != null)
				this.AdvancedProperties.FileOwner = node.Value;

			node = data.Element(XName.Get("displayname_file_owner"));
			if (node != null)
				this.AdvancedProperties.FileOwnerDisplayname = node.Value;

			node = data.Element(XName.Get("share_with_displayname"));
			if (node != null)
				this.AdvancedProperties.SharedWithDisplayname = node.Value;

			node = data.Element(XName.Get("displayname_owner"));
			if (node != null)
				this.AdvancedProperties.DisplaynameOwner = node.Value;

            node = data.Element(XName.Get("password"));
            if (node != null)
                this.AdvancedProperties.Password = node.Value;

            #endregion
        }

        /// <summary>
        /// The shares Id assigned by ownCloud
        /// </summary>
        public int ShareId { get; set; }
		/// <summary>
		/// The shares type Id assigned by ownCloud
		/// </summary>
		public Core.OcsShareType Type { get; set; }
		/// <summary>
		/// The path to the target file/folder
		/// </summary>
		public string TargetPath { get; set; }
		/// <summary>
		/// The permissions granted on the share
		/// </summary>
		public CompuMaster.Ocs.Core.OcsPermission Permissions { get; set; }
		/// <summary>
		/// Advanced properties of share
		/// </summary>
		/// <value>The advanced properties.</value>
		public AdvancedShareProperties AdvancedProperties { get; set; }
		/// <summary>
		/// Expiration date.
		/// </summary>
		/// <value>The expiration.</value>
		public DateTime? Expiration { get; set; }

        /// <summary>
        /// Share note
        /// </summary>
        public string Note { get; set; }

        /// <summary>
        /// Share note
        /// </summary>
        public string Name { get; set; }

        /// <summary>
        /// Share summary
        /// </summary>
        /// <returns></returns>
        public override string ToString()
		{
			return "Share ID " + this.ShareId.ToString() + " (Permission: " + this.Permissions.ToString() + ") " + this.TargetPath;
		}
	}

	/// <summary>
	/// Advanced share properties.
	/// </summary>
	public class AdvancedShareProperties
	{
		/// <summary>
		/// Gets or sets the type of the item.
		/// </summary>
		/// <value>The type of the item.</value>
		public string ItemType { get; set; }
		/// <summary>
		/// Gets or sets the item source.
		/// </summary>
		/// <value>The item source.</value>
		public string ItemSource { get; set; }
		/// <summary>
		/// Gets or sets the parent share.
		/// </summary>
		/// <value>The parent.</value>
		public string Parent { get; set; }
		/// <summary>
		/// Gets or sets the S time.
		/// </summary>
		/// <value>The S time.</value>
		public string STime { get; set; }
		/// <summary>
		/// Gets or sets the expiration date.
		/// </summary>
		/// <value>The expiration.</value>
		public string Expiration { get; set; }
		/// <summary>
		/// Gets or sets the storage location.
		/// </summary>
		/// <value>The storage.</value>
		public string Storage { get; set; }
		/// <summary>
		/// Gets or sets the mail send.
		/// </summary>
		/// <value>The mail send.</value>
		public string MailSend { get; set; }
		/// <summary>
		/// Gets or sets the owner.
		/// </summary>
		/// <value>The owner.</value>
		public string Owner { get; set; }
		/// <summary>
		/// Gets or sets the storage identifier.
		/// </summary>
		/// <value>The storage identifier.</value>
		public string StorageId { get; set; }
		/// <summary>
		/// Gets or sets the file source.
		/// </summary>
		/// <value>The file source.</value>
		public string FileSource { get; set; }
		/// <summary>
		/// Gets or sets the file parent.
		/// </summary>
		/// <value>The file parent.</value>
		public string FileParent { get; set; }
		/// <summary>
		/// Gets or sets the file owner.
		/// </summary>
		/// <value>The file parent.</value>
		public string FileOwner { get; set; }
		/// <summary>
		/// Gets or sets the file owner displayname.
		/// </summary>
		/// <value>The file parent.</value>
		public string FileOwnerDisplayname { get; set; }
		/// <summary>
		/// Gets or sets the share with displayname.
		/// </summary>
		/// <value>The share with displayname.</value>
		public string SharedWithDisplayname { get; set; }
		/// <summary>
		/// Gets or sets the displayname owner.
		/// </summary>
		/// <value>The displayname owner.</value>
		public string DisplaynameOwner { get; set; }
		/// <summary>
		/// Gets or sets the password (note: Nextcloud doesn't report password in plain text, OwnCloud doesn't provide the password at all)
		/// </summary>
		/// <value>The displayname owner.</value>
		public string Password { get; set; }
	}
}