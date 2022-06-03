# CompuMaster.OpenCollaborationService

An implementation for OpenCollaborationService (OCS) client 
* for use with e.g. OwnCloud, NextCloud
* for .NET Standard 2.0 incl. .Net Core + .Net Framework

Access and manage OwnCloud and NextCloud servers through WebDAV and the OwnCloud/NextCloud OCS API
* OCS API v1.7: https://www.freedesktop.org/wiki/Specifications/open-collaboration-services-1.7/
* OCS NextCloud API: https://docs.nextcloud.com/server/latest/developer_manual/client_apis/OCS/index.html#
* OCS OwnCloud API: https://doc.owncloud.com/server/latest/developer_manual/core/apis/ocs-share-api.html

Project status
==============

Current code base has been tested to work on:

* OwnCloud 10
* NextCloud 23

Instructions
============

The project is a .Net Standard 2.0 class library and it should work on .Net and Mono/Xamarin.

Sample Code
===========

### OCS API: Sharing and management

```C#
        static void ShowLoggedInEnvironment()
        {
            Client c = new Client("serverurl", "username", "password");
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
            Client c = new Client("serverurl", "username", "password");
            var user = c.GetUserAttributes("username");
            System.Console.WriteLine("EMail=" + user.EMail);
            System.Console.WriteLine("DisplayName=" + user.DisplayName);
            System.Console.WriteLine("Enabled=" + user.Enabled);
            System.Console.WriteLine("Quota.Total=" + user.Quota.Total);
            System.Console.WriteLine("Quota.Used=" + user.Quota.Used);
            System.Console.WriteLine("Quota.Free=" + user.Quota.Free);
            System.Console.WriteLine("Quota.Relative=" + user.Quota.Relative);
        }
```

### WebDAV access

```C#
using System.IO;
using System.Reflection;

namespace CompuMaster.Ocs.DemoApp
{
    class Program
    {
        static void Main(string[] args)
        {
            string path = Path.GetDirectoryName(Assembly.GetExecutingAssembly().Location);

            var c = new Client("https://cloud.server/", "uploaduser", "uploadpassword");
            var de = c.Download("/5K_Wallpaper_9.png");
            using (var fileStream = new FileStream(path + "\\5K_Wallpaper_9.png", FileMode.Create, FileAccess.Write))
            {
                de.CopyTo(fileStream);
            }

            Stream fs = File.OpenRead(path + "\\5K_Wallpaper_9.png");
            c.Upload("/Zafer.png", fs);
            var ps = c.ShareWithLink("/Zafer.png");
        }
    }
}
```

## Many thanks to the contributors

* Bastian Noffer ( [@bnoffer](https://github.com/bnoffer) ) for his initial owncloud-sharp development at https://github.com/bnoffer/owncloud-sharp
* ZaferGokhan ( https://github.com/ZaferGokhan ) for his .Net Core/.Net 5 support at https://github.com/ZaferGokhan/owncloud-sharp
* Jochen Wezel ( https://github.com/jochenwezel ), CompuMaster GmbH for his dependency updates (especially RestSharp), NuGet publishing and continued support
