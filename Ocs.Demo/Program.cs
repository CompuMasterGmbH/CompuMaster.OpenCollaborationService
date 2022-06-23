using System;
using System.IO;
using System.Reflection;

namespace CompuMaster.Ocs.DemoApp
{
    class Program
    {
        static void Main(string[] args)
        {
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var c = new OcsClient("https://cloud.server/", "uploaduser", "uploadpassword");
            var de = c.Download("/5K_Wallpaper_9.png");
            using (var fileStream = new FileStream(path + "\\5K_Wallpaper_9.png", FileMode.Create, FileAccess.Write))
            {
                de.CopyTo(fileStream);
            }

            Stream fs = File.OpenRead(path + "\\5K_Wallpaper_9.png");
            c.Upload("/Zafer.png", fs);
            var ps = c.CreateShareWithLink("/Zafer.png", Core.OcsPermission.Read, Core.OcsBoolParam.False, "Test Zafer", (DateTime?)null, (string)null);
            System.Console.WriteLine("Shared /Zafer.png with link: " + ps.Url);
        }

        static void ShowLoggedInEnvironment()
        {
            OcsClient c = new OcsClient("serverurl", "username", "password");
            System.Console.WriteLine("## Instance");
            System.Console.WriteLine("BaseUrl=" + c.BaseUrl);
            System.Console.WriteLine("WebDavBaseUrl=" + c.WebDavBaseUrl);
            System.Console.WriteLine();
            System.Console.WriteLine("## User");
            System.Console.WriteLine(c.AuthorizedUserID);
            System.Console.WriteLine();
            System.Console.WriteLine("## Config");
            System.Console.WriteLine("website=" + c.GetConfig().Website);
            System.Console.WriteLine("Host=" + c.GetConfig().Host);
            System.Console.WriteLine("Ssl=" + c.GetConfig().Ssl);
            System.Console.WriteLine("Contact=" + c.GetConfig().Contact);
            System.Console.WriteLine("Version=" + c.GetConfig().Version);
        }

        static void ShowLoggedInUserInfo()
        {
            OcsClient c = new OcsClient("serverurl", "username", "password");
            var user = c.GetUserAttributes("username");
            System.Console.WriteLine("EMail=" + user.EMail);
            System.Console.WriteLine("DisplayName=" + user.DisplayName);
            System.Console.WriteLine("Enabled=" + user.Enabled);
            System.Console.WriteLine("Quota.Total=" + user.Quota.Total);
            System.Console.WriteLine("Quota.Used=" + user.Quota.Used);
            System.Console.WriteLine("Quota.Free=" + user.Quota.Free);
            System.Console.WriteLine("Quota.Relative=" + user.Quota.Relative);
        }
    }
}
