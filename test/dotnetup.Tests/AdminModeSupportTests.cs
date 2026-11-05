// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class AdminModeSupportTests
{
    [TestMethod]
    public void Unsupported_AllowsNothing()
    {
        var support = AdminModeSupport.Unsupported;

        support.IsSupported.Should().BeFalse();
        support.AllowsAnySymbol.Should().BeFalse();
        support.AllowedSymbols.Should().BeEmpty();
        support.Allows(CommonOptions.UntrackedOption).Should().BeFalse();
    }

    [TestMethod]
    public void AnySymbol_AllowsEverySymbol()
    {
        var support = AdminModeSupport.AnySymbol;

        support.IsSupported.Should().BeTrue();
        support.AllowsAnySymbol.Should().BeTrue();
        support.Allows(new Option<bool>("--anything")).Should().BeTrue();
    }

    [TestMethod]
    public void Allow_AllowsOnlyListedSymbolsByReference()
    {
        var allowed = new Option<bool>("--allowed");
        var sameName = new Option<bool>("--allowed");
        var argument = new Argument<string>("value");

        var support = AdminModeSupport.Allow(allowed, argument);

        support.IsSupported.Should().BeTrue();
        support.AllowsAnySymbol.Should().BeFalse();
        support.AllowedSymbols.Should().HaveCount(2);
        support.Allows(allowed).Should().BeTrue();
        support.Allows(argument).Should().BeTrue();
        support.Allows(sameName).Should().BeFalse();
    }

    [TestMethod]
    public void Allow_WithNoSymbols_SupportsCommandOnly()
    {
        var support = AdminModeSupport.Allow();

        support.IsSupported.Should().BeTrue();
        support.Allows(CommonOptions.UntrackedOption).Should().BeFalse();
    }

    [TestMethod]
    public void InstallCommand_IsUnsupportedAsRootCommand()
    {
        var parseResult = Parser.Parse([]);
        var command = new SdkInstallCommand(parseResult);

        command.GetAdminModeSupport(parseResult.RootCommandResult.Command).IsSupported.Should().BeFalse();
    }

    [TestMethod]
    public void InstallCommand_AllowsUntrackedForSdkInstall()
    {
        var parseResult = Parser.Parse(["sdk", "install", "--untracked"]);
        var command = new SdkInstallCommand(parseResult);

        var support = command.GetAdminModeSupport(parseResult.CommandResult.Command);

        support.IsSupported.Should().BeTrue();
        support.AllowsAnySymbol.Should().BeFalse();
        support.Allows(CommonOptions.UntrackedOption).Should().BeTrue();
        support.Allows(SdkInstallCommandParser.ChannelArguments).Should().BeTrue();
        support.Allows(CommonOptions.InstallPathOption).Should().BeFalse();
    }

    [TestMethod]
    public void InitCommand_AllowsAnySymbol()
    {
        var parseResult = Parser.Parse(["init"]);
        var command = new InitCommand(parseResult);

        command.GetAdminModeSupport(parseResult.CommandResult.Command).Should().BeSameAs(AdminModeSupport.AnySymbol);
    }
}
