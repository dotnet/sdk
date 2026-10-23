// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using FluentAssertions;
using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
[DoNotParallelize]
public class AdminModeGateExecutionTests : IDisposable
{
    private readonly AdminModeTestEnvironment _environment = new();

    public void Dispose() => _environment.Dispose();

    [TestMethod, OSCondition(OperatingSystems.Windows)]
    public void Execute_AdminModeInUserConfig_RejectsUnsupportedCommand()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Admin });
        var command = new ProbeCommand(Parser.Parse(["list"]), AdminModeSupport.Unsupported, _environment.Policy);

        command.Execute().Should().Be(1);
        command.Executed.Should().BeFalse();
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
    public void Execute_UnreportedFailure_PrintsErrorLine()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell });
        var failure = new InvalidOperationException("boom");
        var command = new ProbeCommand(Parser.Parse(["list"]), AdminModeSupport.Unsupported, _environment.Policy, failure);

        string output = CaptureOutput(() => command.Execute().Should().Be(1));

        output.Should().Contain("Error: boom");
    }

    [TestMethod]
    public void Execute_FailureAlreadyReportedToUser_DoesNotPrintItAgain()
    {
        DotnetupConfig.Write(new DotnetupConfigData { AccessMode = DotnetAccessMode.Shell });
        var failure = new InvalidOperationException("boom");
        failure.MarkReportedToUser();
        var command = new ProbeCommand(Parser.Parse(["list"]), AdminModeSupport.Unsupported, _environment.Policy, failure);

        string output = CaptureOutput(() => command.Execute().Should().Be(1));

        output.Should().NotContain("boom");
    }

    private static string CaptureOutput(Action action)
    {
        IAnsiConsole previousConsole = AnsiConsole.Console;
        using var output = new StringWriter();
        try
        {
            AnsiConsole.Console = AdminModeSwitcherTests.CreateConsole(output);
            action();
            return output.ToString();
        }
        finally
        {
            AnsiConsole.Console = previousConsole;
        }
    }

    private sealed class ProbeCommand(ParseResult parseResult, AdminModeSupport support, DotnetupPolicy policy, Exception? failure = null)
        : CommandBase(parseResult, "probe", policy)
    {
        public bool Executed { get; private set; }

        internal override AdminModeSupport GetAdminModeSupport(Command command) => support;

        protected override void ExecuteCore()
        {
            Executed = true;
            if (failure is not null)
            {
                throw failure;
            }
        }
    }
}
