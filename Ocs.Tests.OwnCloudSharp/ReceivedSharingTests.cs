using System;
using System.Net;
using System.Net.Http;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using CompuMaster.Ocs.Core;
using CompuMaster.Ocs.Types;
using NUnit.Framework;
using RestSharp;

namespace CompuMaster.Ocs.OwnCloudSharpTests
{
    [TestFixture]
    public class ReceivedSharingTests
    {
        [TestCase(null), TestCase(""), TestCase("/Mounted + folder/report.txt")]
        public void ReceivedQueryPreservesIdentitySenderOwnerAndEachGrant(string path)
        {
            var wire = new Wire();
            var client = Client(wire, out var rest);
            using (rest)
            {
                var shares = client.GetReceivedShares(path);
                Assert.That(wire.Uri.AbsolutePath, Is.EqualTo("/prefix/ocs/v1.php/apps/files_sharing/api/v1/shares"));
                Assert.That(wire.Uri.Query, Does.Contain("shared_with_me=true"));
                Assert.That(wire.Uri.Query, Does.Not.Contain("reshares="));
                Assert.That(wire.Uri.Query, Does.Not.Contain("subfiles="));
                if (!string.IsNullOrEmpty(path)) Assert.That(Uri.UnescapeDataString(wire.Uri.Query), Does.Contain("path=" + path));
                Assert.That(shares.Count, Is.EqualTo(2));
                Assert.That(shares[0].AdvancedProperties.Owner, Is.EqualTo("sharing-user"));
                Assert.That(shares[0].AdvancedProperties.FileOwner, Is.EqualTo("original-owner"));
                Assert.That(shares[0].AdvancedProperties.FileSource, Is.EqualTo("opaque-id"));
                Assert.That(shares[0].TargetPath, Is.EqualTo("/Mounted folder"));
                Assert.That(shares[0].PermissionsAvailable, Is.True);
                Assert.That(shares[0].Permissions, Is.EqualTo((OcsPermission)17));
                Assert.That(((GroupShare)shares[1]).SharedWith, Is.EqualTo("team-id"));
                Assert.That(shares[1].PermissionsAvailable, Is.False);
            }
        }

        [Test]
        public void ExistingOutgoingSignatureKeepsItsQueryDefaults()
        {
            var wire = new Wire();
            var client = Client(wire, out var rest);
            using (rest)
            {
                client.GetShares();
                Assert.That(wire.Uri.Query, Does.Not.Contain("shared_with_me"));
                Assert.That(wire.Uri.Query, Does.Not.Contain("reshares="));
                client.GetShares("/file", OcsBoolParam.True, OcsBoolParam.False);
                Assert.That(wire.Uri.Query, Does.Contain("reshares=true"));
                Assert.That(wire.Uri.Query, Does.Contain("subfiles=false"));
                Assert.That(wire.Uri.Query, Does.Not.Contain("shared_with_me"));
            }
        }

        [TestCase(true), TestCase(false)]
        public void FailedQueriesAreNotReportedAsEmptyReceivedShares(bool httpFailure)
        {
            var wire = new Wire { Status = httpFailure ? HttpStatusCode.Forbidden : HttpStatusCode.OK,
                Body = "<ocs><meta><status>failure</status><statuscode>403</statuscode><message>denied</message></meta><data /></ocs>" };
            var client = Client(wire, out var rest);
            using (rest) Assert.That(() => client.GetReceivedShares(), Throws.Exception);
        }

        [TestCase("<data />", false)]
        [TestCase("", true)]
        [TestCase("<data><unexpected /></data>", true)]
        [TestCase("<data><element><id>1</id></element></data>", true)]
        public void EmptyReceivedListDiffersFromMalformedSuccess(string data, bool fails)
        {
            var wire = new Wire { Body = "<ocs><meta><status>ok</status><statuscode>100</statuscode><message>OK</message></meta>" + data + "</ocs>" };
            var client = Client(wire, out var rest);
            using (rest)
            {
                if (fails) Assert.That(() => client.GetReceivedShares(), Throws.Exception);
                else Assert.That(client.GetReceivedShares(), Is.Empty);
            }
        }

        private static OcsClient Client(Wire wire, out RestClient rest)
        {
            var client = new OcsClient("https://fixture.test/prefix", "recipient-id", "fixture-password");
            var field = typeof(OcsClient).GetField("rest", BindingFlags.Instance | BindingFlags.NonPublic);
            ((RestClient)field.GetValue(client)).Dispose();
            rest = new RestClient(new RestClientOptions("https://fixture.test/prefix/ocs/v1.php/")
                { ConfigureMessageHandler = _ => wire });
            field.SetValue(client, rest);
            return client;
        }

        private sealed class Wire : HttpMessageHandler
        {
            internal Uri Uri;
            internal HttpStatusCode Status = HttpStatusCode.OK;
            internal string Body = "<ocs><meta><status>ok</status><statuscode>100</statuscode><message>OK</message></meta><data>" +
                "<element><id>10</id><share_type>0</share_type><uid_owner>sharing-user</uid_owner><uid_file_owner>original-owner</uid_file_owner><file_source>opaque-id</file_source><file_target>/Mounted folder</file_target><permissions>17</permissions><share_with>recipient-id</share_with></element>" +
                "<element><id>11</id><share_type>1</share_type><share_with>team-id</share_with><file_target>/Mounted folder</file_target></element></data></ocs>";
            protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Uri = request.RequestUri;
                return Task.FromResult(new HttpResponseMessage(Status) { Content = new StringContent(Body) });
            }
        }
    }
}
