using System;
using System.Collections.Generic;
using System.IO;

namespace CompuMaster.Ocs.Types
{
	/// <summary>
	/// File or directory information
	/// </summary>
	public class ResourceInfo
	{
		internal ResourceInfo(string fullPath, bool isDirectory)
        {
			if (fullPath == null || fullPath == "")
				throw new ArgumentNullException(nameof(fullPath));
			else if (!fullPath.StartsWith("/"))
				throw new ArgumentException("Full path must start with a directory separator ('/' character): " + fullPath, nameof(fullPath));
			else if (fullPath == "/")
			{
				this.ItemName = "";
				this.DirectoryName = "";
				this.FullPath = fullPath;
			}
			else if (fullPath.EndsWith("/"))
            {
				string fullDirPath = fullPath.Substring(0, fullPath.Length - 1);
				int LastDirectorySeparator = fullDirPath.LastIndexOf('/');
				this.ItemName = fullDirPath.Substring(LastDirectorySeparator + 1);
				this.DirectoryName = fullDirPath.Substring(0, LastDirectorySeparator);
				this.FullPath = fullDirPath;
				//throw new ArgumentException("Full path must not end with a directory separator ('/' character) " + fullPath, nameof(fullPath));
			}
			else
			{
				int LastDirectorySeparator = fullPath.LastIndexOf('/');
				this.ItemName = fullPath.Substring(LastDirectorySeparator + 1);
				this.DirectoryName = fullPath.Substring(0, LastDirectorySeparator);
				this.FullPath = fullPath;
			}
		}

		/// <summary>
		/// Gets or sets the base name of the file without path
		/// </summary>
		/// <value>name of the file or directory</value>
		public string ItemName { get; set; }

		/// <summary>
		/// Gets or sets the display name (if available)
		/// </summary>
		/// <value>optional display name of the ressource</value>
		public string DisplayName { get; set; }

		/// <summary>
		/// Gets or sets the full path to the file without item name and without trailing slash
		/// </summary>
		/// <value>parent directory path of the file or directory</value>
		public string DirectoryName { get; set; }

		/// <summary>
		/// Gets or sets the full path to the file or directory
		/// </summary>
		/// <value>full path to the file or directory</value>
		public string FullPath { get; set; }

		/// <summary>
		/// Gets or sets the size of the file in bytes
		/// </summary>
		/// <value>size of the file in bytes</value>
		public long? Size { get; set; }

		/// <summary>
		/// Gets or sets the file content type
		/// </summary>
		/// <value>file etag</value>
		public string ETag { get; set; }

		/// <summary>
		/// Gets the type of the content.
		/// </summary>
		/// <value>file content type</value>
		public string ContentType { get; set; }

		/// <summary>
		/// Gets or sets the last modified time
		/// </summary>
		/// <value>last modified time</value>
		public DateTime? LastModified { get; set; }

		/// <summary>
		/// Gets or sets the creation time
		/// </summary>
		/// <value>creation time</value>
		public DateTime? Created { get; set; }

		/// <summary>
		/// The full path
		/// </summary>
		/// <returns></returns>
        public override string ToString()
        {
			return this.FullPath;
        }
    }
}

