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

namespace Microsoft.WindowsAzure.Commands.Test.CloudService.Development.Tests
{
    using Commands.CloudService.Development;
    using Commands.CloudService.Development.Scaffolding;
    using Commands.Utilities.CloudService;
    using Commands.Utilities.Common;
    using Commands.Utilities.Common.XmlSchema.ServiceConfigurationSchema;
    using Commands.Utilities.Common.XmlSchema.ServiceDefinitionSchema;
    using Commands.Utilities.Properties;
    using System;
    using System.IO;
    using System.Linq;
    using System.Management.Automation;
    using Test.Utilities.Common;
    using VisualStudio.TestTools.UnitTesting;
    using ConfigConfigurationSetting = Commands.Utilities.Common.XmlSchema.ServiceConfigurationSchema.ConfigurationSetting;
    using DefinitionConfigurationSetting = Commands.Utilities.Common.XmlSchema.ServiceDefinitionSchema.ConfigurationSetting;
    using TestResources = Test.Utilities.Properties.Resources;

    [TestClass]
    public class EnableAzureMemcacheRoleTests : TestBase
    {
        private MockCommandRuntime mockCommandRuntime;

        private AddAzureNodeWebRoleCommand addNodeWebCmdlet;

        private AddAzureNodeWorkerRoleCommand addNodeWorkerCmdlet;

        private AddAzureCacheWorkerRoleCommand addCacheRoleCmdlet;

        private EnableAzureMemcacheRoleCommand enableCacheCmdlet;

        [TestInitialize]
        public void SetupTest()
        {
            GlobalPathInfo.GlobalSettingsDirectory = Data.AzureSdkAppDir;
            mockCommandRuntime = new MockCommandRuntime();

            enableCacheCmdlet = new EnableAzureMemcacheRoleCommand();
            addCacheRoleCmdlet = new AddAzureCacheWorkerRoleCommand();
            addCacheRoleCmdlet.CommandRuntime = mockCommandRuntime;
            enableCacheCmdlet.CommandRuntime = mockCommandRuntime;
        }

        [TestMethod]
        public void EnableAzureMemcacheRoleProcess()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                string serviceName = "AzureService";
                string rootPath = files.CreateNewService(serviceName);
                string cacheRoleName = "WorkerRole";
                string webRoleName = "WebRole";
                string expectedMessage = string.Format(Resources.EnableMemcacheMessage, webRoleName, cacheRoleName, Resources.MemcacheEndpointPort);

                addNodeWebCmdlet = new AddAzureNodeWebRoleCommand() { RootPath = rootPath, CommandRuntime = mockCommandRuntime, Name = webRoleName };
                addNodeWebCmdlet.ExecuteCmdlet();
                addCacheRoleCmdlet.AddAzureCacheWorkerRoleProcess(cacheRoleName, 1, rootPath);
                mockCommandRuntime.ResetPipelines();
                enableCacheCmdlet.PassThru = true;
                enableCacheCmdlet.CacheRuntimeVersion = "2.2.0";
                enableCacheCmdlet.EnableAzureMemcacheRoleProcess(webRoleName, cacheRoleName, rootPath);

