# CompuMaster.OpenCollaborationService

[![Github Release](https://img.shields.io/github/release/CompuMasterGmbH/CompuMaster.OpenCollaborationService.svg?maxAge=2592000&label=GitHub%20Release)](https://github.com/CompuMasterGmbH/CompuMaster.OpenCollaborationService/releases) 
[![NuGet CompuMaster.Ocs](https://img.shields.io/nuget/v/CompuMaster.Ocs.svg?maxAge=2592000&label=NuGet%20CM.Ocs)](https://www.nuget.org/packages/CompuMaster.Ocs) 

An implementation for OpenCollaborationService (OCS) client 
* for use with e.g. OwnCloud, NextCloud
* for .NET Standard 2.0 incl. .Net Core + .Net Framework

Access and manage OwnCloud and NextCloud servers through WebDAV and the OwnCloud/NextCloud OCS API
* OCS API v1.7: https://www.freedesktop.org/wiki/Specifications/open-collaboration-services-1.7/
* OCS NextCloud API: https://docs.nextcloud.com/server/latest/developer_manual/client_apis/OCS/index.html#
* OCS OwnCloud API: https://doc.owncloud.com/server/latest/developer_manual/core/apis/ocs-share-api.html

## CompuMaster.Dms ecosystem

These separately versioned repositories form the CompuMaster.Dms ecosystem; they are not Git submodules. Repository names, local directory names, and NuGet package IDs can differ.

| Component | Repository | Responsibility |
|---|---|---|
| CompuMaster.Dms | [CompuMaster.Dms](https://github.com/CompuMasterGmbH/CompuMaster.Dms) | Provider-independent DMS workflows, provider adapters, and BrowserUI. |
| CompuMaster.Ocs | [CompuMaster.OpenCollaborationService](https://github.com/CompuMasterGmbH/CompuMaster.OpenCollaborationService) | ownCloud/Nextcloud OCS protocol, sharing, and account/group operations. |
| CompuMaster.Scopevisio.OpenApi | [CompuMaster.Scopevisio.OpenApi](https://github.com/CompuMasterGmbH/CompuMaster.Scopevisio.OpenApi) | Scopevisio OpenScope REST API and authorization. |
| CompuMaster.Scopevisio.Teamwork | [CompuMaster.Scopevisio.Teamwork](https://github.com/CompuMasterGmbH/CompuMaster.Scopevisio.Teamwork) | Teamwork integration connecting OpenScope authorization with CenterDevice clients. |
| CompuMaster.CenterDevice | [CompuMaster.CenterDevice.IO](https://github.com/CompuMasterGmbH/CompuMaster.CenterDevice.IO) | CenterDevice REST and file-system SDK; the DMS package dependency is CompuMaster.CenterDevice.Rest. |

DMS uses WebDAV for ownCloud/Nextcloud file operations and OCS for supported sharing operations. Teamwork builds on OpenScope and CenterDevice clients. Actual package versions and optional source references are defined by the project files on the branch being tested; an unmerged upstream change is not automatically available in DMS.

For cross-repository work, evaluate the affected libraries before adding a DMS-only workaround. Keep reusable protocol, authentication, transport, and SDK behavior in its owning library, while DMS retains the common workflow/capability model and UI mapping. Preserve standalone library consumers, synchronous APIs, identity semantics, and existing defaults.

Track each upstream change in an issue in its owning repository, link its implementing PR there, and reference that issue as a dependency in the affected DMS issue. Keep reciprocal links, exact commit/package versions, integration order, and verification status current. Distinguish implemented, tested, merged, published, and consumed states; do not close a dependency merely because an upstream branch is green.

Shared remote test systems require coordinated exclusive access across repositories and local sessions, including setup and cleanup. Identically named GitHub Actions concurrency groups in different repositories do not provide a shared lock. See [AGENTS.md](AGENTS.md) for working rules.

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
```

### Native DAV chunk uploads

`CompuMaster.Ocs.Core.DavChunkUploadClient` adds asynchronous native ownCloud/Nextcloud chunk uploads using a caller-owned, authenticated `IWebDavClient` and the authenticated user's `/remote.php/dav/uploads/{protocolUserId}/` namespace. Call `IsSupportedAsync` before selecting this workflow; authentication and permission failures remain errors rather than being treated as missing support.

`UploadAsync` streams ordered chunks, passes the total file length and optional UTC modification time, and waits for the final server-side assembly. The source stream remains owned by the caller. The client removes and verifies only its unique upload session on success, failure and cancellation. An interrupted final assembly is reported rather than automatically replayed.

This is a server extension, not a generic WebDAV capability. Unsupported servers and empty files continue to use ordinary PUT, whose client, server and proxy timeouts must accommodate the complete upload. Chunk uploads do not imply cross-session resume or support for ownCloud Infinite Scale's other upload protocols.

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

            var c = new OcsClient("https://cloud.server/", "uploaduser", "uploadpassword");
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
