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

namespace Microsoft.WindowsAzure.Commands.WAPackIaaS.Test.Operations
{
    using Common;
    using Microsoft.WindowsAzure.Commands.Utilities.WAPackIaaS.DataContract;
    using Microsoft.WindowsAzure.Commands.Utilities.WAPackIaaS.WebClient;
    using Mocks;
    using System;
    using Utilities.Common;
    using VisualStudio.TestTools.UnitTesting;
    using Microsoft.WindowsAzure.Commands.Utilities.WAPackIaaS.Operations;
    using Microsoft.WindowsAzure.Commands.Utilities.WAPackIaaS;

    [TestClass]
    public class JobOperationsTests
    { 
        /// <summary>
        /// Tests WaitOnJob with no timeout and a job that completes immediately.
        /// </summary>
        [TestMethod]
        [TestCategory("WAPackIaaS")]
        public void WaitOnJobCompletesImmediately()
        {
            Guid jobId = Guid.NewGuid();

            MockRequestChannel mockChannel = MockRequestChannel.Create();
            mockChannel.AddReturnObject(new Job {Name = "TestJob", ID = jobId, IsCompleted = true});

            var jobOperations = new JobOperations(new WebClientFactory(
                                                      new Subscription(),
                                                      mockChannel));
            DateTime start = DateTime.Now;
            jobOperations.WaitOnJob(jobId);
            Assert.IsTrue((DateTime.Now - start).TotalMilliseconds < 500);
        }

        /// <summary>
        /// Tests WaitOnJob with a timeout where the the Job does not complete before timeout occurs
        /// </summary>
        [TestMethod]
        [TestCategory("WAPackIaaS")]
        public void WaitOnJobTimeoutJobNotFinished()
        {
            Guid jobId = Guid.NewGuid();

            MockRequestChannel mockChannel = MockRequestChannel.Create();
            mockChannel.AddReturnObject(new Job { Name = "TestJob", ID = jobId, IsCompleted = false, Status = "Running" });
            mockChannel.AddReturnObject(new Job { Name = "TestJob", ID = jobId, IsCompleted = false, Status = "Running" });
            mockChannel.AddReturnObject(new Job { Name = "TestJob", ID = jobId, IsCompleted = false, Status = "Running" });
            mockChannel.AddReturnObject(new Job { Name = "TestJob", ID = jobId, IsCompleted = false, Status = "Running" });
            mockChannel.AddReturnObject(new Job { Name = "TestJob", ID = jobId, IsCompleted = false, Status = "Running" });
            mockChannel.AddReturnObject(new Job { Name = "TestJob", ID = jobId, IsCompleted = false, Status = "Running" });
            mockChannel.AddReturnObject(new Job { Name = "TestJob", ID = jobId, IsCompleted = false, Status = "Running" });

            var jobOperations = new JobOperations(new WebClientFactory(
                                                      new Subscription(),
                                                      mockChannel));
            DateTime start = DateTime.Now;
            var result = jobOperations.WaitOnJob(jobId, 6000);
            var diff = (DateTime.Now - start).TotalMilliseconds;
            Assert.IsTrue(diff > 6000);
            Assert.IsTrue(result.jobStatus == JobStatusEnum.OperationTimedOut);
        }

        /// <summary>
        /// Tests WaitOnJob with a timeout where the job completes before the timeout occurs
        /// </summary>
        [TestMethod]
        [TestCategory("WAPackIaaS")]
        public void WaitOnJobTimeoutJobFinished()
        {
            Guid jobId = Guid.NewGuid();

            MockRequestChannel mockChannel = MockRequestChannel.Create();
            mockChannel.AddReturnObject(new Job { Name = "TestJob", ID = jobId, IsCompleted = false, Status = "Running" });
            mockChannel.AddReturnObject(new Job { Name = "TestJob", ID = jobId, IsCompleted = true, Status = "Completed" });

            var jobOperations = new JobOperations(new WebClientFactory(
                                                      new Subscription(),
                                                      mockChannel));
            DateTime start = DateTime.Now;
            var result = jobOperations.WaitOnJob(jobId, 50000);
            var diff = (DateTime.Now - start).TotalMilliseconds;
            Assert.IsTrue(diff < 50000);
            Assert.IsTrue(result.jobStatus == JobStatusEnum.CompletedSuccesfully);
        }
    }
}
