﻿// ----------------------------------------------------------------------------------
//
// Copyright Microsoft Corporation
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
// ----------------------------------------------------------------------------------

namespace Microsoft.WindowsAzure.Commands.WAPackIaaS.FunctionalTest
{
    using Microsoft.VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Commands.Utilities.Properties;
    using System;
    using System.Collections.Generic;
    using System.Linq;

    [TestClass]
    public class GetWAPackVMSizeProfileTests : CmdletTestBase
    {
        public const string cmdletName = "Get-WAPackVMSizeProfile";

        [TestMethod]
        [TestCategory("WAPackIaaS")]
        public void GetWAPackVMSizeProfileWithNoParam()
        {
            var allVMSizeProfiles = this.InvokeCmdlet(cmdletName, null);
            Assert.IsTrue(allVMSizeProfiles.Any());
        }

        [TestMethod]
        [TestCategory("WAPackIaaS")]
        public void GetWAPackVMSizeProfileFromName()
        {
            string expectedVMSizeProfileName = WAPackConfigurationFactory.VMSizeProfileName;
            var inputParams = new Dictionary<string, object>()
            {
                {"Name", expectedVMSizeProfileName}
            };
            var vmSizeProfileFromName = this.InvokeCmdlet(cmdletName, inputParams);

            Assert.AreEqual(1, vmSizeProfileFromName.Count);
            var actualVMSizeProfileName = vmSizeProfileFromName.First().Properties["Name"].Value;

            Assert.AreEqual(expectedVMSizeProfileName, actualVMSizeProfileName);
        }

        [TestMethod]
        [TestCategory("WAPackIaaS")]
        public void GetWAPackVMSizeProfileFromIdAndName()
        {
            string expectedVMSizeProfileName = WAPackConfigurationFactory.VMSizeProfileName;
            var inputParams = new Dictionary<string, object>()
            {
                {"Name", expectedVMSizeProfileName}
            };
            var vmSizeProfileFromName = this.InvokeCmdlet(cmdletName, inputParams);

            Assert.AreEqual(1, vmSizeProfileFromName.Count);

            var expectedvmSizeProfileId = vmSizeProfileFromName.First().Properties["Id"].Value;

            inputParams = new Dictionary<string, object>()
            {
                {"Id", expectedvmSizeProfileId}
            };
            var expectedvmSizeProfile = this.InvokeCmdlet(cmdletName, inputParams);

            var actualvmSizeProfileFromId = expectedvmSizeProfile.First().Properties["Id"].Value;
            Assert.AreEqual(expectedvmSizeProfileId, actualvmSizeProfileFromId);
        }

        [TestMethod]
        [TestCategory("Negative")]
        [TestCategory("WAPackIaaS")]
        public void GetWAPackVMSizeProfileByNameDoesNotExist()
        {
            string expectedVMSizeProfileName = "VMSizeProfileDoesNotExist";
            var inputParams = new Dictionary<string, object>()
            {
                {"Name", expectedVMSizeProfileName}
            };
            var vmSizeProfileFromName = this.InvokeCmdlet(cmdletName, inputParams);

            Assert.AreEqual(0, vmSizeProfileFromName.Count);
        }

        [TestMethod]
        [TestCategory("Negative")]
        [TestCategory("WAPackIaaS")]
        public void GetWAPackVMSizeProfileByIdDoesNotExist()
        {
            var expectedVmId = Guid.NewGuid().ToString();
            var expectedError = string.Format(Resources.ResourceNotFound, expectedVmId);
            var inputParams = new Dictionary<string, object>()
            {
                {"Id", expectedVmId}
            };
            var vmFromName = this.InvokeCmdlet(cmdletName, inputParams, expectedError);
            Assert.AreEqual(0, vmFromName.Count);
        }
    }
}
