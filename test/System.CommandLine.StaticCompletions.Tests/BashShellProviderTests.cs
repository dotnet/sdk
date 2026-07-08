// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace System.CommandLine.StaticCompletions.Tests;

using System.CommandLine.StaticCompletions.Shells;

[TestClass]
public class BashShellProviderTests : VerifyMSTest.VerifyBase
{
    private IShellProvider provider = new BashShellProvider();

    [TestMethod]
    public async Task GenericCompletions()
    {
        await provider.Verify(new("mycommand"), TestContext);
    }

    [TestMethod]
    public async Task SimpleOptionCompletion()
    {
        await provider.Verify(new("mycommand") {
            new Option<string>("--name")
        }, TestContext);
    }

    [TestMethod]
    public async Task SubcommandAndOptionInTopLevelList()
    {
        await provider.Verify(new("mycommand") {
                new Option<string>("--name"),
                new Command("subcommand")
            }, TestContext);
    }

    [TestMethod]
    public async Task NestedSubcommandCompletion()
    {
        await provider.Verify(new("mycommand") {
            new Command("subcommand") {
                new Command("nested")
            }
        }, TestContext);
    }
}
