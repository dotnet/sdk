// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using FluentAssertions;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Env;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.List;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Verifies that <see cref="CommandBase.Execute"/> applies the admin mode gate before running the command.
/// Admin mode comes from an in-memory machine policy passed to each command; the user config lives in a
/// temporary data directory, which is process-wide, so these tests must not run in parallel with other tests.
/// </summary>
[TestClass]
[DoNotParallelize]
public class AdminModeGateExecutionTests
{
    private AdminModeTestEnvironment _environment = null!;

    [TestInitialize]
    public void Initialize()
    {
        _environment = new AdminModeTestEnvironment();
    }

    [TestCleanup]
    public void Cleanup()
    {
        _environment.Dispose();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Execute_InAdminMode_BlocksUnsupportedBuiltInCommand()
    {
        EnableAdminModePolicy();

        new ListCommand(Parser.Parse(["list"]), _environment.Policy).Execute().Should().Be(1);
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Execute_InAdminMode_RejectsUnsupportedCommandBeforeExecuteCore()
    {
        EnableAdminModePolicy();
        var command = new ProbeCommand(Parser.Parse(["list"]), AdminModeSupport.Unsupported, _environment.Policy);

        command.Execute().Should().Be(1);
        command.Executed.Should().BeFalse();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Execute_InAdminMode_RejectsDisallowedSymbolBeforeExecuteCore()
    {
        EnableAdminModePolicy();
        var command = new ProbeCommand(Parser.Parse(["list", "--format", "json"]), AdminModeSupport.Allow(), _environment.Policy);

        command.Execute().Should().Be(1);
        command.Executed.Should().BeFalse();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Execute_InAdminMode_RunsSupportedCommand()
    {
        EnableAdminModePolicy();
        var command = new ProbeCommand(Parser.Parse(["list", "--format", "json"]), AdminModeSupport.AnySymbol, _environment.Policy);

        command.Execute().Should().Be(0);
        command.Executed.Should().BeTrue();
    }

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    [DataRow(new[] { "env", "script", "--shell", "pwsh", "--dotnet" })]
    [DataRow(new[] { "print-env-script", "--shell", "pwsh", "--dotnetup-only" })]
    public void Execute_InAdminMode_AllowsExistingShellProfileCommands(string[] arguments)
    {
        EnableAdminModePolicy();
        var parseResult = Parser.Parse(arguments);
        parseResult.Errors.Should().BeEmpty();

        new EnvScriptCommand(parseResult, policy: _environment.Policy).Execute().Should().Be(0);
    }

    [TestMethod]
    public void Execute_OutsideAdminMode_RunsUnsupportedCommand()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell });
        var command = new ProbeCommand(Parser.Parse(["list"]), AdminModeSupport.Unsupported, _environment.Policy);

        command.Execute().Should().Be(0);
        command.Executed.Should().BeTrue();
    }

    [TestMethod]
    public void Execute_AdminModeRecordedInUserConfig_DoesNotGateCommands()
    {
        // Only the machine policy can select admin mode; a user config that records it is ignored.
        File.WriteAllText(DotnetupPaths.ConfigPath, """{ "schemaVersion": "1", "accessMode": "admin" }""");
        var command = new ProbeCommand(Parser.Parse(["list"]), AdminModeSupport.Unsupported, _environment.Policy);

        command.Execute().Should().Be(0);
        command.Executed.Should().BeTrue();
    }

    private void EnableAdminModePolicy() => _environment.Store.Values[DotnetupPolicy.AdminModeValueName] = true;

    private sealed class ProbeCommand(ParseResult parseResult, AdminModeSupport support, DotnetupPolicy policy)
        : CommandBase(parseResult, "probe", policy)
    {
        public bool Executed { get; private set; }

        internal override AdminModeSupport GetAdminModeSupport(Command command) => support;

        protected override void ExecuteCore() => Executed = true;
    }
}
