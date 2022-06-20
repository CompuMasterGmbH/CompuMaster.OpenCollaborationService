using System.IO;
using System.Text;
using System.Xml;
using System.Xml.Serialization;
using NUnit.Framework;

namespace CompuMaster.Ocs.OwnCloudSharpTests
{
    public class OcsResultDeserialization
    {
        [Test] public void Groups()
        {
            TestDeserializedData(@"<?xml version=""1.0""?>
<ocs>
 <meta>
  <status>ok</status>
  <statuscode>100</statuscode>
  <message/>
 </meta>
 <data>
  <groups>
   <element>testgroup</element>
  </groups>
 </data>
</ocs>
");
        }

        [Test]
        public void CreateUser()
        {
            TestDeserializedData(@"<?xml version=""1.0""?>
<ocs>
 <meta>
  <status>ok</status>
  <statuscode>100</statuscode>
  <message/>
 </meta>
 <data/>
</ocs>
");
        }

        [Test]
        public void UsersSearch()
        {
            TestDeserializedData(@"<?xml version=""1.0""?>
<ocs>
 <meta>
  <status>ok</status>
  <statuscode>100</statuscode>
  <message>OK</message>
  <totalitems></totalitems>
  <itemsperpage></itemsperpage>
 </meta>
 <data>
  <users>
   <element>compumaster-unit-tests</element>
   <element>compumaster-unit-tests-admin</element>
   <element>gustav</element>
   <element>irene</element>
   <element>jwezel</element>
   <element>lwezel</element>
  </users>
 </data>
</ocs>
");
        }

            [Test]
            public void ServerConfig()
            {
                TestDeserializedData(@"<?xml version=""1.0""?>
<ocs>
 <meta>
  <status>ok</status>
  <statuscode>100</statuscode>
  <message>OK</message>
  <totalitems></totalitems>
  <itemsperpage></itemsperpage>
 </meta>
 <data>
  <version>1.7</version>
  <website>Nextcloud</website>
  <host>192.168.32.141</host>
  <contact></contact>
  <ssl>false</ssl>
 </data>
</ocs>
");
            }
        private void TestDeserializedData(string xml)
        {
            var serializer = new XmlSerializer(typeof(CompuMaster.Ocs.Types.OcsResponseResult));
            CompuMaster.Ocs.Types.OcsResponseResult result;

            using (TextReader reader = new StringReader(xml))
            {
                result = (CompuMaster.Ocs.Types.OcsResponseResult)serializer.Deserialize(reader);
            }

            Assert.NotNull(result, "content root element deserialization failed");
            Assert.NotNull(result.Meta, "meta property must be not null");
            System.Console.WriteLine(result.Meta.ToString());
            Assert.NotNull(result.Data, "data property must be not null");
            System.Console.WriteLine("Data: ");
            System.Console.WriteLine(ObjectResultToString(result.Data, 1));
            Assert.NotNull(result.Meta.Status, "meta.status property must be not null");
            Assert.NotNull(result.Meta.StatusCode, "meta.statuscode property must be not null");
            Assert.NotNull(result.Meta.Message, "meta.message property must be not null");
        }

        public static string ObjectResultToString(object obj, int indentLevel)
        {
            var sb = new StringBuilder();

            if (obj == null)
            {
                sb.Append(new string(' ', 4 * indentLevel));
                sb.AppendLine(@"{NULL}");
            }
            else if (obj.GetType() == typeof(System.Xml.XmlNode))
            {
                var node = (System.Xml.XmlNode)obj;
                sb.Append(new string(' ', 4 * indentLevel));
                sb.AppendLine(node.Name + ": ");
                sb.AppendLine(ObjectResultToString(node, (indentLevel + 1)));
            }
            else if (obj.GetType() == typeof(System.Xml.XmlNode[]))
            {
                sb.Append(new string(' ', 4 * indentLevel));
                sb.AppendLine("[]");
                int counter = 0;
                foreach (System.Xml.XmlNode node in (System.Xml.XmlNode[])obj)
                {
                    sb.Append(new string(' ', 4 * (indentLevel + 1)));
                    sb.AppendLine("[" + counter.ToString() + "]");

                    sb.Append(new string(' ', 4 * (indentLevel + 1)));
                    sb.AppendLine(node.Name + ": ");
                    sb.AppendLine(ObjectResultToString(node, (indentLevel + 2)));
                    counter += 1;
                }
            }
            else if (obj.GetType() == typeof(string))
            {
                sb.Append(new string(' ', 4 * indentLevel));
                sb.AppendLine(obj.ToString());
            }
            else
            {
                //iterate properties
                var props = obj.GetType().GetProperties();
                if (props.Length == 0)
                {
                    sb.Append(new string(' ', 4 * indentLevel));
                    sb.AppendLine(obj.ToString());
                }
                else
                {
                    sb.Append(new string(' ', 4 * indentLevel));
                    sb.AppendLine("[]");
                    int counter = 0;
                    foreach (var p in props)
                    {
                        sb.Append(new string(' ', 4 * (indentLevel + 1)));
                        sb.AppendLine("[" + counter.ToString() + "]");

                        sb.Append(new string(' ', 4 * (indentLevel + 1)));
                        try
                        {
                            sb.AppendLine(p.Name + ": " + p.GetValue(obj, null));
                        }
                        catch
                        {
                            sb.AppendLine(p.Name + ": " + "...");
                        }
                        counter += 1;
                    }
                }
            }
            return sb.ToString();
        }
    }
}
