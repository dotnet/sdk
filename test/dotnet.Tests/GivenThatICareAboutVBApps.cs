// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tests
{
    [TestClass]
    public class GivenThatICareAboutVBApps : SdkTest
    {
        public GivenThatICareAboutVBApps()
        {
        }


        [TestMethod]
        public void ICanBuildVBApps()
        {
            var testInstance = TestAssetsManager.CopyTestAsset("VBTestApp")
                .WithSource();

            new BuildCommand(testInstance)
                .Execute()
                .Should().Pass();
        }

        [TestMethod]
        public void ICanRunVBApps()
        {
            var testInstance = TestAssetsManager.CopyTestAsset("VBTestApp")
                .WithSource();

            new DotnetCommand(Log)
                .WithWorkingDirectory(testInstance.Path)
                .Execute("run")
                .Should().Pass();
        }

        [TestMethod]
        public void ICanPublicAndRunVBApps()
        {
            var testInstance = TestAssetsManager.CopyTestAsset("VBTestApp")
                .WithSource();

            var publishCommand = new PublishCommand(testInstance);

            publishCommand
                .Execute()
                .Should().Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";
            var outputDll = Path.Combine(
                publishCommand.GetOutputDirectory(configuration: configuration).FullName,
                "VBTestApp.dll");

            new DotnetCommand(Log)
                .Execute(outputDll)
                .Should().Pass()
                         .And.HaveStdOutContaining("Hello World");
        }
    }
}
