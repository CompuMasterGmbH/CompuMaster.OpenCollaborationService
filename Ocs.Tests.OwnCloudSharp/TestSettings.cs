using System;

namespace CompuMaster.Ocs.OwnCloudSharpTests
{
	/// <summary>
	/// Defines settings used in the ClientTest Fixture.
	/// </summary>
	public class TestSettings
	{
		public TestSettings(CompuMaster.Ocs.Test.SettingsBase settings)
        {
			this.Settings = settings;
			ownCloudUser = Settings.InputLine("username");
			ownCloudInstanceUrl = Settings.InputLine("server url");
			ownCloudPassword = Settings.InputLine("password");
		}

		protected CompuMaster.Ocs.Test.SettingsBase Settings;

		/// <summary>
		/// The ownCloud instance URL
		/// </summary>
		public string ownCloudInstanceUrl;
		/// <summary>
		/// The ownCloud user
		/// </summary>
		public string ownCloudUser;
		/// <summary>
		/// The ownCloud password
		/// </summary>
		public string ownCloudPassword;

		public string testFileName = "/CM.Ocs.owncloud-sharp test.txt";
		public string testDirName = "/CM.Ocs.owncloud-sharp test-folder";
	}
}

