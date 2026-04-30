// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

public class AspireRootCommandCliTests
{
    [Fact]
    public void Help_RootCommand()
    {
        // --help should not throw and should return null
        var args = new[] { "--help" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.Null(launcher);
    }

    [Fact]
    public void Help_SubCommand()
    {
        // subcommand --help should not throw and should return null
        var args = new[] { "host", "--help" };
        var launcher = AspireLauncher.TryCreate(args);
        Assert.Null(launcher);
    }

    [Fact]
    public void NoArguments()
    {
        // No arguments should return null (error case)
        var args = Array.Empty<string>();
        var launcher = AspireLauncher.TryCreate(args);
        Assert.Null(launcher);
    }
}
