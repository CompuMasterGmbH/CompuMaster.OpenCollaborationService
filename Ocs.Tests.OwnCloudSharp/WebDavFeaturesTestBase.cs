using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

using CompuMaster.Ocs;
using CompuMaster.Ocs.Core;

namespace CompuMaster.Ocs.OwnCloudSharpTests
{
	/// <summary>
	/// Tests the ownCloud# client
	/// </summary>
	/// <remarks>
	/// OCS API Standard: https://www.freedesktop.org/wiki/Specifications/open-collaboration-services-1.7/
	/// </remarks>
	[TestFixture(Category = "WebDAV" )]
	public abstract class WebDavFeaturesTestBase
	{
		protected WebDavFeaturesTestBase(CompuMaster.Ocs.Test.SettingsBase settings)
        {
			this.Settings = settings;
			this.TestSettings = new CompuMaster.Ocs.OwnCloudSharpTests.TestSettings(Settings);
		}

		protected CompuMaster.Ocs.Test.SettingsBase Settings;
		protected CompuMaster.Ocs.OwnCloudSharpTests.TestSettings TestSettings;

		#region Members
		/// <summary>
		/// ownCloud# instance.
		/// </summary>
		private OcsClient c;
		/// <summary>
		/// File upload payload data.
		/// </summary>
		private byte[] payloadData;
		#endregion

		#region Setup and Tear Down
		/// <summary>
		/// Init this test parameters.
		/// </summary>
		[OneTimeSetUp]
		public void Init()
		{
			c = new OcsClient(TestSettings.ownCloudInstanceUrl, TestSettings.ownCloudUser, TestSettings.ownCloudPassword);
			payloadData = System.Text.Encoding.UTF8.GetBytes("owncloud# NUnit Payload\r\nPlease feel free to delete");

			if (TestSettings.IgnoreTestEnvironment)
				Assert.Ignore("No login credentials assigned for unit tests");
			else
			{
				try
				{
					if (!c.Exists("/")) throw new Exception("Root directory not found");
				}
				catch (CompuMaster.Ocs.Exceptions.OcsResponseException ex)
				{
					throw new Exception("Login user \"" + TestSettings.ownCloudUser + "\" not authorized for root directory access: (status code: " + ex.OcsStatusCode + ")", ex);
				}
				catch (Exception ex)
				{
					throw new Exception("Login failed (login user \"" + TestSettings.ownCloudUser + "\")", ex);
				}
			}
		}

		/// <summary>
		/// Cleanup test data.
		/// </summary>
		[OneTimeTearDown]
		public void Cleanup()
		{
			if (!TestSettings.IgnoreTestEnvironment)
			{
				#region DAV Test CleanUp
				if (c.Exists(TestSettings.testFileName))
					c.Delete(TestSettings.testFileName);
				if (c.Exists(TestSettings.testDirName))
					c.Delete(TestSettings.testDirName);
				if (c.Exists("/copy-test"))
				{
					//c.Delete ("/copy-test/file.txt");
					c.Delete("/copy-test");
				}
				if (c.Exists("/move-test"))
				{
					//c.Delete ("/move-test/file.txt");
					c.Delete("/move-test");
				}

				if (c.Exists("/zip-test"))
				{
					//c.Delete ("/zip-test/file.txt");
					c.Delete("/zip-test");
				}
			}
			#endregion
		}
		#endregion

		#region DAV Tests
		/// <summary>
		/// Test the file upload.
		/// </summary>
		[Test ()]
		public void Upload ()
		{
			if (c.Exists(TestSettings.testFileName))
				c.Delete(TestSettings.testFileName);
			MemoryStream payload = new MemoryStream (payloadData);
			c.Upload (TestSettings.testFileName, payload, "text/plain");
			Assert.True(true);
		}

		/// <summary>
		/// Tests if a file exists.
		/// </summary>
		[Test ()]
		public void Exists() {
			if (!c.Exists(TestSettings.testFileName))
			{
				MemoryStream payload = new MemoryStream(payloadData);
				c.Upload(TestSettings.testFileName, payload, "text/plain");
			}
			Assert.True (c.Exists (TestSettings.testFileName));
		}

		/// <summary>
		/// Tests if the file does not exist.
		/// </summary>
		[Test ()]
		public void NotExists() {
			Assert.False (c.Exists ("/this-does-not-exist.txt"));
		}

		/// <summary>
		/// Tests file download.
		/// </summary>
		[Test ()]
		public void Download() {
			if (!c.Exists(TestSettings.testFileName))
			{
				MemoryStream payload = new MemoryStream(payloadData);
				c.Upload(TestSettings.testFileName, payload, "text/plain");
			}

			var content = c.Download (TestSettings.testFileName);
			Assert.IsNotNull(content);
		}

		/// <summary>
		/// Tests file deletion.
		/// </summary>
		[Test ()]
		public void Delete() {
			if (!c.Exists(TestSettings.testFileName))
			{
				MemoryStream payload = new MemoryStream(payloadData);
				c.Upload(TestSettings.testFileName, payload, "text/plain");
			}
			c.Delete (TestSettings.testFileName);
			Assert.True (true);
		}

		/// <summary>
		/// Tests directory creation.
		/// </summary>
		[Test()]
		public void CreateDirectory()
		{
			if (c.Exists(TestSettings.testDirName))
				//cleanup before core testing
				c.Delete(TestSettings.testDirName);

			c.CreateDirectory(TestSettings.testDirName);

			//already exists
			Assert.Catch(() => { c.CreateDirectory(TestSettings.testDirName); });
		}