                AssertCachingEnabled(files, serviceName, rootPath, webRoleName, expectedMessage);
            }
        }

        /// <summary>
        /// Verify that enabling cache on worker role will pass.
        /// </summary>
        [TestMethod]
        public void EnableAzureMemcacheRoleProcessOnWorkerRoleSuccess()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                string serviceName = "AzureService";
                string rootPath = files.CreateNewService(serviceName);
                string cacheRoleName = "CacheWorkerRole";
                string workerRoleName = "WorkerRole";
                string expectedMessage = string.Format(Resources.EnableMemcacheMessage, workerRoleName, cacheRoleName, Resources.MemcacheEndpointPort);

                addNodeWorkerCmdlet = new AddAzureNodeWorkerRoleCommand() { RootPath = rootPath, CommandRuntime = mockCommandRuntime, Name = workerRoleName };
                addNodeWorkerCmdlet.ExecuteCmdlet();
                addCacheRoleCmdlet.AddAzureCacheWorkerRoleProcess(cacheRoleName, 1, rootPath);
                mockCommandRuntime.ResetPipelines();
                enableCacheCmdlet.PassThru = true;
                enableCacheCmdlet.EnableAzureMemcacheRoleProcess(workerRoleName, cacheRoleName, rootPath);

                WorkerRole workerRole = Testing.GetWorkerRole(rootPath, workerRoleName);

                AzureAssert.RuntimeUrlAndIdExists(workerRole.Startup.Task, Resources.CacheRuntimeValue);

                AzureAssert.ScaffoldingExists(Path.Combine(files.RootPath, serviceName, workerRoleName), Path.Combine(Resources.CacheScaffolding, Resources.WorkerRole));
                AzureAssert.StartupTaskExists(workerRole.Startup.Task, Resources.CacheStartupCommand);

                AzureAssert.InternalEndpointExists(workerRole.Endpoints.InternalEndpoint,
                    new InternalEndpoint { name = Resources.MemcacheEndpointName, protocol = InternalProtocol.tcp, port = Resources.MemcacheEndpointPort });

                LocalStore localStore = new LocalStore
                {
                    name = Resources.CacheDiagnosticStoreName,
                    cleanOnRoleRecycle = false
                };

                AzureAssert.LocalResourcesLocalStoreExists(localStore, workerRole.LocalResources);

                DefinitionConfigurationSetting diagnosticLevel = new DefinitionConfigurationSetting { name = Resources.CacheClientDiagnosticLevelAssemblyName };
                AzureAssert.ConfigurationSettingExist(diagnosticLevel, workerRole.ConfigurationSettings);

                ConfigConfigurationSetting clientDiagnosticLevel = new ConfigConfigurationSetting { name = Resources.ClientDiagnosticLevelName, value = Resources.ClientDiagnosticLevelValue };
                AzureAssert.ConfigurationSettingExist(clientDiagnosticLevel, Testing.GetCloudRole(rootPath, workerRoleName).ConfigurationSettings);
                AzureAssert.ConfigurationSettingExist(clientDiagnosticLevel, Testing.GetLocalRole(rootPath, workerRoleName).ConfigurationSettings);

                string workerConfigPath = string.Format(@"{0}\{1}\{2}", rootPath, workerRoleName, "web.config");
                string workerCloudConfig = File.ReadAllText(workerConfigPath);
                Assert.IsTrue(workerCloudConfig.Contains("configSections"));
                Assert.IsTrue(workerCloudConfig.Contains("dataCacheClients"));

                Assert.AreEqual<string>(expectedMessage, mockCommandRuntime.VerboseStream[0]);
                Assert.AreEqual<string>(workerRoleName, (mockCommandRuntime.OutputPipeline[0] as PSObject).GetVariableValue<string>(Parameters.RoleName));
            }
        }

        /// <summary>
        /// Verify that enabling cache with non-existing cache worker role will fail.
        /// </summary>
        [TestMethod]
        public void EnableAzureMemcacheRoleProcessCacheRoleDoesNotExistFail()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                string serviceName = "AzureService";
                string rootPath = files.CreateNewService(serviceName);
                string cacheRoleName = "WorkerRole";
                string webRoleName = "WebRole";
                string expected = string.Format(Resources.RoleNotFoundMessage, cacheRoleName);

                addNodeWebCmdlet = new AddAzureNodeWebRoleCommand() { RootPath = rootPath, CommandRuntime = mockCommandRuntime, Name = webRoleName };
                addNodeWebCmdlet.ExecuteCmdlet();

                Testing.AssertThrows<Exception>(() => enableCacheCmdlet.EnableAzureMemcacheRoleProcess(webRoleName, cacheRoleName, rootPath));
            }
        }

        /// <summary>
        /// Verify that enabling cache with non-existing role to enable on will fail.
        /// </summary>
        [TestMethod]
        public void EnableAzureMemcacheRoleProcessRoleDoesNotExistFail()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                string serviceName = "AzureService";
                string rootPath = files.CreateNewService(serviceName);
                string cacheRoleName = "WorkerRole";
                string webRoleName = "WebRole";
                string expected = string.Format(Resources.RoleNotFoundMessage, webRoleName);

                addCacheRoleCmdlet.AddAzureCacheWorkerRoleProcess(cacheRoleName, 1, rootPath);

                Testing.AssertThrows<Exception>(() => enableCacheCmdlet.EnableAzureMemcacheRoleProcess(webRoleName, cacheRoleName, rootPath));
            }
        }

        /// <summary>
        /// Verify that enabling cache using same cache worker role on role with cache will fail.
        /// </summary>
        [TestMethod]
        public void EnableAzureMemcacheRoleProcessAlreadyEnabledFail()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                string serviceName = "AzureService";
                string rootPath = files.CreateNewService(serviceName);
                string cacheRoleName = "WorkerRole";
                string webRoleName = "WebRole";
                string expected = string.Format(Resources.CacheAlreadyEnabledMessage, webRoleName);

                addNodeWebCmdlet = new AddAzureNodeWebRoleCommand() { RootPath = rootPath, CommandRuntime = mockCommandRuntime, Name = webRoleName };
                addNodeWebCmdlet.ExecuteCmdlet();
                addCacheRoleCmdlet.AddAzureCacheWorkerRoleProcess(cacheRoleName, 1, rootPath);
                enableCacheCmdlet.EnableAzureMemcacheRoleProcess(webRoleName, cacheRoleName, rootPath);

                Testing.AssertThrows<Exception>(() => enableCacheCmdlet.EnableAzureMemcacheRoleProcess(webRoleName, cacheRoleName, rootPath));
            }
        }

        /// <summary>
        /// Verify that enabling cache using different cache worker role on role with cache will fail.
        /// </summary>
        [TestMethod]
        public void EnableAzureMemcacheRoleProcessAlreadyEnabledNewCacheRoleFail()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                string serviceName = "AzureService";
                string rootPath = files.CreateNewService(serviceName);
                string cacheRoleName = "WorkerRole";
                string newCacheRoleName = "NewCacheWorkerRole";
                string webRoleName = "WebRole";
                string expected = string.Format(Resources.CacheAlreadyEnabledMessage, webRoleName);

                addNodeWebCmdlet = new AddAzureNodeWebRoleCommand() { RootPath = rootPath, CommandRuntime = mockCommandRuntime, Name = webRoleName };
                addNodeWebCmdlet.ExecuteCmdlet();
                addCacheRoleCmdlet.AddAzureCacheWorkerRoleProcess(cacheRoleName, 1, rootPath);
                enableCacheCmdlet.EnableAzureMemcacheRoleProcess(webRoleName, cacheRoleName, rootPath);
                addCacheRoleCmdlet.AddAzureCacheWorkerRoleProcess(newCacheRoleName, 1, rootPath);

                Testing.AssertThrows<Exception>(() => enableCacheCmdlet.EnableAzureMemcacheRoleProcess(webRoleName, cacheRoleName, rootPath));
            }
        }

        /// <summary>
        /// Verify that enabling cache using non-cache worker role will fail.
        /// </summary>
        [TestMethod]
        public void EnableAzureMemcacheRoleProcessUsingNonCacheWorkerRole()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                string serviceName = "AzureService";
                string rootPath = files.CreateNewService(serviceName);
                string workerRoleName = "WorkerRole";
                string webRoleName = "WebRole";
                string expected = string.Format(Resources.NotCacheWorkerRole, workerRoleName);

                addNodeWebCmdlet = new AddAzureNodeWebRoleCommand() { RootPath = rootPath, CommandRuntime = mockCommandRuntime, Name = webRoleName };
                addNodeWebCmdlet.ExecuteCmdlet();
                addNodeWorkerCmdlet = new AddAzureNodeWorkerRoleCommand() { RootPath = rootPath, CommandRuntime = mockCommandRuntime, Name = workerRoleName };
                addNodeWorkerCmdlet.ExecuteCmdlet();

                Testing.AssertThrows<Exception>(() => enableCacheCmdlet.EnableAzureMemcacheRoleProcess(webRoleName, workerRoleName, rootPath));
            }
        }

        [TestMethod]
        public void EnableAzureMemcacheRoleProcessWithDefaultRoleName()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                string originalDirectory = Directory.GetCurrentDirectory();
                string serviceName = "AzureService";
                string rootPath = files.CreateNewService(serviceName);
                string webRoleName = "WebRole";
                string cacheRoleName = "WorkerRole";
                string expectedMessage = string.Format(Resources.EnableMemcacheMessage, webRoleName, cacheRoleName, Resources.MemcacheEndpointPort);

                addNodeWebCmdlet = new AddAzureNodeWebRoleCommand() { RootPath = rootPath, CommandRuntime = mockCommandRuntime, Name = webRoleName };
                addNodeWebCmdlet.ExecuteCmdlet();
                Directory.SetCurrentDirectory(Path.Combine(rootPath, webRoleName));
                addCacheRoleCmdlet.AddAzureCacheWorkerRoleProcess(cacheRoleName, 1, rootPath);
                mockCommandRuntime.ResetPipelines();
                enableCacheCmdlet.PassThru = true;
                enableCacheCmdlet.CacheRuntimeVersion = "2.2.0";
                enableCacheCmdlet.RoleName = string.Empty;
                enableCacheCmdlet.CacheWorkerRoleName = cacheRoleName;
                enableCacheCmdlet.ExecuteCmdlet();

                AssertCachingEnabled(files, serviceName, rootPath, webRoleName, expectedMessage);
                Directory.SetCurrentDirectory(originalDirectory);
            }
        }

        private void AssertCachingEnabled(
            FileSystemHelper files,
            string serviceName,
            string rootPath,
            string webRoleName,
            string expectedMessage)
        {
            WebRole webRole = Testing.GetWebRole(rootPath, webRoleName);
            RoleSettings roleSettings = Testing.GetCloudRole(rootPath, webRoleName);

            AzureAssert.RuntimeUrlAndIdExists(webRole.Startup.Task, Resources.CacheRuntimeValue);

            Assert.AreEqual<string>(Resources.CacheRuntimeVersionKey, webRole.Startup.Task[0].Environment[0].name);
            Assert.AreEqual<string>(enableCacheCmdlet.CacheRuntimeVersion, webRole.Startup.Task[0].Environment[0].value);
            
            Assert.AreEqual<string>(Resources.EmulatedKey, webRole.Startup.Task[2].Environment[0].name);
            Assert.AreEqual<string>("/RoleEnvironment/Deployment/@emulated", webRole.Startup.Task[2].Environment[0].RoleInstanceValue.xpath);
            
            Assert.AreEqual<string>(Resources.CacheRuntimeUrl, webRole.Startup.Task[2].Environment[1].name);
            Assert.AreEqual<string>(TestResources.CacheRuntimeUrl, webRole.Startup.Task[2].Environment[1].value);
            Assert.AreEqual(1, webRole.Startup.Task.Count(t => t.commandLine.Equals(Resources.CacheStartupCommand)));
            

            AzureAssert.ScaffoldingExists(Path.Combine(files.RootPath, serviceName, webRoleName), Path.Combine(Resources.CacheScaffolding, Resources.WebRole));
            AzureAssert.StartupTaskExists(webRole.Startup.Task, Resources.CacheStartupCommand);

            AzureAssert.InternalEndpointExists(webRole.Endpoints.InternalEndpoint,
                new InternalEndpoint { name = Resources.MemcacheEndpointName, protocol = InternalProtocol.tcp, port = Resources.MemcacheEndpointPort });

            LocalStore localStore = new LocalStore
            {
                name = Resources.CacheDiagnosticStoreName,
                cleanOnRoleRecycle = false
            };

            AzureAssert.LocalResourcesLocalStoreExists(localStore, webRole.LocalResources);

            DefinitionConfigurationSetting diagnosticLevel = new DefinitionConfigurationSetting { name = Resources.CacheClientDiagnosticLevelAssemblyName };
            AzureAssert.ConfigurationSettingExist(diagnosticLevel, webRole.ConfigurationSettings);

            ConfigConfigurationSetting clientDiagnosticLevel = new ConfigConfigurationSetting { name = Resources.ClientDiagnosticLevelName, value = Resources.ClientDiagnosticLevelValue };
            AzureAssert.ConfigurationSettingExist(clientDiagnosticLevel, roleSettings.ConfigurationSettings);

            AssertWebConfig(string.Format(@"{0}\{1}\{2}", rootPath, webRoleName, Resources.WebCloudConfig));
            AssertWebConfig(string.Format(@"{0}\{1}\{2}", rootPath, webRoleName, Resources.WebConfigTemplateFileName));

            Assert.AreEqual<string>(expectedMessage, mockCommandRuntime.VerboseStream[0]);
            Assert.AreEqual<string>(webRoleName, (mockCommandRuntime.OutputPipeline[0] as PSObject).GetVariableValue<string>(Parameters.RoleName));
        }

        private static void AssertWebConfig(string webCloudConfigPath)
        {
            string webCloudCloudConfigContents = File.ReadAllText(webCloudConfigPath);
            Assert.IsTrue(webCloudCloudConfigContents.Contains("configSections"));
            Assert.IsTrue(webCloudCloudConfigContents.Contains("dataCacheClients"));
        }

        /// <summary>
        /// Verify that enabling cache with non-existing cache worker role will fail.
        /// </summary>
        [TestMethod]
        public void EnableAzureMemcacheRoleProcessOnCacheWorkerRoleFail()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                string serviceName = "AzureService";
                string rootPath = files.CreateNewService(serviceName);
                string cacheRoleName = "WorkerRole";
                string expected = string.Format(Resources.InvalidCacheRoleName, cacheRoleName);

                addCacheRoleCmdlet.AddAzureCacheWorkerRoleProcess(cacheRoleName, 1, rootPath);

                Testing.AssertThrows<Exception>(() => enableCacheCmdlet.EnableAzureMemcacheRoleProcess(cacheRoleName, cacheRoleName, rootPath));
            }
        }

        [TestMethod]
        public void EnableAzureMemcacheWithoutCacheWorkerRoleName()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                string serviceName = "AzureService";
                string rootPath = files.CreateNewService(serviceName);
                string cacheRoleName = "WorkerRole";
                string webRoleName = "WebRole";
                string expectedMessage = string.Format(Resources.EnableMemcacheMessage, webRoleName, cacheRoleName, Resources.MemcacheEndpointPort);

                addNodeWebCmdlet = new AddAzureNodeWebRoleCommand() { RootPath = rootPath, CommandRuntime = mockCommandRuntime, Name = webRoleName };
                addNodeWebCmdlet.ExecuteCmdlet();
                addCacheRoleCmdlet.AddAzureCacheWorkerRoleProcess(cacheRoleName, 1, rootPath);
                mockCommandRuntime.ResetPipelines();
                enableCacheCmdlet.PassThru = true;
                enableCacheCmdlet.CacheRuntimeVersion = "2.2.0";
                enableCacheCmdlet.EnableAzureMemcacheRoleProcess(webRoleName, null, rootPath);

                AssertCachingEnabled(files, serviceName, rootPath, webRoleName, expectedMessage);
            }
        }

        [TestMethod]
        public void EnableAzureMemcacheWithoutCacheWorkerRoleNameAndServiceHasMultipleWorkerRoles()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                string serviceName = "AzureService";
                string rootPath = files.CreateNewService(serviceName);
                string cacheRoleName = "CacheWorkerRole";
                string webRoleName = "WebRole";
                string expectedMessage = string.Format(Resources.EnableMemcacheMessage, webRoleName, cacheRoleName, Resources.MemcacheEndpointPort);

                addNodeWebCmdlet = new AddAzureNodeWebRoleCommand() { RootPath = rootPath, CommandRuntime = mockCommandRuntime, Name = webRoleName };
                addNodeWebCmdlet.ExecuteCmdlet();
                addNodeWorkerCmdlet = new AddAzureNodeWorkerRoleCommand() { RootPath = rootPath, CommandRuntime = mockCommandRuntime, Name = "WorkerRole" };
                addNodeWorkerCmdlet.ExecuteCmdlet();
                addCacheRoleCmdlet.AddAzureCacheWorkerRoleProcess(cacheRoleName, 1, rootPath);
                mockCommandRuntime.ResetPipelines();
                enableCacheCmdlet.PassThru = true;
                enableCacheCmdlet.CacheRuntimeVersion = "2.2.0";
                enableCacheCmdlet.EnableAzureMemcacheRoleProcess(webRoleName, null, rootPath);

                AssertCachingEnabled(files, serviceName, rootPath, webRoleName, expectedMessage);
            }
        }

        [TestMethod]
        public void EnableAzureMemcacheWithNoCacheWorkerRolesFail()
        {
            using (FileSystemHelper files = new FileSystemHelper(this))
            {
                string serviceName = "AzureService";
                string rootPath = files.CreateNewService(serviceName);
                string webRoleName = "WebRole";
                string expectedMessage = string.Format(Resources.NoCacheWorkerRoles);

                addNodeWebCmdlet = new AddAzureNodeWebRoleCommand() { RootPath = rootPath, CommandRuntime = mockCommandRuntime, Name = webRoleName };
                addNodeWebCmdlet.ExecuteCmdlet();
                addNodeWorkerCmdlet = new AddAzureNodeWorkerRoleCommand() { RootPath = rootPath, CommandRuntime = mockCommandRuntime, Name = "WorkerRole" };
                addNodeWorkerCmdlet.ExecuteCmdlet();
                mockCommandRuntime.ResetPipelines();
                enableCacheCmdlet.PassThru = true;
                enableCacheCmdlet.CacheRuntimeVersion = "2.2.0";

                Testing.AssertThrows<Exception>(() => enableCacheCmdlet.EnableAzureMemcacheRoleProcess(webRoleName, null, rootPath), expectedMessage);
            }
        }
    }
}
