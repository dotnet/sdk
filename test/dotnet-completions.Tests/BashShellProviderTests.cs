// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Complete.Tests;

using Microsoft.DotNet.Cli.Completions.Shells;
using Microsoft.DotNet.Cli.MSBuild.Tests;
using System.CommandLine;
using System.CommandLine.Help;

public class HelpExtensionsTests
{
    [Fact]
    public void BashProviderShould()
    {
        var provider = new BashShellProvider();

    }
}