		/// <summary>
		/// Tests directory deletion.
		/// </summary>
		[Test()]
		public void DeleteDirectory()
		{
			if (!c.Exists(TestSettings.testDirName))
				c.CreateDirectory(TestSettings.testDirName);

			c.Delete(TestSettings.testDirName);
		}

		/// <summary>
		/// Tests list command.
		/// </summary>
		[Test ()]
		public void List() {
			//prepare test environment
			MemoryStream payload = new MemoryStream(payloadData);
			c.Upload(TestSettings.testFileName, payload, "text/plain");
			if (!c.Exists(TestSettings.testDirName))
				c.CreateDirectory(TestSettings.testDirName);
			payload = new MemoryStream(payloadData);
			c.Upload(TestSettings.testDirName + TestSettings.testFileName, payload, "text/plain");

			//check test environment - root dir listing
			Assert.Catch<ArgumentNullException>(() => { var result = c.List(null); });
			Assert.Catch<ArgumentNullException>(() => { var result = c.List(""); });

			var result = c.List ("/");
			Assert.Greater (result.Count, 0);
			Assert.That(result[0].DirectoryName, Is.EqualTo(""));
			Assert.NotNull(result[0].ItemName);
			Assert.NotNull(result[0].FullPath);
			Assert.IsNotEmpty(result[0].ItemName);
			Assert.IsNotEmpty(result[0].FullPath);
			
			//check expected sub test directory
			bool TestDirFound = false;
			foreach (Types.ResourceInfo res in result)
            {
				if (res.FullPath == TestSettings.testDirName)
				{
					TestDirFound = true;
					Assert.That(res.ContentType, Is.EqualTo("dav/directory"));
					Assert.That(res.ItemName, Is.EqualTo(TestSettings.testDirName.Substring(1)));
					Assert.That(res.DirectoryName, Is.EqualTo(""));
				}
			}
			Assert.True(TestDirFound);

			//check test environment - test dir listing
			result = c.List(TestSettings.testDirName);
			Assert.That(result.Count, Is.EqualTo(1));
			Assert.That(result[0].ItemName, Is.EqualTo(TestSettings.testFileName.Substring(1)));
			Assert.That(result[0].DirectoryName, Is.EqualTo(TestSettings.testDirName));
			Assert.That(result[0].FullPath, Is.EqualTo(TestSettings.testDirName + TestSettings.testFileName));
		}

		/// <summary>
		/// Tests getting resource information.
		/// </summary>
		[Test()]
		public void GetResourceInfo()
		{
			//prepare test environment
			MemoryStream payload = new MemoryStream(payloadData);
			c.Upload(TestSettings.testFileName, payload, "text/plain");
			if (!c.Exists(TestSettings.testDirName))
				c.CreateDirectory(TestSettings.testDirName);
			payload = new MemoryStream(payloadData);
			c.Upload(TestSettings.testDirName + TestSettings.testFileName, payload, "text/plain");

			//check test environment
			Assert.Catch<ArgumentNullException>(() => { var result = c.GetResourceInfo(null); });
			Assert.Catch<ArgumentNullException>(() => { var result = c.GetResourceInfo(""); });

			var resInfo = c.GetResourceInfo("/");
			Assert.NotNull(resInfo);
			Assert.That(resInfo.FullPath, Is.EqualTo("/"));
			Assert.That(resInfo.DirectoryName, Is.EqualTo(""));
			Assert.That(resInfo.ItemName, Is.EqualTo(""));

			resInfo = c.GetResourceInfo(TestSettings.testFileName);
			Assert.NotNull(resInfo);
			Assert.That(resInfo.FullPath, Is.EqualTo(TestSettings.testFileName));
			Assert.That(resInfo.DirectoryName, Is.EqualTo(""));
			Assert.That(resInfo.ItemName, Is.EqualTo(TestSettings.testFileName.Substring(1)));

			resInfo = c.GetResourceInfo(TestSettings.testDirName + TestSettings.testFileName);
			Assert.NotNull(resInfo);
			Assert.That(resInfo.FullPath, Is.EqualTo(TestSettings.testDirName + TestSettings.testFileName));
			Assert.That(resInfo.DirectoryName, Is.EqualTo(TestSettings.testDirName));
			Assert.That(resInfo.ItemName, Is.EqualTo(TestSettings.testFileName.Substring(1)));
		}

		/// <summary>
		/// Tests copying files.
		/// </summary>
		[Test ()]
		public void Copy() {
			MemoryStream payload = new MemoryStream (payloadData);
			c.Upload (TestSettings.testFileName, payload, "text/plain");

			if (!c.Exists("/copy-test"))
				c.CreateDirectory ("/copy-test");

			c.Copy (TestSettings.testFileName, "/copy-test/file.txt");
			Assert.True (true);
		}

		/// <summary>
		/// Tests moving files.
		/// </summary>
		[Test ()]
		public void Move() {
			MemoryStream payload = new MemoryStream (payloadData);
			c.Upload (TestSettings.testFileName, payload, "text/plain");

			if (!c.Exists("/move-test"))
				c.CreateDirectory ("/move-test");

			c.Move (TestSettings.testFileName, "/move-test/file.txt");

			Assert.True (true);
		}

		/// <summary>
		/// Tests downloading a direcotry as ZIP file.
		/// </summary>
		[Test ()]
		public void DownloadDirectoryAsZip() {
			MemoryStream payload = new MemoryStream (payloadData);
			c.Upload (TestSettings.testFileName, payload, "text/plain");

			if (!c.Exists("/zip-test"))
				c.CreateDirectory ("/zip-test");

			var content = c.DownloadDirectoryAsZip ("/zip-test");
			Assert.IsNotNull (content);
		}
		#endregion
	}
}

