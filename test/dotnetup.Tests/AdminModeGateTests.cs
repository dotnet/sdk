// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Reflection;
using FluentAssertions;
using Microsoft.Dotnet.Installation;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.ElevatedSystemPath;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Runtime.Install;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Sdk.Install;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Self;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Shared;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class AdminModeGateTests
{
    private static readonly EffectiveAccessMode s_admin = new(DotnetAccessMode.Admin, AccessModeSource.Policy);

    [TestMethod]
    [DataRow(null)]
    [DataRow((int)DotnetAccessMode.None)]
    [DataRow((int)DotnetAccessMode.Shell)]
    [DataRow((int)DotnetAccessMode.Everywhere)]
    public void Enforce_OutsideAdminMode_IsNoOp(int? accessMode)
    {
        var mode = accessMode is int value
            ? new EffectiveAccessMode((DotnetAccessMode)value, AccessModeSource.UserConfig)
            : EffectiveAccessMode.Unset;
        var parseResult = Parser.Parse(["sdk", "install", "10.0", "--install-path", "C:\\x"]);

        AdminModeGate.Enforce(parseResult, AdminModeSupport.Unsupported, mode);
        AdminModeGate.Enforce(parseResult, AdminModeSupport.Allow(), mode);
    }

    [TestMethod]
    [DataRow(new[] { "list" }, "dotnetup list")]
    [DataRow(new[] { "sdk", "update" }, "dotnetup sdk update")]
    [DataRow(new string[0], "dotnetup")]
    public void Enforce_UnsupportedCommand_ThrowsCommandNotSupported(string[] args, string expectedPath)
    {
        var parseResult = Parser.Parse(args);

        var ex = Assert.ThrowsExactly<DotnetInstallException>(
            () => AdminModeGate.Enforce(parseResult, AdminModeSupport.Unsupported, s_admin));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.AdminModeCommandNotSupported);
        ex.Message.Should().Contain($"'{expectedPath}'")
            .And.Contain("'sdk install'")
            .And.Contain("'runtime install'")
            .And.Contain("'init'")
            .And.Contain("'env set'")
            .And.Contain("'env script'")
            .And.Contain("'self update'")
            .And.Contain("dotnetup init --mode <mode>");
    }

    [TestMethod]
    public void Enforce_AllowedOptionAndArgument_Passes()
    {
        var parseResult = Parser.Parse(["sdk", "install", "10.0", "--untracked"]);
        var support = AdminModeSupport.Allow(CommonOptions.UntrackedOption, SdkInstallCommandParser.ChannelArguments);

        AdminModeGate.Enforce(parseResult, support, s_admin);
    }

    [TestMethod]
    public void Enforce_DisallowedOption_ThrowsSymbolNotSupported()
    {
        var parseResult = Parser.Parse(["sdk", "install", "10.0", "--untracked", "--install-path", "C:\\x"]);
        var support = AdminModeSupport.Allow(CommonOptions.UntrackedOption, SdkInstallCommandParser.ChannelArguments);

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() => AdminModeGate.Enforce(parseResult, support, s_admin));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.AdminModeSymbolNotSupported);
        ex.Message.Should().Contain("'--install-path'").And.Contain("'dotnetup sdk install'");
    }

    [TestMethod]
    public void Enforce_DefaultedOptions_AreIgnored()
    {
        // --interactive, --verbosity, and the bool options all receive implicit default results.
        var parseResult = Parser.Parse(["sdk", "install"]);

        AdminModeGate.GetExplicitSymbols(parseResult.CommandResult).Should().BeEmpty();
        AdminModeGate.Enforce(parseResult, AdminModeSupport.Allow(), s_admin);
    }

    [TestMethod]
    public void Enforce_PositionalArgument_RejectedUnlessListed()
    {
        var parseResult = Parser.Parse(["sdk", "install", "10.0", "--untracked"]);

        var ex = Assert.ThrowsExactly<DotnetInstallException>(
            () => AdminModeGate.Enforce(parseResult, AdminModeSupport.Allow(CommonOptions.UntrackedOption), s_admin));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.AdminModeSymbolNotSupported);
        ex.Message.Should().Contain($"'<{SdkInstallCommandParser.ChannelArguments.Name}>'");
        AdminModeGate.Enforce(Parser.Parse(["sdk", "install", "--untracked"]), AdminModeSupport.Allow(CommonOptions.UntrackedOption), s_admin);
    }

    [TestMethod]
    public void Enforce_AnySymbol_AllowsEverything()
    {
        var parseResult = Parser.Parse(["sdk", "install", "10.0", "--install-path", "C:\\x", "--manifest-path", "C:\\m.json"]);

        AdminModeGate.Enforce(parseResult, AdminModeSupport.AnySymbol, s_admin);
    }

    [TestMethod]
    public void Enforce_HelpOption_IsIgnored()
    {
        var parseResult = Parser.Parse(["sdk", "install", "--help"]);

        AdminModeGate.Enforce(parseResult, AdminModeSupport.Allow(), s_admin);
    }

    [TestMethod]
    [DataRow(new[] { "--global", "sub" })]
    [DataRow(new[] { "sub", "--global" })]
    public void Enforce_OptionsOnParentCommands_AreChecked(string[] args)
    {
        var global = new Option<bool>("--global") { Recursive = true };
        var sub = new Command("sub");
        var root = new RootCommand { Options = { global }, Subcommands = { sub } };
        var parseResult = root.Parse(args);
        parseResult.Errors.Should().BeEmpty();

        var ex = Assert.ThrowsExactly<DotnetInstallException>(() => AdminModeGate.Enforce(parseResult, AdminModeSupport.Allow(), s_admin));

        ex.ErrorCode.Should().Be(DotnetInstallErrorCode.AdminModeSymbolNotSupported);
        ex.Message.Should().Contain("'--global'").And.Contain("'dotnetup sub'");
        AdminModeGate.Enforce(parseResult, AdminModeSupport.Allow(global), s_admin);
    }

    [TestMethod]
    public void GetCommandPath_UsesTypedCommandNames()
    {
        AdminModeGate.GetCommandPath(Parser.Parse(["install"]).CommandResult).Should().Be("dotnetup install");
        AdminModeGate.GetCommandPath(Parser.Parse(["runtime", "install"]).CommandResult).Should().Be("dotnetup runtime install");
    }

    [TestMethod]
    public void OnlyExpectedCommandsOptIntoAdminMode()
    {
        var declaringTypes = typeof(CommandBase).Assembly.GetTypes()
            .Where(t => t != typeof(CommandBase) && typeof(CommandBase).IsAssignableFrom(t))
            .Where(t => t.GetMethod(
                nameof(CommandBase.GetAdminModeSupport),
                BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly) is not null)
            .ToHashSet();

        declaringTypes.Should().BeEquivalentTo(new[]
        {
            typeof(InstallCommand),
            typeof(InitCommand),
            typeof(EnvSetCommand),
            typeof(EnvScriptCommand),
            typeof(SelfUpdateCommand),
            typeof(ElevatedSystemPathCommand),
        });
    }

    [TestMethod]
    public void InstallCommandAllowlist_IsRegisteredOnServedCommands()
    {
        var sdkParse = Parser.Parse(["sdk", "install"]);
        var runtimeParse = Parser.Parse(["runtime", "install"]);
        var allowed = new SdkInstallCommand(sdkParse).GetAdminModeSupport(sdkParse.CommandResult.Command).AllowedSymbols
            .Concat(new RuntimeInstallCommand(runtimeParse).GetAdminModeSupport(runtimeParse.CommandResult.Command).AllowedSymbols)
            .Distinct()
            .ToList();
        Command[] served =
        [
            SdkInstallCommandParser.GetSdkInstallCommand(),
            SdkInstallCommandParser.GetRootInstallCommand(),
            RuntimeInstallCommandParser.GetRuntimeInstallCommand(),
        ];

        allowed.Should().NotBeEmpty();
        foreach (var symbol in allowed)
        {
            served.Any(c => IsRegistered(c, symbol)).Should().BeTrue(
                $"'{symbol.Name}' is allowed in admin mode and must be registered on an install command");
        }
    }

    private static bool IsRegistered(Command command, Symbol symbol) => symbol switch
    {
        Option option => command.Options.Contains(option),
        Argument argument => command.Arguments.Contains(argument),
        _ => false,
    };
}
