// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.Testing;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher.Tests
{
    public class ProgramTests : DotNetWatchTestBase
    {
        public ProgramTests(ITestOutputHelper logger)
            : base(logger)
        {
        }

        [Fact]
        public async Task ConsoleCancelKey()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchKitchenSink")
                .WithSource();

            var console = new TestConsole(Logger);
            var reporter = new TestReporter(Logger);

            var watching = reporter.RegisterSemaphore(MessageDescriptor.WatchingWithHotReload);
            var shutdownRequested = reporter.RegisterSemaphore(MessageDescriptor.ShutdownRequested);

            var program = Program.TryCreate(
                TestOptions.GetCommandLineOptions(["--verbose"]),
                console,
                TestOptions.GetEnvironmentOptions(workingDirectory: testAsset.Path, TestContext.Current.ToolsetUnderTest.DotNetHostPath),
                reporter,
                out var errorCode);

            Assert.Equal(0, errorCode);
            Assert.NotNull(program);

            var run = program.RunAsync();

            await watching.WaitAsync();

            console.PressCancelKey();

            var exitCode = await run;
            Assert.Equal(0, exitCode);

            await shutdownRequested.WaitAsync();
        }

        [Theory]
        [InlineData(new[] { "--no-hot-reload", "run" }, "")]
        [InlineData(new[] { "--no-hot-reload", "run", "args" }, "args")]
        [InlineData(new[] { "--no-hot-reload", "--", "run", "args" }, "run,args")]
        [InlineData(new[] { "--no-hot-reload" }, "")]
        [InlineData(new string[] { }, "")]
        [InlineData(new[] { "run" }, "")]
        [InlineData(new[] { "run", "args" }, "args")]
        [InlineData(new[] { "--", "run", "args" }, "run,args")]
        [InlineData(new[] { "--", "test", "args" }, "test,args")]
        [InlineData(new[] { "--", "build", "args" }, "build,args")]
        [InlineData(new[] { "abc" }, "abc")]
        public async Task Arguments(string[] arguments, string expectedApplicationArgs)
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp", identifier: string.Join(",", arguments))
                .WithSource();

            App.Start(testAsset, arguments);

            Assert.Equal(expectedApplicationArgs, await App.AssertOutputLineStartsWith("Arguments = "));
        }

        [Theory]
        [InlineData(new[] { "--no-hot-reload", "--", "run", "args" }, "Argument Specified in Props,run,args")]
        [InlineData(new[] { "--", "run", "args" }, "Argument Specified in Props,run,args")]
        // if arguments specified on command line the ones from launch profile are ignored
        [InlineData(new[] { "-lp", "P1", "--", "run", "args" },"Argument Specified in Props,run,args")]
        // arguments specified in build file override arguments in launch profile
        [InlineData(new[] { "-lp", "P1" }, "Argument Specified in Props")]
        public async Task Arguments_HostArguments(string[] arguments, string expectedApplicationArgs)
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadAppCustomHost", identifier: string.Join(",", arguments))
                .WithSource();

            App.Start(testAsset, arguments);

            AssertEx.Equal(expectedApplicationArgs, await App.AssertOutputLineStartsWith("Arguments = "));
        }

        [Fact]
        public async Task RunArguments_NoHotReload()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadAppMultiTfm")
                .WithSource();

            App.DotnetWatchArgs.Clear();
            App.Start(testAsset, arguments:
            [
                "--no-hot-reload",
                "run",
                "-f",
                "net6.0",
                "--property:AssemblyVersion=1.2.3.4",
                "--property",
                "AssemblyTitle= | A=B'\tC | ",
                "-v",
                "minimal",
                "--",         // the following args are application args
                "-v",         
            ]);

            Assert.Equal("-v", await App.AssertOutputLineStartsWith("Arguments = "));
            Assert.Equal("WatchHotReloadAppMultiTfm, Version=1.2.3.4, Culture=neutral, PublicKeyToken=null", await App.AssertOutputLineStartsWith("AssemblyName = "));
            Assert.Equal("' | A=B'\tC | '", await App.AssertOutputLineStartsWith("AssemblyTitle = "));
            Assert.Equal(".NETCoreApp,Version=v6.0", await App.AssertOutputLineStartsWith("TFM = "));

            // expected output from build (-v minimal):
            Assert.Contains(App.Process.Output, l => l.Contains("Determining projects to restore..."));

            // not expected to find verbose output of dotnet watch
            Assert.DoesNotContain(App.Process.Output, l => l.Contains("Working directory:"));
        }

        [Fact]
        public async Task RunArguments_HotReload()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadAppMultiTfm")
                .WithSource();

            App.DotnetWatchArgs.Clear();
            App.Start(testAsset, arguments:
            [
                "run",
                "-f",         // dotnet watch does not recognize this arg -> dotnet run arg
                "net6.0",
                "--property",
                "AssemblyVersion=1.2.3.4",
                "--property",
                "AssemblyTitle= | A=B'\tC | ",
                "--",         // the following args are not dotnet run args
                "-v",         // dotnet build argument
                "minimal"
            ]);

            Assert.Equal("WatchHotReloadAppMultiTfm, Version=1.2.3.4, Culture=neutral, PublicKeyToken=null", await App.AssertOutputLineStartsWith("AssemblyName = "));
            Assert.Equal("' | A=B'\tC | '", await App.AssertOutputLineStartsWith("AssemblyTitle = "));
            Assert.Equal(".NETCoreApp,Version=v6.0", await App.AssertOutputLineStartsWith("TFM = "));

            // not expected to find verbose output of dotnet watch
            Assert.DoesNotContain(App.Process.Output, l => l.Contains("Working directory:"));

            Assert.Contains(App.Process.Output, l => l.Contains("Hot reload enabled."));
        }

        [Theory]
        [InlineData("P1", "argP1")]
        [InlineData("P and Q and \"R\"", "argPQR")]
        public async Task ArgumentsFromLaunchSettings_Watch(string profileName, string expectedArgs)
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppWithLaunchSettings", identifier: profileName)
                .WithSource();

            App.Start(testAsset, arguments: new[]
            {
                "--verbose",
                "--no-hot-reload",
                "-lp",
                profileName
            });

            Assert.Equal(expectedArgs, await App.AssertOutputLineStartsWith("Arguments: "));

            Assert.Contains(App.Process.Output, l => l.Contains($"Found named launch profile '{profileName}'."));
            Assert.Contains(App.Process.Output, l => l.Contains("Hot Reload disabled by command line switch."));
        }

        [Theory]
        [InlineData("P1", "argP1")]
        [InlineData("P and Q and \"R\"", "argPQR")]
        public async Task ArgumentsFromLaunchSettings_HotReload(string profileName, string expectedArgs)
        {
            var testAsset = TestAssets.CopyTestAsset("WatchAppWithLaunchSettings", identifier: profileName)
                .WithSource();

            App.Start(testAsset, arguments: new[]
            {
                "--verbose",
                "-lp",
                profileName
            });

            Assert.Equal(expectedArgs, await App.AssertOutputLineStartsWith("Arguments: "));

            Assert.Contains(App.Process.Output, l => l.Contains($"Found named launch profile '{profileName}'."));
        }

        [Fact]
        public async Task TestCommand()
        {
            var testAsset = TestAssets.CopyTestAsset("XunitCore")
                .WithSource();

            App.Start(testAsset, ["--verbose", "test", "--list-tests", "/p:VSTestUseMSBuildOutput=false"]);

            await App.AssertOutputLineEquals("The following Tests are available:");
            await App.AssertOutputLineEquals("    TestNamespace.VSTestXunitTests.VSTestXunitPassTest");

            // update file:
            var testFile = Path.Combine(testAsset.Path, "UnitTest1.cs");
            var content = File.ReadAllText(testFile, Encoding.UTF8);
            File.WriteAllText(testFile, content.Replace("VSTestXunitPassTest", "VSTestXunitPassTest2"), Encoding.UTF8);

            await App.AssertOutputLineEquals("The following Tests are available:");
            await App.AssertOutputLineEquals("    TestNamespace.VSTestXunitTests.VSTestXunitPassTest2");
        }

        [Fact]
        public async Task TestCommand_MultiTargeting()
        {
            var testAsset = TestAssets.CopyTestAsset("XunitMulti")
                .WithSource();

            App.Start(testAsset, ["--verbose", "--framework", ToolsetInfo.CurrentTargetFramework, "test", "--list-tests", "/p:VSTestUseMSBuildOutput=false"]);

            await App.AssertOutputLineEquals("The following Tests are available:");
            await App.AssertOutputLineEquals("    TestNamespace.VSTestXunitTests.VSTestXunitFailTestNetCoreApp");
        }

        [Fact]
        public async Task BuildCommand()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchNoDepsApp")
                .WithSource();

            App.Start(testAsset, ["--verbose", "--property", "TestProperty=123", "build", "/t:TestTarget"]);

            await App.AssertOutputLine(line => line.Contains("warning : The value of property is '123'", StringComparison.Ordinal));
        }
    }
}
