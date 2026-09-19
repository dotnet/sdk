// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Watch.UnitTests;

[TestClass]
public class ProgramTests_HostArguments : DotNetWatchTestBase
{
    [TestMethod]
    [DataRow(new[] { "--no-hot-reload", "--", "run", "args" }, "Argument Specified in Props,run,args")]
    [DataRow(new[] { "--", "run", "args" }, "Argument Specified in Props,run,args")]
    // if arguments specified on command line the ones from launch profile are ignored
    [DataRow(new[] { "-lp", "P1", "--", "run", "args" },"Argument Specified in Props,run,args")]
    // arguments specified in build file override arguments in launch profile
    [DataRow(new[] { "-lp", "P1" }, "Argument Specified in Props")]
    public async Task Arguments_HostArguments(string[] arguments, string expectedApplicationArgs)
    {
        var testAsset = TestAssets.CopyTestAsset("WatchHotReloadAppCustomHost", identifier: string.Join(",", arguments))
            .WithSource();

        App.Start(testAsset, arguments);

        AssertEx.Equal(expectedApplicationArgs, await App.AssertOutputLineStartsWith("Arguments = "));
    }
}
