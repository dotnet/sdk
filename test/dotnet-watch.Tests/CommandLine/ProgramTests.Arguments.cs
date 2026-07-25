// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class ProgramTests_Arguments : DotNetWatchTestBase
{
    [TestMethod]
    [DataRow(new[] { "--no-hot-reload", "run" }, "")]
    [DataRow(new[] { "--no-hot-reload", "run", "args" }, "args")]
    [DataRow(new[] { "--no-hot-reload", "--", "run", "args" }, "run,args")]
    [DataRow(new[] { "--no-hot-reload" }, "")]
    [DataRow(new string[] { }, "")]
    [DataRow(new[] { "run" }, "")]
    [DataRow(new[] { "run", "args" }, "args")]
    [DataRow(new[] { "--", "run", "args" }, "run,args")]
    [DataRow(new[] { "--", "test", "args" }, "test,args")]
    [DataRow(new[] { "--", "build", "args" }, "build,args")]
    [DataRow(new[] { "abc" }, "abc")]
    public async Task Arguments(string[] arguments, string expectedApplicationArgs)
    {
        var testAsset = TestAssets.CopyTestAsset("WatchHotReloadApp", identifier: string.Join(",", arguments))
            .WithSource();

        App.SuppressVerboseLogging();
        App.Start(testAsset, arguments);

        await App.WaitUntilOutputContains($"Arguments = {expectedApplicationArgs}");
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // https://github.com/dotnet/sdk/issues/53061
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

        await App.WaitUntilOutputContains("Arguments = -v");
        await App.WaitUntilOutputContains("AssemblyName = WatchHotReloadAppMultiTfm, Version=1.2.3.4, Culture=neutral, PublicKeyToken=null");
        await App.WaitUntilOutputContains("AssemblyTitle = ' | A=B'\tC | '");
        await App.WaitUntilOutputContains("TFM = .NETCoreApp,Version=v6.0");

        // expected output from build (-v minimal):
        await App.WaitUntilOutputContains("Determining projects to restore...");

        // not expected to find verbose output of dotnet watch
        Assert.DoesNotContain("Working directory:", string.Join(Environment.NewLine, App.Process.Output));
    }

    [TestMethod]
    [OSCondition(ConditionMode.Exclude, OperatingSystems.OSX)] // https://github.com/dotnet/sdk/issues/53061
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

        await App.WaitUntilOutputContains("AssemblyName = WatchHotReloadAppMultiTfm, Version=1.2.3.4, Culture=neutral, PublicKeyToken=null");
        await App.WaitUntilOutputContains("AssemblyTitle = ' | A=B'\tC | '");
        await App.WaitUntilOutputContains("TFM = .NETCoreApp,Version=v6.0");
        await App.WaitUntilOutputContains("Hot reload enabled.");

        // not expected to find verbose output of dotnet watch
        Assert.DoesNotContain("Working directory:", string.Join(Environment.NewLine, App.Process.Output));
    }

    [TestMethod]
    [DataRow("P1", "argP1")]
    [DataRow("P and Q and \"R\"", "argPQR")]
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

        await App.WaitUntilOutputContains($"Arguments: {expectedArgs}");
        await App.WaitUntilOutputContains($"Found named launch profile '{profileName}'.");
        await App.WaitUntilOutputContains("Hot Reload disabled by command line switch.");
    }

    [TestMethod]
    [DataRow("P1", "argP1")]
    [DataRow("P and Q and \"R\"", "argPQR")]
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

        await App.WaitUntilOutputContains($"Arguments: {expectedArgs}");
        await App.WaitUntilOutputContains($"Found named launch profile '{profileName}'.");
    }
}
