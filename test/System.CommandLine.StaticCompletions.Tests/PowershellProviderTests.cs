// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace System.CommandLine.StaticCompletions.Tests;

using System.CommandLine.StaticCompletions.Shells;

[TestClass]
public class PowershellProviderTests : VerifyMSTest.VerifyBase
{
    private IShellProvider provider = new PowerShellShellProvider();

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

    [TestMethod]
    public async Task SanitizesSpecialCharactersInHelpText()
    {
        await provider.Verify(new("mycommand") {
            new Option<string>("--curly") { Description = "Text with “curly” and „low“ quotes" },
            new Option<string>("--dollar") { Description = "Text with $dollar sign" },
            new Option<string>("--backtick") { Description = "Text with `backtick` char" },
            new Option<string>("--double") { Description = "Text with \"double\" quote" },
            new Option<string>("--single") { Description = "Text with 'single' quotes" },
            new Command("subcmd", "Subcmd: “curly” „low“ $dollar `backtick` \"double\" and 'single'"),
        }, TestContext);
    }
}
