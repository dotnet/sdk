// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class SubcommandTests(ITestOutputHelper output) : DotNetWatchTestBase(output)
    {
        [Fact]
        public async Task TestCommand()
        {
            var testAsset = TestAssets.CopyTestAsset("XunitCore")
                .WithSource();

            App.Start(testAsset, ["--verbose", "test", "--list-tests", "/p:VSTestUseMSBuildOutput=false"]);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForFileChangeBeforeRestarting);

            await App.WaitUntilOutputContains("The following Tests are available:");
            await App.WaitUntilOutputContains("    TestNamespace.VSTestXunitTests.VSTestXunitPassTest");
            App.Process.ClearOutput();

            // update file:
            var testFile = Path.Combine(testAsset.Path, "UnitTest1.cs");
            var content = File.ReadAllText(testFile, Encoding.UTF8);
            File.WriteAllText(testFile, content.Replace("VSTestXunitPassTest", "VSTestXunitPassTest2"), Encoding.UTF8);

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForFileChangeBeforeRestarting);

            await App.WaitUntilOutputContains("The following Tests are available:");
            await App.WaitUntilOutputContains("    TestNamespace.VSTestXunitTests.VSTestXunitPassTest2");
        }

        [Fact]
        public async Task TestCommand_MultiTargeting()
        {
            var testAsset = TestAssets.CopyTestAsset("XunitMulti")
                .WithSource();

            App.Start(testAsset, ["--verbose", "test", "--framework", ToolsetInfo.CurrentTargetFramework, "--list-tests", "/p:VSTestUseMSBuildOutput=false"]);

            await App.AssertOutputLineEquals("The following Tests are available:");
            await App.AssertOutputLineEquals("    TestNamespace.VSTestXunitTests.VSTestXunitFailTestNetCoreApp");
        }

        [Fact]
        public async Task BuildCommand()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchNoDepsApp")
                .WithSource();

            App.Start(testAsset, ["--verbose", "--property", "TestProperty=123", "build", "/t:TestTarget"]);

            await App.WaitUntilOutputContains(MessageDescriptor.CommandDoesNotSupportHotReload.GetMessage("build"));
            await App.WaitUntilOutputContains("warning : The value of property is '123'");

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForFileChangeBeforeRestarting);

            // evaluation affected by -c option:
            Assert.Contains("TestProperty", App.Process.Output.Single(line => line.Contains("/t:GenerateWatchList")));
        }

        [Fact]
        public async Task MSBuildCommand()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchNoDepsApp")
                .WithSource();

            App.Start(testAsset, ["--verbose", "/p:TestProperty=123", "msbuild", "/t:TestTarget"]);

            await App.WaitUntilOutputContains(MessageDescriptor.CommandDoesNotSupportHotReload.GetMessage("msbuild"));
            await App.WaitUntilOutputContains("warning : The value of property is '123'");

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForFileChangeBeforeRestarting);

            // TestProperty is not passed to evaluation since msbuild command doesn't include it in forward options:
            Assert.DoesNotContain("TestProperty", App.Process.Output.Single(line => line.Contains("/t:GenerateWatchList")));
        }

        [Fact]
        public async Task PackCommand()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchNoDepsApp")
                .WithSource();

            App.Start(testAsset, ["--verbose", "pack", "-c", "Release"]);

            var packagePath = Path.Combine(testAsset.Path, "bin", "Release", "WatchNoDepsApp.1.0.0.nupkg");

            await App.WaitUntilOutputContains(MessageDescriptor.CommandDoesNotSupportHotReload.GetMessage("pack"));
            await App.WaitUntilOutputContains($"Successfully created package '{packagePath}'");

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForFileChangeBeforeRestarting);

            // evaluation affected by -c option:
            Assert.Contains("-property:Configuration=Release", App.Process.Output.Single(line => line.Contains("/t:GenerateWatchList")));
        }

        [Fact]
        public async Task PublishCommand()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchNoDepsApp")
                .WithSource();

            App.Start(testAsset, ["--verbose", "publish", "-c", "Release"]);
            
            await App.WaitUntilOutputContains(MessageDescriptor.CommandDoesNotSupportHotReload.GetMessage("publish"));
            await App.WaitUntilOutputContains(Path.Combine("Release", ToolsetInfo.CurrentTargetFramework, "publish"));

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForFileChangeBeforeRestarting);

            // evaluation affected by -c option:
            Assert.Contains("-property:Configuration=Release", App.Process.Output.Single(line => line.Contains("/t:GenerateWatchList")));
        }

        [Fact]
        public async Task FormatCommand()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchNoDepsApp")
                .WithSource();

            App.SuppressVerboseLogging();
            App.Start(testAsset, ["--verbose", "format", "--verbosity", "detailed"]);

            await App.WaitUntilOutputContains(MessageDescriptor.CommandDoesNotSupportHotReload.GetMessage("format"));
            await App.WaitUntilOutputContains("format --verbosity detailed");
            await App.WaitUntilOutputContains("Format complete in");

            await App.WaitUntilOutputContains(MessageDescriptor.WaitingForFileChangeBeforeRestarting);
        }
    }
}
