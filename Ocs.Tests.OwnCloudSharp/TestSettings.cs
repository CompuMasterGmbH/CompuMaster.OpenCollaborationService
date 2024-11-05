using System;
using System.Runtime.CompilerServices;

namespace CompuMaster.Ocs.OwnCloudSharpTests
{
	/// <summary>
	/// Defines settings used in the ClientTest Fixture.
	/// </summary>
	public class TestSettings
	{
		public TestSettings(CompuMaster.Ocs.Test.SettingsBase settings, string testClassName)
        {
			this.Settings = settings;
			this.OwnCloudUser = Settings.InputLine("username");
			this.OwnCloudInstanceUrl = Settings.InputLine("server url");
			this.OwnCloudPassword = Settings.InputLine("password");
			this.TestClassName = testClassName;
        }

		public string TestClassName;

		protected CompuMaster.Ocs.Test.SettingsBase Settings;

		/// <summary>
		/// The ownCloud instance URL
		/// </summary>
		public string OwnCloudInstanceUrl;
		/// <summary>
		/// The ownCloud user
		/// </summary>
		public string OwnCloudUser;
		/// <summary>
		/// The ownCloud password
		/// </summary>
		public string OwnCloudPassword;

        private const string _testFileName = "/**--test.txt";
        public string TestFileName([CallerMemberName] string callerName = "")
        {
            return GetFullContext(_testFileName, callerName);
        }

        private const string _testDirName = "/**--test-folder";
        public string TestDirName([CallerMemberName] string callerName = "")
        {
            return GetFullContext(_testDirName, callerName);
        }

        private string GetFullContext(string value, string callerName)
        {
			if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("Value must not be null or empty", nameof(value));
            if (string.IsNullOrWhiteSpace(callerName)) throw new ArgumentException("Caller name must not be null or empty", nameof(callerName));
			var result = value.Replace("**", this.TestClassName.Replace("CompuMaster.Ocs.OwnCloudSharpTests.", "CM.Ocs...") + "." + callerName);
			//return $"{callerName}: {value}";
			//return $"{GetType().FullName}.{callerName}: {value}";
			return result;
     }

        public bool IgnoreTestEnvironment
		{
			get
			{
				return (String.IsNullOrEmpty(OwnCloudInstanceUrl) || String.IsNullOrEmpty(OwnCloudUser) || OwnCloudUser == "none" || String.IsNullOrEmpty(OwnCloudPassword) || OwnCloudPassword == "none");
			}
		}
	}
}

