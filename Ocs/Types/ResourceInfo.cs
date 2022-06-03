﻿using System;
using System.Collections.Generic;
using System.IO;

namespace CompuMaster.Ocs.Types
{
	/// <summary>
	/// File or directory information
	/// </summary>
	public class ResourceInfo
	{
		/// <summary>
		/// Gets or sets the base name of the file without path
		/// </summary>
		/// <value>name of the file</value>
		public string Name { get; set; }

		/// <summary>
		/// Gets or sets the full path to the file without name and without trailing slash
		/// </summary>
		/// <value>path to the file</value>
		public string Path { get; set; }

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

	}
}

