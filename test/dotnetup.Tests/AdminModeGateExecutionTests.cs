// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using FluentAssertions;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.List;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Verifies that <see cref="CommandBase.Execute"/> applies the admin mode gate before running the command.
/// These tests replace the process-wide access mode, so they must not run in parallel with other tests.
/// </summary>
[TestClass]
[DoNotParallelize]
public class AdminModeGateExecutionTests
{
    private Func<EffectiveAccessMode>? _originalOverride;

    [TestInitialize]
    public void Initialize()
    {
        _originalOverride = AccessModeResolver.OverrideForTesting;
    }

    [TestCleanup]
    public void Cleanup()
    {
        AccessModeResolver.OverrideForTesting = _originalOverride;
    }

    [TestMethod]
    public void Execute_InAdminMode_BlocksUnsupportedBuiltInCommand()
    {
        UseAccessMode(DotnetAccessMode.Admin);

        new ListCommand(Parser.Parse(["list"])).Execute().Should().Be(1);
    }

    [TestMethod]
    public void Execute_InAdminMode_RejectsUnsupportedCommandBeforeExecuteCore()
    {
        UseAccessMode(DotnetAccessMode.Admin);
        var command = new ProbeCommand(Parser.Parse(["list"]), AdminModeSupport.Unsupported);

        command.Execute().Should().Be(1);
        command.Executed.Should().BeFalse();
    }

    [TestMethod]
    public void Execute_InAdminMode_RejectsDisallowedSymbolBeforeExecuteCore()
    {
        UseAccessMode(DotnetAccessMode.Admin);
        var command = new ProbeCommand(Parser.Parse(["list", "--format", "json"]), AdminModeSupport.Allow());

        command.Execute().Should().Be(1);
        command.Executed.Should().BeFalse();
    }

    [TestMethod]
    public void Execute_InAdminMode_RunsSupportedCommand()
    {
        UseAccessMode(DotnetAccessMode.Admin);
        var command = new ProbeCommand(Parser.Parse(["list", "--format", "json"]), AdminModeSupport.AnySymbol);

        command.Execute().Should().Be(0);
        command.Executed.Should().BeTrue();
    }

    [TestMethod]
    public void Execute_OutsideAdminMode_RunsUnsupportedCommand()
    {
        UseAccessMode(DotnetAccessMode.Shell);
        var command = new ProbeCommand(Parser.Parse(["list"]), AdminModeSupport.Unsupported);

        command.Execute().Should().Be(0);
        command.Executed.Should().BeTrue();
    }

    private static void UseAccessMode(DotnetAccessMode mode)
    {
        var accessMode = new EffectiveAccessMode(
            mode,
            mode == DotnetAccessMode.Admin ? AccessModeSource.Policy : AccessModeSource.UserConfig);
        AccessModeResolver.OverrideForTesting = () => accessMode;
    }

    private sealed class ProbeCommand(ParseResult parseResult, AdminModeSupport support)
        : CommandBase(parseResult, "probe")
    {
        public bool Executed { get; private set; }

        internal override AdminModeSupport GetAdminModeSupport(Command command) => support;

        protected override void ExecuteCore() => Executed = true;
    }
}
