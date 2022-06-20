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
	[TestFixture(Explicit = true, Category = "OCS Admin-API", Reason = "Manual check only since too many/unsafe administration changes")]
	public abstract class OcsApiFeaturesTestBase
	{
		protected OcsApiFeaturesTestBase(CompuMaster.Ocs.Test.SettingsBase settings)
		{
			this.Settings = settings;
			this.TestSettings = new CompuMaster.Ocs.OwnCloudSharpTests.TestSettings(Settings);
		}

		protected CompuMaster.Ocs.Test.SettingsBase Settings;
		protected CompuMaster.Ocs.OwnCloudSharpTests.TestSettings TestSettings;

		public const string testFileName = "/CM.Ocs.owncloud-sharp-test.txt";
		public const string testDirName = "/CM.Ocs.owncloud-sharp-test-folder";

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
		[SetUp]
		public void InitEveryTest()
		{
			System.Console.WriteLine();
			System.Console.WriteLine("## " + NUnit.Framework.TestContext.CurrentContext.Test.FullName);
			System.Console.WriteLine("TEST ENVIRONMENT: " + TestSettings.ownCloudInstanceUrl);
			System.Console.WriteLine("TEST USER       : " + TestSettings.ownCloudUser);
		}

		/// <summary>
		/// Init this test parameters.
		/// </summary>
		[OneTimeSetUp]
		public void Init()
		{
			System.Console.WriteLine();
			System.Console.WriteLine("# INIT: " + NUnit.Framework.TestContext.CurrentContext.Test.ClassName);
			System.Console.WriteLine("TEST ENVIRONMENT: " + TestSettings.ownCloudInstanceUrl);
			System.Console.WriteLine("TEST USER: " + TestSettings.ownCloudUser);

			c = new OcsClient(TestSettings.ownCloudInstanceUrl, TestSettings.ownCloudUser, TestSettings.ownCloudPassword);
			payloadData = System.Text.Encoding.UTF8.GetBytes("owncloud# NUnit Payload\r\nPlease feel free to delete");

			if (TestSettings.ownCloudInstanceUrl == null || TestSettings.ownCloudUser == null)
				Assert.Ignore("No login credentials assigned for unit tests");

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

			try
			{
				if (!c.UserExists("sharetest"))
					c.CreateUser("sharetest", "test");
				if (!c.GroupExists("testgroup"))
					c.CreateGroup("testgroup");
				if (!c.IsUserInGroup("sharetest", "testgroup"))
					c.AddUserToGroup("sharetest", "testgroup");
			}
			catch (CompuMaster.Ocs.Exceptions.OcsResponseException ex)
			{
				throw new Exception("Login user not authorized to manage users/groups (status code: " + ex.OcsStatusCode + ")", ex);
			}
		}

		/// <summary>
		/// Cleanup test data.
		/// </summary>
		[OneTimeTearDown]
		public void Cleanup()
		{
			#region OCS Share Test CleanUp
			if (c.Exists("/share-link-test.txt"))
			{
				if (c.IsShared("/share-link-test.txt"))
				{
					var shares = c.GetShares("/share-link-test.txt");
					foreach (var share in shares)
						c.DeleteShare(share.ShareId);
				}
				c.Delete("/share-link-test.txt");
			}

			if (c.Exists("/share-user-test.txt"))
			{
				if (c.IsShared("/share-user-test.txt"))
				{
					var shares = c.GetShares("/share-user-test.txt");
					foreach (var share in shares)
						c.DeleteShare(share.ShareId);
				}
				c.Delete("/share-user-test.txt");
			}

			if (c.Exists("/share-group-test.txt"))
			{
				if (c.IsShared("/share-group-test.txt"))
				{
					var shares = c.GetShares("/share-group-test.txt");
					foreach (var share in shares)
						c.DeleteShare(share.ShareId);
				}
				c.Delete("/share-group-test.txt");
			}

			if (c.Exists("/share-update-test.txt"))
			{
				if (c.IsShared("/share-update-test.txt"))
				{
					var shares = c.GetShares("/share-update-test.txt");
					foreach (var share in shares)
						c.DeleteShare(share.ShareId);
				}
				c.Delete("/share-update-test.txt");
			}

			if (c.Exists("/share-delete-test.txt"))
			{
				if (c.IsShared("/share-delete-test.txt"))
				{
					var shares = c.GetShares("/share-delete-test.txt");
					foreach (var share in shares)
						c.DeleteShare(share.ShareId);
				}
				c.Delete("/share-delete-test.txt");
			}

			if (c.Exists("/share-shared-test.txt"))
			{
				if (c.IsShared("/share-shared-test.txt"))
				{
					var shares = c.GetShares("/share-shared-test.txt");
					foreach (var share in shares)
						c.DeleteShare(share.ShareId);
				}
				c.Delete("/share-shared-test.txt");
			}

			if (c.Exists("/share-get-test.txt"))
			{
				if (c.IsShared("/share-get-test.txt"))
				{
					var shares = c.GetShares("/share-get-test.txt");
					foreach (var share in shares)
						c.DeleteShare(share.ShareId);
				}
				c.Delete("/share-get-test.txt");
			}
			#endregion

			#region OCS User Test cleanup
			if (c.UserExists("octestusr1"))
			{
				var c1 = new OcsClient(TestSettings.ownCloudInstanceUrl, "octestusr1", "octestpwd");
				var shares = c1.GetShares("");
				foreach (var share in shares)
					c1.DeleteShare(share.ShareId);
				c.DeleteUser("octestusr1");
			}
			if (c.UserExists("octestusr"))
			{
				var c2 = new OcsClient(TestSettings.ownCloudInstanceUrl, "octestusr", "octestpwd");
				var shares = c2.GetShares("");
				foreach (var share in shares)
					c2.DeleteShare(share.ShareId);
				c.DeleteUser("octestusr");
			}
			if (c.UserExists("octestusr-subadmin"))
			{
				var c2 = new OcsClient(TestSettings.ownCloudInstanceUrl, "octestusr-subadmin", "test-octestusr-subadmin");
				var shares = c2.GetShares("");
				foreach (var share in shares)
					c2.DeleteShare(share.ShareId);
				c.DeleteUser("octestusr-subadmin");
			}
			#endregion

			#region OCS App Attribute Test Cleanup
			if (c.GetAttribute("files", "test").Count > 0)
				c.DeleteAttribute("files", "test");
			#endregion

			#region General CleanUp
			var c3 = new OcsClient(TestSettings.ownCloudInstanceUrl, "sharetest", "test");
			var c3shares = c3.GetShares("");
			foreach (var share in c3shares)
				c3.DeleteShare(share.ShareId);
			c.RemoveUserFromGroup("sharetest", "testgroup");
			c.DeleteGroup("testgroup");
			c.DeleteUser("sharetest");
			#endregion
		}
		#endregion

		#region OCS Tests
		#region Remote Shares
		/*
		 * Deactivated because of testability limitations.
		 * OC 8.2 is not officialy released and currently I have only one OC 8.2 dev instance running.
		[Test ()]
		public void ListOpenRemoteShare() {
			// TODO: Implement ListOpenRemoteShare Test
		}

		[Test ()]
		public void AcceptRemoteShare() {
			// TODO: Implement AcceptRemoteShare Test
		}

		[Test ()]
		public void DeclineRemoteShare() {
			// TODO: Implement AcceptRemoteShare Test
		}

		[Test ()]
		public void ShareWithRemote() {
			MemoryStream payload = new MemoryStream (payloadData);

			var result = c.Upload ("/share-remote-test.txt", payload, "text/plain");
			var share = c.ShareWithUser ("/share-remote-test.txt", "user@example.com", Convert.ToInt32 (OcsPermission.All), OcsBoolParam.True);

			Assert.NotNull (share);
		}*/
		#endregion

		#region Shares
		/// <summary>
		/// Test ShareWithLink;
		/// </summary>
		[Test()]
		public void ShareWithLink()
		{
			MemoryStream payload = new MemoryStream(payloadData);

			c.Upload("/share-link-test.txt", payload, "text/plain");
			Assert.Catch<CompuMaster.Ocs.Exceptions.OcsResponseException>(() =>
			{
				//throws CompuMaster.Ocs.Exceptions.OCSResponseError : 404 Das öffentliche Hochladen ist nur für öffentlich freigegebene Ordner erlaubt
				//since the shared item is a file, not a folder
				c.ShareWithLink("/share-link-test.txt", OcsPermission.All, "test", OcsBoolParam.True);
			});
			var share = c.ShareWithLink("/share-link-test.txt", OcsPermission.All, "test", OcsBoolParam.False);

			Assert.NotNull(share);
		}

		/// <summary>
		/// Test ShareWithUser.
		/// </summary>
		[Test()]
		public void ShareWithUser()
		{
			MemoryStream payload = new MemoryStream(payloadData);

			c.Upload("/share-user-test.txt", payload, "text/plain");
			var share = c.ShareWithUser("/share-user-test.txt", "sharetest", OcsPermission.All, OcsBoolParam.False);

			Assert.NotNull(share);
		}

		/// <summary>
		/// Test ShareWithGroup.
		/// </summary>
		[Test()]
		public void ShareWithGroup()
		{
			MemoryStream payload = new MemoryStream(payloadData);

			c.Upload("/share-group-test.txt", payload, "text/plain");
			var share = c.ShareWithGroup("/share-group-test.txt", "testgroup", OcsPermission.All);

			Assert.NotNull(share);
		}

		/// <summary>
		/// Test UpdateShare.
		/// </summary>
		[Test()]
		public void UpdateShare()
		{
			MemoryStream payload = new MemoryStream(payloadData);

			c.Upload("/share-update-test.txt", payload, "text/plain");
			var share = c.ShareWithLink("/share-update-test.txt", OcsPermission.All, "test", OcsBoolParam.False);
			System.Console.WriteLine(share.ToString() + " >>> " + share.Url);

			c.UpdateShare(share.ShareId, OcsPermission.None, "test123test");
		}

		/// <summary>
		/// Test DeleteShare.
		/// </summary>
		[Test()]
		public void DeleteShare()
		{
			MemoryStream payload = new MemoryStream(payloadData);

			c.Upload("/share-delete-test.txt", payload, "text/plain");
			var share = c.ShareWithLink("/share-delete-test.txt", OcsPermission.All, "test", OcsBoolParam.False);

			c.DeleteShare(share.ShareId);
			Assert.True(true);
		}

		/// <summary>
		/// Test IsShared.
		/// </summary>
		/// <returns><c>true</c> if this instance is shared; otherwise, <c>false</c></returns>
		[Test()]
		public void IsShared()
		{
			MemoryStream payload = new MemoryStream(payloadData);

			c.Upload("/share-shared-test.txt", payload, "text/plain");
			c.ShareWithLink("/share-shared-test.txt", OcsPermission.All, "test", OcsBoolParam.False);
			Assert.True(c.IsShared("/share-shared-test.txt"));
		}

		/// <summary>
		/// Test GetShares for a given path.
		/// </summary>
		[Test()]
		public void GetSharesForPath()
		{
			MemoryStream payload = new MemoryStream(payloadData);

			c.Upload("/share-get-test.txt", payload, "text/plain");
			c.ShareWithLink("/share-get-test.txt", OcsPermission.All, "test", OcsBoolParam.False);

			var content = c.GetShares("/share-get-test.txt");
			Assert.Greater(content.Count, 0);
		}

		/// <summary>
		/// Test GetShares for the current user.
		/// </summary>
		[Test()]
		public void GetSharesForUser()
		{
			//currently logged in user (which typically belongs to admin role when used as test-user for these unit tests)
			{
				var shares = c.GetShares("");
				Assert.Greater(shares.Count, 0);
				foreach (var share in shares)
				{
					System.Console.WriteLine("Share found for user " + c.AuthorizedUserID + ": " + share.ToString());
				}
				Assert.That(c.GetShares().Count, Is.EqualTo(shares.Count), "Assert equal result with path = empty string or without path argument");
			}

			//test with a standard user
			GetSharesForNewTempUsers("octestusr1", "octestpwd", false);

			//test with a sub-admin user
			//GetSharesForNewTempUsers("octestusr-sub", "test-octestusr-subadmin", false);
			//GetSharesForNewTempUsers("octestusr-subadmin", "test-octestusr-subadmin", false);

			c.DeleteUser("octestusr1");
			//Assert.True(c.DeleteUser("octestusr-sub"));
			//Assert.True(c.DeleteUser("octestusr-subadmin"));

			Assert.True(true);
		}

		private void GetSharesForNewTempUsers(string username, string password, bool isSubAdmin)
		{
			if (c.UserExists(username))
				c.DeleteUser(username);
			c.CreateUser(username, password);

			var c1 = new OcsClient(TestSettings.ownCloudInstanceUrl, username, password);
			var shares = c1.GetShares();
			//Assert.Greater(shares.Count, 0); //as long as it's not implemented, there won't be any shares
			foreach (var share in shares)
			{
				System.Console.WriteLine("Share found for user " + c1.AuthorizedUserID + ": " + share.ToString());
			}
		}
		#endregion

		#region Users
		/// <summary>
		/// Test CreateUser.
		/// </summary>
		[Test()]
		public void CreateUser()
		{
			if (c.UserExists("octestusr1"))
				//Existing user must be deleted before test core can start to re-create it again
				c.DeleteUser("octestusr1");

			c.CreateUser("octestusr1", "octestpwd");
			Assert.True(c.UserExists("octestusr1"));
		}

		/// <summary>
		/// Test DeleteUser.
		/// </summary>
		[Test()]
		public void DeleteUser()
		{
			if (!c.UserExists("deluser"))
				//User must be created before test core can start to delete it again
				c.CreateUser("deluser", "delpwd");

			c.DeleteUser("deluser");
			Assert.False(c.UserExists("deluser"));
		}

		/// <summary>
		/// Test UserExists.
		/// </summary>
		[Test()]
		public void UserExists()
		{
			Assert.True(c.UserExists("sharetest"));
		}

		/// <summary>
		/// Test SearchUsers.
		/// </summary>
		[Test()]
		public void SearchUsers()
		{
			var result = c.SearchUsers("sharetest");
			Assert.Greater(result.Count, 0);
		}

		/// <summary>
		/// Test GetUserAttributes.
		/// </summary>
		[Test()]
		public void GetUserAttributes()
		{
			var result = c.GetUserAttributes("sharetest");
			Assert.NotNull(result);
		}

		/// <summary>
		/// Test SetUserAttribute.
		/// </summary>
		[Test()]
		public void SetUserAttribute()
		{
			Assert.True(c.SetUserAttribute("sharetest", OCSUserAttributeKey.EMail, "demo@example.com"));
		}

		/// <summary>
		/// Test AddUserToGroup.
		/// </summary>
		[Test()]
		public void AddUserToGroup()
		{
			if (!c.UserExists("octestusr"))
				c.CreateUser("octestusr", "octestpwd");

			c.AddUserToGroup("octestusr", "testgroup");
			Assert.True(c.IsUserInGroup("octestusr", "testgroup"));
		}

		/// <summary>
		/// Test GetUserGroups.
		/// </summary>
		[Test()]
		public void GetUserGroups()
		{
			var result = c.GetUserGroups("octestusr");
			Assert.GreaterOrEqual(result.Count, 0);
		}

		/// <summary>
		/// Test IsUserInGroup.
		/// </summary>
		/// <returns><c>true</c> if this instance is user in group; otherwise, <c>false</c></returns>
		[Test()]
		public void IsUserInGroup()
		{
			if (!c.UserExists("octestusr"))
			{
				c.CreateUser("octestusr", "octestpwd");
			}
			c.AddUserToGroup("octestusr", "testgroup");

			Assert.True(c.IsUserInGroup("octestusr", "testgroup"));
		}

		/// <summary>
		/// Test IsUserNotInGroup.
		/// </summary>
		/// <returns><c>true</c> if this instance is user not in group; otherwise, <c>false</c></returns>
		[Test()]
		public void IsUserNotInGroup()
		{
			Assert.False(c.IsUserInGroup(TestSettings.ownCloudUser, "testgroup"));
		}

		/// <summary>
		/// Test RemoveUserFromGroup.
		/// </summary>
		[Test()]
		public void RemoveUserFromGroup()
		{
			if (!c.UserExists("octestusr"))
			{
				c.CreateUser("octestusr", "octestpwd");
				c.AddUserToGroup("octestusr", "testgroup");
			}
			if (!c.IsUserInGroup("octestusr", "testgroup"))
				c.AddUserToGroup("octestusr", "testgroup");

			c.RemoveUserFromGroup("octestusr", "testgroup");
			Assert.True(true);
		}

		/// <summary>
		/// Test AddUserToSubAdminGroup.
		/// </summary>
		[Test()]
		public void AddUserToSubAdminGroup()
		{
			if (!c.UserExists("octestusr"))
			{
				c.CreateUser("octestusr", "octestpwd");
				c.AddUserToGroup("octestusr", "testgroup");
			}

			c.AddUserToSubAdminGroup("octestusr", "testgroup");
			Assert.True(true);
		}

		/// <summary>
		/// Test GetUserSubAdminGroups.
		/// </summary>
		[Test()]
		public void GetUserSubAdminGroups()
		{
			if (!c.UserExists("octestusr"))
			{
				c.CreateUser("octestusr", "octestpwd");
				c.AddUserToGroup("octestusr", "testgroup");
			}
			if (!c.IsUserInSubAdminGroup("octestusr", "testgroup"))
				c.AddUserToSubAdminGroup("octestusr", "testgroup");

			var result = c.GetUserSubAdminGroups("octestusr");
			Assert.NotNull(result);
		}

		/// <summary>
		/// Test IsUserInSubAdminGroup.
		/// </summary>
		/// <returns><c>true</c> if this instance is user in sub admin group; otherwise, <c>false</c></returns>
		[Test()]
		public void IsUserInSubAdminGroup()
		{
			if (!c.UserExists("octestusr"))
			{
				c.CreateUser("octestusr", "octestpwd");
				c.AddUserToGroup("octestusr", "testgroup");
			}
			if (!c.IsUserInSubAdminGroup("octestusr", "testgroup"))
				c.AddUserToSubAdminGroup("octestusr", "testgroup");

			Assert.True(c.IsUserInSubAdminGroup("octestusr", "testgroup"));
		}

		/// <summary>
		/// Test RemoveUserFromSubAdminGroup.
		/// </summary>
		public void RemoveUserFromSubAdminGroup()
		{
			if (!c.UserExists("octestusr"))
			{
				c.CreateUser("octestusr", "octestpwd");
				c.AddUserToGroup("octestusr", "testgroup");
			}
			if (!c.IsUserInSubAdminGroup("octestusr", "testgroup"))
				c.AddUserToSubAdminGroup("octestusr", "testgroup");

			c.RemoveUserFromSubAdminGroup("octestusr", "testgroup");
			Assert.False(c.IsUserInSubAdminGroup("octestusr", "testgroup"));
		}
		#endregion

		#region Groups
		/// <summary>
		/// Test CreateGroup.
		/// </summary>
		[Test()]
		public void CreateGroup()
		{
			if (!c.GroupExists("ocsgroup"))
				c.CreateGroup("ocsgroup");
			Assert.True(c.GroupExists("ocsgroup"));
		}

		/// <summary>
		/// Test DeleteGroup.
		/// </summary>
		[Test()]
		public void DeleteGroup()
		{
			if (!c.GroupExists("ocsgroup"))
				c.CreateGroup("ocsgroup");
			c.DeleteGroup("ocsgroup");
			Assert.False(c.GroupExists("ocsgroup"));
		}

		/// <summary>
		/// Test GroupExists with existing group.
		/// </summary>
		[Test()]
		public void GroupExists()
		{
			var result = c.GroupExists("testgroup");
			Assert.True(result);
		}

		/// <summary>
		/// Test GroupExists with not existing group.
		/// </summary>
		public void GroupNotExists()
		{
			Assert.False(c.GroupExists("ocs-does-not-exist"));
		}

		/// <summary>
		/// Test SearchGroups.
		/// </summary>
		[Test()]
		public void SearchGroups()
		{
			var result = c.SearchGroups("testgroup");
			Assert.Greater(result.Count, 0);

			foreach (string item in result)
            {
				System.Console.WriteLine(item.ToString());
            }
		}
		#endregion

		#region Config
		/// <summary>
		/// Test GetConfig.
		/// </summary>
		[Test()]
		public void GetConfig()
		{
			var result = c.GetConfig();
			Assert.NotNull(result);
		}
		#endregion

		#region Application attributes
		/// <summary>
		/// Test GetAttribute.
		/// </summary>
		public void GetAttribute()
		{
			var result = c.GetAttribute("files");
			Assert.NotNull(result);
		}

		/// <summary>
		/// Test SetAttribute.
		/// </summary>
		public void SetAttribute()
		{
			c.SetAttribute("files", "test", "true");
			Assert.True(true);
		}

		/// <summary>
		/// Test DeleteAttribute.
		/// </summary>
		public void DeleteAttribute()
		{
			if (c.GetAttribute("files", "test").Count == 0)
				c.SetAttribute("files", "test", "true");

			c.DeleteAttribute("files", "test");
			Assert.True(true);
		}
		#endregion

		#region Apps
		/// <summary>
		/// Test GetApps.
		/// </summary>
		[Test()]
		public void GetApps()
		{
			var result = c.GetApps();
			Assert.Greater(result.Count, 0);
			foreach (string AppName in result)
			{
				System.Console.WriteLine("Found app: " + AppName);
			}
		}

		/// <summary>
		/// Test GetApp.
		/// </summary>
		[Test()]
		public void GetApp()
		{
			var result = c.GetApp("contacts");
			Assert.NotNull(result);
			Assert.IsNotEmpty(result.Id);
			System.Console.WriteLine("AppInfo " + result.DisplayName + " DefaultEnable=" + result.DefaultEnable);

			result = c.GetApp("files");
			Assert.NotNull(result);
			Assert.IsNotEmpty(result.Id);
			System.Console.WriteLine("AppInfo " + result.DisplayName + " DefaultEnable=" + result.DefaultEnable);

			Assert.Catch<CompuMaster.Ocs.Exceptions.OcsResponseException>(() => c.GetApp("this-app-never-exists"));

			var AllApps = c.GetApps();
			foreach (string AppName in AllApps)
			{
				try
				{
					result = c.GetApp(AppName);
					System.Console.WriteLine("FOUND AppInfo " + AppName + " (" + result.DisplayName + ")" + " DefaultEnable=" + result.DefaultEnable);
					Assert.That(result.Id, Is.EqualTo(AppName));
				}
				catch (CompuMaster.Ocs.Exceptions.OcsResponseException ex)
				{
					System.Console.WriteLine("FAILED QUERY: AppInfo " + AppName + " -> " + ex.Message);
				}
			}
		}

		/// <summary>
		/// Test EnableApp.
		/// </summary>
		[Test(), Explicit(@"App ""news"" will change its status to enabled"), Ignore(@"App ""news"" not available at test environment")]
		public void EnableApp()
		{
			c.EnableApp("news");
			Assert.True(true);
		}

		/// <summary>
		/// Test DisableApp.
		/// </summary>
		[Test(), Explicit(@"App ""news"" will change its status to disabled"), Ignore(@"App ""news"" not available at test environment")]
		public void DisableApp()
		{
			c.DisableApp("news");
			Assert.True(true);
		}
		#endregion
		#endregion
	}
}