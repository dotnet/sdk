// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Runtime.CompilerServices;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.NET.Publish.Tests
{
    public class GivenThatWeWantToPublishAToolProject : SdkTest
    {

        public static string HostfxrName =
                RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "hostfxr.dll" :
                RuntimeInformation.IsOSPlatform(OSPlatform.Linux) ? "libhostfxr.so" :
                "libhostfxr.dylib";

        public GivenThatWeWantToPublishAToolProject(ITestOutputHelper log) : base(log)
        {
        }

        private TestAsset SetupTestAsset([CallerMemberName] string callingMethod = "")
        {
            TestAsset helloWorldAsset = _testAssetsManager
                .CopyTestAsset("PortableTool", callingMethod)
                .WithSource();


            return helloWorldAsset;
        }

        [Fact]
        // this test verifies that we don't regress the 'normal' publish experience accidentally in the
        // PackTool.targets
        public void It_can_publish_and_has_apphost()
        {
            var testAsset = SetupTestAsset();
            var publishCommand = new PublishCommand(testAsset);

            publishCommand.Execute();

            publishCommand.GetOutputDirectory(targetFramework: ToolsetInfo.CurrentTargetFramework)
                .Should().HaveFile("consoledemo" + Constants.ExeSuffix);
        }

        [Fact]
        // this test verifies that we don't regress the 'normal' publish experience accidentally in the
        // PackTool.targets
        public void It_can_publish_selfcontained_and_has_apphost()
        {
            var testAsset = SetupTestAsset().SetProjProperty("PublishSelfContained", "true");
            var publishCommand = new PublishCommand(testAsset);
            publishCommand.WithWorkingDirectory(testAsset.Path).Execute();
            publishCommand.GetOutputDirectory(targetFramework: ToolsetInfo.CurrentTargetFramework, runtimeIdentifier: System.Runtime.InteropServices.RuntimeInformation.RuntimeIdentifier)
                .Should().HaveFile("consoledemo" + Constants.ExeSuffix)
                .And.HaveFile(HostfxrName);
        }
    }
}
