using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

using CompuMaster.Ocs;
using CompuMaster.Ocs.Core;
using System.Threading.Tasks;
using System.Threading;

namespace CompuMaster.Ocs.OwnCloudSharpTests
{
	/// <summary>
	/// Tests the ownCloud# client
	/// </summary>
	/// <remarks>
	/// OCS API Standard: https://www.freedesktop.org/wiki/Specifications/open-collaboration-services-1.7/
	/// </remarks>
	[TestFixture(Category = "WebDAV" )]
	[Parallelizable(ParallelScope.All)]
	public abstract class WebDavFeaturesTestBase
	{
		private const int MAX_PARALLEL_TEST_TASKS = 5;
        #region Parallel Test Execution
        private static readonly SemaphoreSlim _semaphore = new SemaphoreSlim(MAX_PARALLEL_TEST_TASKS);

        [SetUp]
        public async Task MaxParallelismSetUp()
        {
            await _semaphore.WaitAsync();
        }

        [TearDown]
        public void MaxParallelismTearDown()
        {
            _semaphore.Release();
        }

		[OneTimeTearDown]
        public void MaxParallelismOneTimeTearDown()
        {
            _semaphore.Dispose();
        }
        #endregion

        protected WebDavFeaturesTestBase(CompuMaster.Ocs.Test.SettingsBase settings)
        {
			this.Settings = settings;
			this.TestSettings = new CompuMaster.Ocs.OwnCloudSharpTests.TestSettings(Settings, this.GetType().FullName);
		}

		protected CompuMaster.Ocs.Test.SettingsBase Settings;
		protected CompuMaster.Ocs.OwnCloudSharpTests.TestSettings TestSettings;

        [Test()]
        public void TestSettings_UniqueRemoteObjectNames()
        {
            Assert.That(TestSettings.TestFileName(), Is.EqualTo("/CM.Ocs...WebDavFeaturesOwnCloudTest.TestSettings_UniqueRemoteObjectNames--test.txt"));
            Assert.That(TestSettings.TestDirName(), Is.EqualTo("/CM.Ocs...WebDavFeaturesOwnCloudTest.TestSettings_UniqueRemoteObjectNames--test-folder"));
        }

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
			c = new OcsClient(TestSettings.OwnCloudInstanceUrl, TestSettings.OwnCloudUser, TestSettings.OwnCloudPassword);
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
					throw new Exception("Login user \"" + TestSettings.OwnCloudUser + "\" not authorized for root directory access: (status code: " + ex.OcsStatusCode + ")", ex);
				}
				catch (Exception ex)
				{
					throw new Exception("Login failed (login user \"" + TestSettings.OwnCloudUser + "\")", ex);
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
				if (c.Exists(TestSettings.TestFileName()))
					c.Delete(TestSettings.TestFileName());
				if (c.Exists(TestSettings.TestDirName()))
					c.Delete(TestSettings.TestDirName());
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
			if (c.Exists(TestSettings.TestFileName()))
				c.Delete(TestSettings.TestFileName());
			MemoryStream payload = new MemoryStream (payloadData);
			c.Upload (TestSettings.TestFileName(), payload, "text/plain");
			Assert.True(true);
		}

		/// <summary>
		/// Tests if a file exists.
		/// </summary>
		[Test ()]
		public void Exists() {
			if (!c.Exists(TestSettings.TestFileName()))
			{
				MemoryStream payload = new MemoryStream(payloadData);
				c.Upload(TestSettings.TestFileName(), payload, "text/plain");
			}
			Assert.True (c.Exists (TestSettings.TestFileName()));
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
			if (!c.Exists(TestSettings.TestFileName()))
			{
				MemoryStream payload = new MemoryStream(payloadData);
				c.Upload(TestSettings.TestFileName(), payload, "text/plain");
			}

			var content = c.Download (TestSettings.TestFileName());
			Assert.IsNotNull(content);
		}

		/// <summary>
		/// Tests file deletion.
		/// </summary>
		[Test ()]
		public void Delete() {
			if (!c.Exists(TestSettings.TestFileName()))
			{
				MemoryStream payload = new MemoryStream(payloadData);
				c.Upload(TestSettings.TestFileName(), payload, "text/plain");
			}
			c.Delete (TestSettings.TestFileName());
			Assert.True (true);
		}

		/// <summary>
		/// Tests directory creation.
		/// </summary>
		[Test()]
		public void CreateDirectory()
		{
			if (c.Exists(TestSettings.TestDirName()))
				//cleanup before core testing
				c.Delete(TestSettings.TestDirName());

			c.CreateDirectory(TestSettings.TestDirName());

			//already exists
			Assert.Catch(() => { c.CreateDirectory(TestSettings.TestDirName()); });
		}

		/// <summary>
		/// Tests directory deletion.
		/// </summary>
		[Test()]
		public void DeleteDirectory()
		{
			if (!c.Exists(TestSettings.TestDirName()))
				c.CreateDirectory(TestSettings.TestDirName());

			c.Delete(TestSettings.TestDirName());
		}

