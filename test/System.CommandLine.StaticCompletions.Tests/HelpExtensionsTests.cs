// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace System.CommandLine.StaticCompletions.Tests;

using System.CommandLine.Help;
using System.CommandLine.StaticCompletions;
using FluentAssertions;
using Xunit;

public class HelpExtensionsTests
{
    [Fact]
    public void HelpOptionOnlyShowsUsefulNames()
    {
        new HelpOption().Names().Should().BeEquivalentTo(["--help", "-h"]);
    }

    [Fact]
    public void OptionNamesListNameThenAliases()
    {
        new CliOption<string>("--name", "-n", "--nombre").Names().Should().Equal(["--name", "-n", "--nombre"]);
    }

    [Fact]
    public void OptionsWithNoAliasesHaveOnlyOneName()
    {
        new CliOption<string>("--name").Names().Should().Equal(["--name"]);
    }

    [Fact]
    public void HeirarchicalOptionsAreFlattened()
    {
        var parentCommand = new CliCommand("parent");
        var childCommand = new CliCommand("child");
        parentCommand.Subcommands.Add(childCommand);
        parentCommand.Options.Add(new CliOption<string>("--parent-global") { Recursive = true });
        parentCommand.Options.Add(new CliOption<string>("--parent-local") { Recursive = false });
        parentCommand.Options.Add(new CliOption<string>("--parent-global-but-hidden") { Recursive = true, Hidden = true });

        childCommand.Options.Add(new CliOption<string>("--child-local"));
        childCommand.Options.Add(new CliOption<string>("--child-hidden") { Hidden = true });

        // note: no parent-local or parent-global-but-hidden options, and no locally hidden options
        childCommand.HierarchicalOptions().Select(c => c.Name).Should().Equal(["--child-local", "--parent-global"]);
    }
}
