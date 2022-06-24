using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;

using CompuMaster.Ocs;
using CompuMaster.Ocs.Core;
using CompuMaster.Ocs.Types;

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

				try
				{
					if (!c.UserExists("sharetest"))
						c.CreateUser("sharetest", "testCryptic123!");
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
		}

		/// <summary>
		/// Cleanup test data.
		/// </summary>
		[OneTimeTearDown]
		public void Cleanup()
		{
			if (!TestSettings.IgnoreTestEnvironment)
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

				if (c.Exists("/share-folder-test"))
				{
					if (c.IsShared("/share-folder-test"))
					{
						var shares = c.GetShares("/share-folder-test");
						foreach (var share in shares)
							c.DeleteShare(share.ShareId);
					}
					c.Delete("/share-folder-test");
				}
				#endregion

				#region OCS User Test cleanup
				if (c.UserExists("octestusr1"))
				{
					var c1 = new OcsClient(TestSettings.ownCloudInstanceUrl, "octestusr1", "octestpwd-C0mplex");
					var shares = c1.GetShares("");
					foreach (var share in shares)
						c1.DeleteShare(share.ShareId);
					c.DeleteUser("octestusr1");
				}
				if (c.UserExists("octestusr"))
				{
					var c2 = new OcsClient(TestSettings.ownCloudInstanceUrl, "octestusr", "octestpwd-C0mplex");
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
				if (this.GetType() != typeof(OcsApiNextCloudTest))
				{
					if (c.GetAttribute("calendar", "test").Count > 0)
						c.DeleteAttribute("calendar", "test");
				}
				#endregion

				#region General CleanUp
				var c3 = new OcsClient(TestSettings.ownCloudInstanceUrl, "sharetest", "testCryptic123!");
				var c3shares = c3.GetShares("");
				foreach (var share in c3shares)
					c3.DeleteShare(share.ShareId);
				c.RemoveUserFromGroup("sharetest", "testgroup");
				c.DeleteGroup("testgroup");
				c.DeleteUser("sharetest");
			}
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

		#region Sharees API
		[Test()]
		public void Sharees()
		{
			List<Sharee> result;

			result = c.Sharees("test", false, "file");
			Assert.NotNull(result);
			Assert.That(result.Count, Is.GreaterThanOrEqualTo(2));
			System.Console.WriteLine("## file - test");
			foreach (Sharee value in result)
				System.Console.WriteLine("- " + value.ToString());

			result = c.Sharees("", false, "file");
			Assert.NotNull(result);
			if (this.GetType() == typeof(OcsApiNextCloudTest))
			{
				//NextCloud result contains list of all users/groups
				Assert.That(result.Count, Is.GreaterThan(0));
				System.Console.WriteLine("## file - *");
				foreach (Sharee value in result)
					System.Console.WriteLine("- " + value.ToString());
			}
			else
				//OwnCloud always results 0 items for empty string search
				Assert.That(result.Count, Is.EqualTo(0));

			result = c.Sharees("test", false, "folder");
			Assert.NotNull(result);
			Assert.That(result.Count, Is.GreaterThan(0));
			System.Console.WriteLine("## folder - test");
			foreach (Sharee value in result)
				System.Console.WriteLine("- " + value.ToString());

			result = c.Sharees("", false, "folder");
			Assert.NotNull(result);
			if (this.GetType() == typeof(OcsApiNextCloudTest))
			{
				//NextCloud result contains list of all users/groups
				Assert.That(result.Count, Is.GreaterThan(0));
				System.Console.WriteLine("## folder - *");
				foreach (Sharee value in result)
					System.Console.WriteLine("- " + value.ToString());
			}
			else
				//OwnCloud always results 0 items for empty string search
				Assert.That(result.Count, Is.EqualTo(0));
		}

		[Test()]
		public void ShareesRecommended()
		{
			List<Sharee> result;

			if (this.GetType() == typeof(OcsApiNextCloudTest))
			{
				//NextCloud supports feature ShareesRecommended
				result = c.ShareesRecommended("file");
				Assert.NotNull(result);
				Assert.That(result.Count, Is.GreaterThan(0));

				result = c.ShareesRecommended("folder");
				Assert.NotNull(result);
				Assert.That(result.Count, Is.GreaterThan(0));
			}
			else
			{
				//OwnCloud doesn't support feature
				try
				{
					c.ShareesRecommended("file");
					Assert.Fail("Expected CompuMaster.Ocs.Exceptions.OcsResponseException : OCS-StatusCode: 999 (failure), HTTP-StatusCode: 200, Message: Invalid query, please check the syntax. API specifications are here: http://www.freedesktop.org/wiki/Specifications/open-collaboration-services.");
				}
				catch (Exceptions.OcsResponseException ex)
				{
					System.Console.WriteLine("Exception found: " + ex.Message);
					Assert.That(ex.HttpStatusCode, Is.EqualTo(System.Net.HttpStatusCode.OK));
					Assert.That(ex.OcsStatusCode, Is.EqualTo(999));
				}
			}
		}

		#endregion

		#region Shares
		/// <summary>
		/// Test ShareWithLink;
		/// </summary>
		[Test()]
		public void ShareWithLink()
		{
			DateTime ExpectedExpiry = DateTime.Now.AddDays(1);

			//test sharing of file
			MemoryStream payload = new MemoryStream(payloadData);
			c.Upload("/share-link-test.txt", payload, "text/plain");
			Assert.Catch<CompuMaster.Ocs.Exceptions.OcsResponseException>(() =>
			{
				//throws CompuMaster.Ocs.Exceptions.OCSResponseError : 404 Das öffentliche Hochladen ist nur für öffentlich freigegebene Ordner erlaubt
				//since the shared item is a file, not a folder
				c.CreateShareWithLink("/share-link-test.txt", OcsPermission.All, OcsBoolParam.True, "test for allow-upload", ExpectedExpiry, "test-with-C0mplex-password");
			});
			var share = c.CreateShareWithLink("/share-link-test.txt", OcsPermission.All, OcsBoolParam.False, "test for no-public-upload", DateTime.Now.AddDays(1), "test-with-C0mplex-password");

			Assert.NotNull(share);
			Assert.That(share.Name, Is.EqualTo("test for no-public-upload"));
			Assert.That(share.ShareId, Is.Not.EqualTo(0));
			Assert.That(share.Url, Is.Not.Null);
			Assert.That(share.Url, Is.Not.Empty);
			Assert.That(share.Token, Is.Not.Null);
			Assert.That(share.Token, Is.Not.Empty);
			Assert.That(share.Type, Is.EqualTo(Ocs.Core.OcsShareType.Link));
			if (this.GetType() == typeof(OcsApiNextCloudTest))
				//typically with NextCloud, following permissions are not set after share creation for a file: Ocs.Core.OcsPermission.Create | Ocs.Core.OcsPermission.Update | Ocs.Core.OcsPermission.Delete));
				Assert.That(share.Permissions, Is.EqualTo(Ocs.Core.OcsPermission.Read | Ocs.Core.OcsPermission.Share));
			else if (this.GetType() == typeof(OcsApiOwnCloudTest))
				//typically with OwnCloud, following permissions are not set after share creation for a file: Ocs.Core.OcsPermission.Create | Ocs.Core.OcsPermission.Update | Ocs.Core.OcsPermission.Delete | Ocs.Core.OcsPermission.Share));
				Assert.That(share.Permissions, Is.EqualTo(Ocs.Core.OcsPermission.Read));
			else
				throw new NotImplementedException();

			if (this.GetType() != typeof(OcsApiNextCloudTest)) //following test is deactivated temporary due to bug issue at nextcloud server, see https://github.com/nextcloud/server/issues/10178
				Assert.That(share.Expiration.Value.Date, Is.EqualTo(ExpectedExpiry.Date));


			//test sharing of folder
			if (c.Exists("/share-folder-test"))
				c.Delete("/share-folder-test");
			c.CreateDirectory("/share-folder-test");

			share = c.CreateShareWithLink("/share-folder-test", OcsPermission.All, OcsBoolParam.False, "test for share-folder to link (no public-upload)", ExpectedExpiry, "test-with-C0mplex-password");
			Assert.That(share.Name, Is.EqualTo("test for share-folder to link (no public-upload)"));
			Assert.That(share.ShareId, Is.Not.EqualTo(0));
			Assert.That(share.Url, Is.Not.Null);
			Assert.That(share.Url, Is.Not.Empty);
			Assert.That(share.Token, Is.Not.Null);
			Assert.That(share.Token, Is.Not.Empty);
			Assert.That(share.Type, Is.EqualTo(Ocs.Core.OcsShareType.Link));
			if (this.GetType() == typeof(OcsApiNextCloudTest))
				//typically with NextCloud, following permissions are not set after share creation for a file: Ocs.Core.OcsPermission.Create | Ocs.Core.OcsPermission.Update | Ocs.Core.OcsPermission.Delete));
				Assert.That(share.Permissions, Is.EqualTo(Ocs.Core.OcsPermission.Read | Ocs.Core.OcsPermission.Share));
			else if (this.GetType() == typeof(OcsApiOwnCloudTest))
				//typically with OwnCloud, following permissions are not set after share creation for a file: Ocs.Core.OcsPermission.Create | Ocs.Core.OcsPermission.Update | Ocs.Core.OcsPermission.Delete | Ocs.Core.OcsPermission.Share));
				Assert.That(share.Permissions, Is.EqualTo(Ocs.Core.OcsPermission.Read));
			else
				throw new NotImplementedException();

			if (this.GetType() != typeof(OcsApiNextCloudTest)) //following test is deactivated temporary due to bug issue at nextcloud server, see https://github.com/nextcloud/server/issues/10178
				Assert.That(share.Expiration.Value.Date, Is.EqualTo(ExpectedExpiry.Date));

			share = c.CreateShareWithLink("/share-folder-test", OcsPermission.All, OcsBoolParam.True, "test for share-folder to link (with public-upload)", ExpectedExpiry, "public-test-with-C0mplex-password");
			Assert.That(share.Name, Is.EqualTo("test for share-folder to link (with public-upload)"));
			Assert.That(share.ShareId, Is.Not.EqualTo(0));
			Assert.That(share.Url, Is.Not.Null);
			Assert.That(share.Url, Is.Not.Empty);
			Assert.That(share.Token, Is.Not.Null);
			Assert.That(share.Token, Is.Not.Empty);
			Assert.That(share.Type, Is.EqualTo(Ocs.Core.OcsShareType.Link));
			if (this.GetType() == typeof(OcsApiNextCloudTest))
				Assert.That(share.Permissions, Is.EqualTo(Ocs.Core.OcsPermission.Read | Ocs.Core.OcsPermission.Share | Ocs.Core.OcsPermission.Create | Ocs.Core.OcsPermission.Update | Ocs.Core.OcsPermission.Delete));
			else if (this.GetType() == typeof(OcsApiOwnCloudTest))
				Assert.That(share.Permissions, Is.EqualTo(Ocs.Core.OcsPermission.Read | Ocs.Core.OcsPermission.Create | Ocs.Core.OcsPermission.Update | Ocs.Core.OcsPermission.Delete));
			else
				throw new NotImplementedException();

			if (this.GetType() != typeof(OcsApiNextCloudTest)) //following test is deactivated temporary due to bug issue at nextcloud server, see https://github.com/nextcloud/server/issues/10178
				Assert.That(share.Expiration.Value.Date, Is.EqualTo(ExpectedExpiry.Date));


		}

		/// <summary>
		/// Test ShareWithUser.
		/// </summary>
		[Test()]
		public void ShareWithUser()
		{
			DateTime ExpectedExpiry = DateTime.Now.AddDays(1);
			MemoryStream payload = new MemoryStream(payloadData);

			if (c.Exists("/share-user-test.txt"))
				c.Delete("/share-user-test.txt");

			c.Upload("/share-user-test.txt", payload, "text/plain");

			var share = c.CreateShareWithUser("/share-user-test.txt", "sharetest", OcsPermission.All, ExpectedExpiry);
			Assert.NotNull(share);
			Assert.That(share.SharedWith, Is.EqualTo("sharetest"));
			Assert.That(share.ShareId, Is.Not.EqualTo(0));
			Assert.That(share.Type, Is.EqualTo(Ocs.Core.OcsShareType.User));
			if (this.GetType() != typeof(OcsApiNextCloudTest)) //following test is deactivated temporary due to bug issue at nextcloud server, see https://github.com/nextcloud/server/issues/10178
				Assert.That(share.Expiration.Value.Date, Is.EqualTo(ExpectedExpiry.Date));
			Assert.That(share.Permissions, Is.EqualTo(Ocs.Core.OcsPermission.Read | Ocs.Core.OcsPermission.Share | Ocs.Core.OcsPermission.Update)); //typically missing at OwnCloud after share-creation: Ocs.Core.OcsPermission.Create | Ocs.Core.OcsPermission.Delete


			Assert.Catch<Ocs.Exceptions.OcsResponseException>(() =>
			{
				//fails with: CompuMaster.Ocs.Exceptions.OcsResponseException : OCS-StatusCode: 403 (failure), HTTP-StatusCode: 200, Message: Die Freigabe von share-user-test.txt ist fehlgeschlagen, da external-test-user@remote nicht gefunden wurde. Möglicherweise ist der Server nicht erreichbar.
				c.CreateShareWithRemoteUser("/share-user-test.txt", "external-test-user@remote-not-existing", OcsPermission.All, ExpectedExpiry);
			});

			/* DEACTIVATED CODE SINCE TEST ENVIRONMENT NEEDS TO BE PREPARED WITH EXTERNAL REMOTE USERS, FIRST
			var remoteShare = c.ShareWithUser("/share-user-test.txt", "external-test-user@remote-not-existing", OcsPermission.All, OcsBoolParam.True);
			Assert.NotNull(remoteShare);
			Assert.That(remoteShare.GetType, Is.EqualTo(typeof(Ocs.Types.RemoteShare)));
			Assert.That(remoteShare.SharedWith, Is.EqualTo("external-test-user"));
			Assert.That(remoteShare.ShareId, Is.Not.EqualTo(0));
			Assert.That(remoteShare.Type, Is.EqualTo(Ocs.Core.OcsShareType.Remote));
			Assert.That(share.Expiration.Value.Date, Is.EqualTo(ExpectedExpiry.Date));
			Assert.That(remoteShare.Permissions, Is.EqualTo(Ocs.Core.OcsPermission.Read | Ocs.Core.OcsPermission.Share | Ocs.Core.OcsPermission.Update)); //typically missing at OwnCloud after share-creation: Ocs.Core.OcsPermission.Create | Ocs.Core.OcsPermission.Delete
			*/

			Assert.Catch<Ocs.Exceptions.OcsResponseException>(() =>
			{
				//fails with: CompuMaster.Ocs.Exceptions.OcsResponseException : OCS-StatusCode: 403 (failure), HTTP-StatusCode: 200, Message: Die Freigabe von share-user-test.txt ist fehlgeschlagen, da external-test-user@remote nicht gefunden wurde. Möglicherweise ist der Server nicht erreichbar.
				c.CreateShareWithRemoteUser("/share-user-test.txt", "external-test-user@remote-not-existing", OcsPermission.All, ExpectedExpiry);
			});

			/* DEACTIVATED CODE SINCE TEST ENVIRONMENT NEEDS TO BE PREPARED WITH EXTERNAL REMOTE USERS, FIRST
			var remoteShare = c.ShareWithRemoteUser("/share-user-test.txt", "external-test-user@remote-not-existing", OcsPermission.All, DateTime.Now.AddDays(1));
			Assert.NotNull(remoteShare);
			Assert.That(remoteShare.GetType, Is.EqualTo(typeof(Ocs.Types.RemoteShare)));
			Assert.That(remoteShare.SharedWith, Is.EqualTo("external-test-user"));
			Assert.That(remoteShare.ShareId, Is.Not.EqualTo(0));
			Assert.That(remoteShare.Type, Is.EqualTo(Ocs.Core.OcsShareType.Remote));
			Assert.That(share.Expiration.Value.Date, Is.EqualTo(ExpectedExpiry.Date));
			Assert.That(remoteShare.Permissions, Is.EqualTo(Ocs.Core.OcsPermission.Read | Ocs.Core.OcsPermission.Share | Ocs.Core.OcsPermission.Update)); //typically missing at OwnCloud after share-creation: Ocs.Core.OcsPermission.Create | Ocs.Core.OcsPermission.Delete
			*/
		}

		/// <summary>
		/// Test ShareWithGroup.
		/// </summary>
		[Test()]
		public void ShareWithGroup()
		{
			DateTime ExpectedExpiry = DateTime.Now.AddDays(1);
			MemoryStream payload = new MemoryStream(payloadData);

			if (c.Exists("/share-group-test.txt"))
				c.Delete("/share-group-test.txt");

			c.Upload("/share-group-test.txt", payload, "text/plain");
			var share = c.CreateShareWithGroup("/share-group-test.txt", "testgroup", OcsPermission.All, ExpectedExpiry);

			Assert.NotNull(share);
			Assert.That(share.SharedWith, Is.EqualTo("testgroup"));
			Assert.That(share.ShareId, Is.Not.EqualTo(0));
			Assert.That(share.Type, Is.EqualTo(Ocs.Core.OcsShareType.Group));
			if (this.GetType() != typeof(OcsApiNextCloudTest)) //following test is deactivated temporary due to bug issue at nextcloud server, see https://github.com/nextcloud/server/issues/10178
				Assert.That(share.Expiration.GetValueOrDefault().Date, Is.EqualTo(ExpectedExpiry.Date));
			Assert.That(share.Permissions, Is.EqualTo(Ocs.Core.OcsPermission.Read | Ocs.Core.OcsPermission.Share | Ocs.Core.OcsPermission.Update)); //typically missing at OwnCloud after share-creation: Ocs.Core.OcsPermission.Create | Ocs.Core.OcsPermission.Delete
		}

		/// <summary>
		/// Test UpdateShare.
		/// </summary>
		[Test()]
		public void UpdateShare()
		{
			DateTime ExpectedExpiry = DateTime.Now.AddDays(1);
			MemoryStream payload = new MemoryStream(payloadData);

			c.Upload("/share-update-test.txt", payload, "text/plain");
			var createdShare = c.CreateShareWithLink("/share-update-test.txt", OcsPermission.All, OcsBoolParam.False, "test UpdateShare", ExpectedExpiry, "test-with-C0mplex-password");
			System.Console.WriteLine(createdShare.ToString() + " >>> " + createdShare.Url);

			string ExpectedPassword2 = "test-another-C0mplex-password";
			Share newInfo = c.UpdateShare(createdShare.ShareId, OcsPermission.None, OcsBoolParam.None, (DateTime?)null, ExpectedPassword2);
			Assert.That(newInfo.ShareId, Is.EqualTo(createdShare.ShareId));
			Assert.That(newInfo.Expiration?.Date, Is.EqualTo(ExpectedExpiry.Date));
			if (this.GetType() == typeof(OcsApiNextCloudTest))
				//NextCloud provides password information
				Assert.That(newInfo.AdvancedProperties.Password, Is.Not.Null);
			else
				//OwnCloud doesn't report password information
				Assert.That(newInfo.AdvancedProperties.Password, Is.Null);

			DateTime ExpectedExpiry3 = ExpectedExpiry.AddDays(1);
			string ExpectedPassword3 = "test-updated-C0mplex-password";
			newInfo = c.UpdateShare(createdShare.ShareId, OcsPermission.None, OcsBoolParam.None, ExpectedExpiry3, ExpectedPassword3);
			Assert.That(newInfo.ShareId, Is.EqualTo(createdShare.ShareId));
			Assert.That(newInfo.Expiration?.Date, Is.EqualTo(ExpectedExpiry3.Date));
			if (this.GetType() == typeof(OcsApiNextCloudTest))
				//NextCloud provides password information
				Assert.That(newInfo.AdvancedProperties.Password, Is.Not.Null);
			else
				//OwnCloud doesn't report password information
				Assert.That(newInfo.AdvancedProperties.Password, Is.Null);
		}

		/// <summary>
		/// Test DeleteShare.
		/// </summary>
		[Test()]
		public void DeleteShare()
		{
			MemoryStream payload = new MemoryStream(payloadData);

			c.Upload("/share-delete-test.txt", payload, "text/plain");
			var share = c.CreateShareWithLink("/share-delete-test.txt", OcsPermission.All, OcsBoolParam.False, "test DeleteShare", DateTime.Now.AddDays(1), "test-with-C0mplex-password");

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
			c.CreateShareWithLink("/share-shared-test.txt", OcsPermission.All, OcsBoolParam.False, "test IsShared", (DateTime?)null, "test-with-C0mplex-password");
			Assert.True(c.IsShared("/share-shared-test.txt"));
		}

		/// <summary>
		/// Test GetShare
		/// </summary>
		/// <returns><c>true</c> if this instance is shared; otherwise, <c>false</c></returns>
		[Test()]
		public void GetShare()
		{
			MemoryStream payload = new MemoryStream(payloadData);

			c.Upload("/share-shared-test.txt", payload, "text/plain");
			Share createdLinkShare = c.CreateShareWithLink("/share-shared-test.txt", OcsPermission.All, OcsBoolParam.False, "test IsShared", (DateTime?)null, "test-with-C0mplex-password");
			Share rereadShare = c.GetShare(createdLinkShare.ShareId);
			Assert.That(rereadShare.ShareId, Is.EqualTo(createdLinkShare.ShareId));

			//verify password is available
			if (this.GetType() == typeof(OcsApiNextCloudTest))
			{
				Assert.That(createdLinkShare.AdvancedProperties.Password, Is.Not.Null);
				Assert.That(rereadShare.AdvancedProperties.Password, Is.Not.Null);
			}
			else
			{
				Assert.That(createdLinkShare.AdvancedProperties.Password, Is.Null);
				Assert.That(rereadShare.AdvancedProperties.Password, Is.Null);
			}

			//verify password is readable as plain text
			/* NOTE: as per status 2022-06: Nextcloud OCS API provides password non-plain-text, OwnCloud doesn't report password at all
			Assert.That(createdLinkShare.AdvancedProperties.Password, Is.EqualTo("test-with-C0mplex-password"));
			Assert.That(rereadShare.AdvancedProperties.Password, Is.EqualTo("test-with-C0mplex-password"));
			*/
		}

		/// <summary>
		/// Test GetShares for a given path.
		/// </summary>
		[Test()]
		public void GetSharesForPath()
		{
			//test sharing of file
			MemoryStream payload = new MemoryStream(payloadData);
			c.Upload("/share-get-test.txt", payload, "text/plain");
			c.CreateShareWithLink("/share-get-test.txt", OcsPermission.All, OcsBoolParam.False, "test GetSharesForPath with link", DateTime.Now.AddDays(1), "test-with-C0mplex-password");
			c.CreateShareWithUser("/share-get-test.txt", "sharetest", OcsPermission.All, DateTime.Now.AddDays(1));
			c.CreateShareWithGroup("/share-get-test.txt", "testgroup", OcsPermission.All, DateTime.Now.AddDays(1));

			Assert.Catch<Ocs.Exceptions.OcsResponseException>(() =>
			{
				c.CreateShareWithLink("/share-get-test.txt", OcsPermission.All, OcsBoolParam.True, "test GetSharesForPath with link with public-upload (should fail!)", DateTime.Now.AddDays(1), "public-test-with-C0mplex-password");
			});

			var content = c.GetShares("/share-get-test.txt");
			Assert.That(content.Count, Is.EqualTo(3));
			foreach (var item in content)
			{
				Assert.That(item.TargetPath, Is.EqualTo("/share-get-test.txt"));
				Assert.That(item.AdvancedProperties.ItemType, Is.EqualTo("file"));
				Assert.That(item.AdvancedProperties.Owner, Is.EqualTo(c.AuthorizedUserID));
				Assert.That(item.AdvancedProperties.FileOwner, Is.EqualTo(c.AuthorizedUserID));
				Assert.That(item.Type, Is.InRange(Ocs.Core.OcsShareType.User, Ocs.Core.OcsShareType.Remote));
				System.Console.WriteLine("Found share type " + item.Type.ToString() + " => " + item.GetType().FullName);
				System.Console.WriteLine("Share ID " + item.ShareId.ToString() + " (" + item.Permissions.ToString() + "): " + item.TargetPath);
				Assert.That(item.AdvancedProperties.SharedWithDisplayname, Is.Not.Null);
				Assert.That(item.AdvancedProperties.SharedWithDisplayname, Is.Not.Empty);
			}


			//test sharing of folder
			if (c.Exists("/share-folder-test"))
				c.Delete("/share-folder-test");
			c.CreateDirectory("/share-folder-test");
			c.CreateShareWithLink("/share-folder-test", OcsPermission.All, OcsBoolParam.False, "test for share-folder to link (no public-upload) ", DateTime.Now.AddDays(1), "test-with-C0mplex-password");
			c.CreateShareWithLink("/share-folder-test", OcsPermission.All, OcsBoolParam.True, "test for share-folder to link (with public-upload)", DateTime.Now.AddDays(1), "public-test-with-C0mplex-password");
			c.CreateShareWithUser("/share-folder-test", "sharetest", OcsPermission.All, DateTime.Now.AddDays(1));
			//c.ShareWithUser("/share-folder-test", "remote@user", OcsPermission.All, OcsBoolParam.True); //would fail since test environment lacks external users feature
			c.CreateShareWithGroup("/share-folder-test", "testgroup", OcsPermission.All, DateTime.Now.AddDays(1));

			content = c.GetShares("/share-folder-test");
			Assert.That(content.Count, Is.EqualTo(4));
			foreach (var item in content)
			{
				Assert.That(item.TargetPath, Is.EqualTo("/share-folder-test"));
				Assert.That(item.AdvancedProperties.ItemType, Is.EqualTo("folder"));
				Assert.That(item.AdvancedProperties.Owner, Is.EqualTo(c.AuthorizedUserID));
				Assert.That(item.AdvancedProperties.FileOwner, Is.EqualTo(c.AuthorizedUserID));
				Assert.That(item.Type, Is.InRange(Ocs.Core.OcsShareType.User, Ocs.Core.OcsShareType.Remote));
				System.Console.WriteLine("Found share type " + item.Type.ToString() + " => " + item.GetType().FullName);
				System.Console.WriteLine("Share ID " + item.ShareId.ToString() + " (" + item.Permissions.ToString() + "): " + item.TargetPath);
				Assert.That(item.AdvancedProperties.SharedWithDisplayname, Is.Not.Null);
				Assert.That(item.AdvancedProperties.SharedWithDisplayname, Is.Not.Empty);
			}
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
			GetSharesForNewTempUsers("octestusr1", "octestpwd-C0mplex", false);

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

			c.CreateUser("octestusr1", "octestpwd-C0mplex");
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
				c.CreateUser("deluser", "delpwd-C0mplex");

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
			c.SetUserAttribute("sharetest", OCSUserAttributeKey.EMail, "demo@example.com");
			Assert.That(c.GetUserAttributes("sharetest").EMail, Is.EqualTo("demo@example.com"));
		}

		/// <summary>
		/// Test AddUserToGroup.
		/// </summary>
		[Test()]
		public void AddUserToGroup()
		{
			if (!c.UserExists("octestusr"))
				c.CreateUser("octestusr", "octestpwd-C0mplex");

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
				c.CreateUser("octestusr", "octestpwd-C0mplex");
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
				c.CreateUser("octestusr", "octestpwd-C0mplex");
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
				c.CreateUser("octestusr", "octestpwd-C0mplex");
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
				c.CreateUser("octestusr", "octestpwd-C0mplex");
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
				c.CreateUser("octestusr", "octestpwd-C0mplex");
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
				c.CreateUser("octestusr", "octestpwd-C0mplex");
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
		[Test()]
		public void GetAttribute()
		{
			if (this.GetType() == typeof(OcsApiNextCloudTest))
				Assert.Ignore("OCS API missing at NextCloud server?!");

			var result = c.GetAttribute("calendar");
			Assert.NotNull(result);
		}

		/// <summary>
		/// Test SetAttribute.
		/// </summary>
		[Test()]
		public void SetAttribute()
		{
			if (this.GetType() == typeof(OcsApiNextCloudTest))
				Assert.Ignore("OCS API missing at NextCloud server?!");

			c.SetAttribute("calendar", "test", "true");

			List<AppAttribute> result;

			//test getting full key-value-list
			result = c.GetAttribute("calendar");
			bool TestAttributeFound = false;
			foreach (AppAttribute attr in result)
			{
				if (attr.Key == "test")
				{
					TestAttributeFound = true;
					Assert.That(attr.value, Is.EqualTo("true"));
				}
			}
			Assert.That(TestAttributeFound, Is.EqualTo(true));

			//test getting key-only-entries
			try
			{
				result = c.GetAttribute("calendar", "test");
			}
			catch (Ocs.Exceptions.OcsResponseException)
			{
				if (this.GetType() == typeof(OcsApiNextCloudTest))
					Assert.Ignore("OCS API misfunction at NextCloud server?!");
				else
					throw;
			}
			Assert.That(result.Count, Is.EqualTo(1));
			Assert.That(result[0].value, Is.EqualTo("true"));
		}

		/// <summary>
		/// Test DeleteAttribute.
		/// </summary>
		[Test()]
		public void DeleteAttribute()
		{
			if (this.GetType() == typeof(OcsApiNextCloudTest))
				Assert.Ignore("OCS API missing at NextCloud server?!");

			if (c.GetAttribute("calendar", "test").Count == 0)
				c.SetAttribute("calendar", "test", "true");

			try
			{
				c.DeleteAttribute("calendar", "test");
			}
			catch (Ocs.Exceptions.OcsResponseException)
			{
				if (this.GetType() == typeof(OcsApiOwnCloudTest))
					Assert.Ignore("OCS API misfunction at OwnCloud server?!");
				else
					throw;
			}
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