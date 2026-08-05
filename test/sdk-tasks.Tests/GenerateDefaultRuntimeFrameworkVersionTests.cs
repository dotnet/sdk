// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Build.Tasks;

namespace Microsoft.CoreSdkTasks.Tests
{
    [TestClass]
    public class GenerateDefaultRuntimeFrameworkVersionTests : SdkTest
    {
        [TestMethod]
        [DataRow("3.0.0-rtm", "3.0.0-rtm")]
        [DataRow("3.1.0", "3.1.0")]
        [DataRow("10.3.10", "10.3.0")]
        [DataRow("1.1.10-prerelease", "1.1.0")]
        public void ItGeneratesDefaultVersionBasedOnRuntimePackVersion(string runtimePackVersion, string defaultRuntimeFrameworkVersion)
        {
            var generateTask = new GenerateDefaultRuntimeFrameworkVersion()
            {
                RuntimePackVersion = runtimePackVersion
            };

            generateTask
                .Execute()
                .Should().BeTrue();

            generateTask.DefaultRuntimeFrameworkVersion.Should().Be(defaultRuntimeFrameworkVersion);
        }
    }
}
