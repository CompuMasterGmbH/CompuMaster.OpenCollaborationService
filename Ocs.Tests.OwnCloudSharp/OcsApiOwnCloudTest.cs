using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace CompuMaster.Ocs.OwnCloudSharpTests
{
    public class OcsApiOwnCloudTest : OcsApiFeaturesTestBase
    {
        public OcsApiOwnCloudTest() : base(new CompuMaster.Ocs.Test.SettingsOwnCloudAdminUser())
        {
            //nothing to do, here
        }
    }
}
