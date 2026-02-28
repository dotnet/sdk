// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Watch.UnitTests
{
    public class ProgramTests_Arguments(ITestOutputHelper output) : DotNetWatchTestBase(output)
    {
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

            App.SuppressVerboseLogging();
            App.Start(testAsset, arguments);

            Assert.Equal(expectedApplicationArgs, await App.AssertOutputLineStartsWith("Arguments = "));
        }

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificFact(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public async Task RunArguments_NoHotReload()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadAppMultiTfm")
                .WithSource();

            App.SuppressVerboseLogging();
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

        //  https://github.com/dotnet/sdk/issues/49665
        [PlatformSpecificFact(TestPlatforms.Any & ~TestPlatforms.OSX)]
        public async Task RunArguments_HotReload()
        {
            var testAsset = TestAssets.CopyTestAsset("WatchHotReloadAppMultiTfm")
                .WithSource();

            App.SuppressVerboseLogging();
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
    }
}
