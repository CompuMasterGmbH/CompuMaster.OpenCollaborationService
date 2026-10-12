using System;
using CompuMaster.Ocs.Core;
using NUnit.Framework;

namespace CompuMaster.Ocs.OwnCloudSharpTests
{
    [TestFixture]
    public sealed class DavFileVersionEndpointTests
    {
        [TestCase("remote.php/webdav", "remote.php/dav/")]
        [TestCase("webdav", "dav/")]
        [TestCase("remote.php/dav/files/url-user", "remote.php/dav/")]
        [TestCase("dav/files/url-user", "dav/")]
        public void PersonalAndLegacyFoldersKeepPrefixAndUseAuthenticatedIdentity(string path, string root)
        {
            var endpoint = DavFileVersionEndpoint.Resolve(new Uri("https://fixture.test:8443/prefix/" + path + "/Folder%2B%20one/"), DavFileVersionProtocol.Nextcloud, "protocol+ id");
            Assert.That(endpoint.DavRoot.AbsoluteUri, Is.EqualTo("https://fixture.test:8443/prefix/" + root));
            Assert.That(endpoint.UserId, Is.EqualTo("protocol+ id"));
            Assert.That(endpoint.FilesRoot.AbsoluteUri, Is.EqualTo(path.Contains("files/") ? "https://fixture.test:8443/prefix/" + path + "/Folder%2B%20one/" : endpoint.DavRoot + "files/protocol%2B%20id/Folder%2B%20one/"));
        }

        [TestCase("dav"), TestCase("remote.php/dav")]
        public void InfiniteScaleSpaceUsesOpaqueResourceIdWithoutUserSubstitution(string path)
        {
            var uri = new Uri("https://fixture.test/prefix/" + path + "/spaces/storage%24space!id/Folder/");
            var endpoint = DavFileVersionEndpoint.Resolve(uri, DavFileVersionProtocol.OwnCloudInfiniteScale);
            Assert.That(endpoint.FilesRoot, Is.EqualTo(uri));
            Assert.That(endpoint.DavRoot.AbsoluteUri, Is.EqualTo("https://fixture.test/prefix/" + path + "/"));
            Assert.That(endpoint.UserId, Is.Null);
            Assert.Throws<NotSupportedException>(() => DavFileVersionEndpoint.Resolve(uri, DavFileVersionProtocol.Nextcloud));
            Assert.Throws<NotSupportedException>(() => DavFileVersionEndpoint.Resolve(uri, DavFileVersionProtocol.OwnCloudServer));
        }

        [TestCase(1), TestCase(2), TestCase(3)]
        public void LegacyNeverGuessesUserIdentity(int protocol)
        {
            var uri = new Uri("https://fixture.test/prefix/remote.php/webdav/Folder/");
            Assert.Throws<NotSupportedException>(() => DavFileVersionEndpoint.Resolve(uri, (DavFileVersionProtocol)protocol));
            var endpoint = DavFileVersionEndpoint.Resolve(uri, (DavFileVersionProtocol)protocol, "opaque-id");
            Assert.That(endpoint.FilesRoot.AbsoluteUri, Is.EqualTo("https://fixture.test/prefix/remote.php/dav/files/opaque-id/Folder/"));
        }

        [TestCase("https://login:secret@fixture.test/dav/files/user/")]
        [TestCase("https://fixture.test/dav/files/user/?query=x")]
        [TestCase("https://fixture.test/dav/files/user/#fragment")]
        [TestCase("https://fixture.test/dav/spaces/space%2Fother/")]
        [TestCase("https://fixture.test/dav/files/user/../other/")]
        [TestCase("https://fixture.test/dav/files/user/%2e%2e/other/")]
        [TestCase("https://fixture.test/dav/files/user/folder%5Cother/")]
        [TestCase("https://fixture.test/dav/files/user/folder%00/")]
        public void InvalidOriginsIdentitiesAndTraversalFailLocally(string value)
        {
            Assert.Throws<ArgumentException>(() => DavFileVersionEndpoint.Resolve(new Uri(value), DavFileVersionProtocol.OwnCloudInfiniteScale));
        }

        [TestCase(""), TestCase("../user"), TestCase("user\\other"), TestCase("user\n")]
        public void InvalidAuthenticatedIdentityIsNotInsertedInAnAddress(string value)
        {
            Assert.Throws<ArgumentException>(() => DavFileVersionEndpoint.Resolve(new Uri("https://fixture.test/webdav/"), DavFileVersionProtocol.Nextcloud, value));
        }
    }
}