		/// <summary>
		/// Tests list command.
		/// </summary>
		[Test ()]
		public void List() {
			//prepare test environment
			MemoryStream payload = new MemoryStream(payloadData);
			c.Upload(TestSettings.TestFileName(), payload, "text/plain");
			if (!c.Exists(TestSettings.TestDirName()))
				c.CreateDirectory(TestSettings.TestDirName());
			payload = new MemoryStream(payloadData);
			c.Upload(TestSettings.TestDirName() + TestSettings.TestFileName(), payload, "text/plain");

			//check test environment - root dir listing
			Assert.Catch<ArgumentNullException>(() => { var resultDummy = c.List(null); });
			Assert.Catch<ArgumentNullException>(() => { var resultDummy = c.List(""); });

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
				if (res.FullPath == TestSettings.TestDirName())
				{
					TestDirFound = true;
					Assert.That(res.ContentType, Is.EqualTo("dav/directory"));
					Assert.That(res.ItemName, Is.EqualTo(TestSettings.TestDirName().Substring(1)));
					Assert.That(res.DirectoryName, Is.EqualTo(""));
				}
			}
			Assert.True(TestDirFound);

			//check test environment - test dir listing
			result = c.List(TestSettings.TestDirName());
			Assert.That(result.Count, Is.EqualTo(1));
			Assert.That(result[0].ItemName, Is.EqualTo(TestSettings.TestFileName().Substring(1)));
			Assert.That(result[0].DirectoryName, Is.EqualTo(TestSettings.TestDirName()));
			Assert.That(result[0].FullPath, Is.EqualTo(TestSettings.TestDirName() + TestSettings.TestFileName()));
		}

		/// <summary>
		/// Tests getting resource information.
		/// </summary>
		[Test()]
		public void GetResourceInfo()
		{
			//prepare test environment
			MemoryStream payload = new MemoryStream(payloadData);
			c.Upload(TestSettings.TestFileName(), payload, "text/plain");
			if (!c.Exists(TestSettings.TestDirName()))
				c.CreateDirectory(TestSettings.TestDirName());
			payload = new MemoryStream(payloadData);
			c.Upload(TestSettings.TestDirName() + TestSettings.TestFileName(), payload, "text/plain");

			//check test environment
			Assert.Catch<ArgumentNullException>(() => { var result = c.GetResourceInfo(null); });
			Assert.Catch<ArgumentNullException>(() => { var result = c.GetResourceInfo(""); });

			var resInfo = c.GetResourceInfo("/");
			Assert.NotNull(resInfo);
			Assert.That(resInfo.FullPath, Is.EqualTo("/"));
			Assert.That(resInfo.DirectoryName, Is.EqualTo(""));
			Assert.That(resInfo.ItemName, Is.EqualTo(""));

			resInfo = c.GetResourceInfo(TestSettings.TestFileName());
			Assert.NotNull(resInfo);
			Assert.That(resInfo.FullPath, Is.EqualTo(TestSettings.TestFileName()));
			Assert.That(resInfo.DirectoryName, Is.EqualTo(""));
			Assert.That(resInfo.ItemName, Is.EqualTo(TestSettings.TestFileName().Substring(1)));

			resInfo = c.GetResourceInfo(TestSettings.TestDirName() + TestSettings.TestFileName());
			Assert.NotNull(resInfo);
			Assert.That(resInfo.FullPath, Is.EqualTo(TestSettings.TestDirName() + TestSettings.TestFileName()));
			Assert.That(resInfo.DirectoryName, Is.EqualTo(TestSettings.TestDirName()));
			Assert.That(resInfo.ItemName, Is.EqualTo(TestSettings.TestFileName().Substring(1)));
		}

		/// <summary>
		/// Tests copying files.
		/// </summary>
		[Test ()]
		public void Copy() {
			MemoryStream payload = new MemoryStream (payloadData);
			c.Upload (TestSettings.TestFileName(), payload, "text/plain");

			if (!c.Exists("/copy-test"))
				c.CreateDirectory ("/copy-test");

			c.Copy (TestSettings.TestFileName(), "/copy-test/file.txt");
			Assert.True (true);
		}

		/// <summary>
		/// Tests moving files.
		/// </summary>
		[Test ()]
		public void Move() {
			MemoryStream payload = new MemoryStream (payloadData);
			c.Upload (TestSettings.TestFileName(), payload, "text/plain");

			if (!c.Exists("/move-test"))
				c.CreateDirectory ("/move-test");

			c.Move (TestSettings.TestFileName(), "/move-test/file.txt");

			Assert.True (true);
		}

		/// <summary>
		/// Tests downloading a direcotry as ZIP file.
		/// </summary>
		[Test ()]
		public void DownloadDirectoryAsZip() {
			MemoryStream payload = new MemoryStream (payloadData);
			c.Upload (TestSettings.TestFileName(), payload, "text/plain");

			if (!c.Exists("/zip-test"))
				c.CreateDirectory ("/zip-test");

			var content = c.DownloadDirectoryAsZip ("/zip-test");
			Assert.IsNotNull (content);
		}
		#endregion
	}
}

