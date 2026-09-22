// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Globalization;
using System.Resources;
using System.Text;
using Microsoft.Dotnet.Installation;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Self;
using Spectre.Console;
using BootstrapperStrings = Microsoft.DotNet.Tools.Bootstrapper.Strings;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class SelfCommandParserTests
{
    [TestMethod]
    [DataRow("self update", false)]
    [DataRow("self update --no-progress", true)]
    [DataRow("self update --no-progress true", true)]
    [DataRow("self update --no-progress false", false)]
    public void ParsesSharedNoProgressOption(string commandLine, bool expected)
    {
        var result = Parser.Parse(commandLine.Split(' '));

        result.Errors.Should().BeEmpty();
        result.CommandResult.Command.Name.Should().Be("update");
        result.GetValue(CommonOptions.NoProgressOption).Should().Be(expected);
        result.CommandResult.Command.Options.Should().Contain(CommonOptions.NoProgressOption);
    }

    [TestMethod]
    [DataRow("self update", null)]
    [DataRow("self update --channel daily", "daily")]
    [DataRow("self update --channel preview", "preview")]
    [DataRow("self update --channel stable", "stable")]
    public void ParsesChannelOption(string commandLine, string? expected)
    {
        var result = Parser.Parse(commandLine.Split(' '));

        result.Errors.Should().BeEmpty();
        result.GetValue(SelfCommandParser.ChannelOption).Should().Be(expected);
        result.CommandResult.Command.Options.Should().Contain(SelfCommandParser.ChannelOption);
    }

    [TestMethod]
    [DataRow("self update --no-progress invalid")]
    [DataRow("self update --channel servicing")]
    [DataRow("self update --unknown")]
    [DataRow("self update 1.0")]
    public void RejectsInvalidArguments(string commandLine)
    {
        Parser.Parse(commandLine.Split(' ')).Errors.Should().NotBeEmpty();
    }

    [TestMethod]
    public void DescriptionsUseResources()
    {
        var command = SelfCommandParser.GetCommand();

        command.Description.Should().Be(BootstrapperStrings.SelfCommandDescription);
        command.Subcommands.Single().Description.Should().Be(BootstrapperStrings.SelfUpdateCommandDescription);
    }

    [TestMethod]
    public void HelpIncludesSharedNoProgressOptionWithoutRunningUpdate()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var result = Parser.Parse(["self", "update", "--help"]);

        result.Invoke(new InvocationConfiguration { Output = output, Error = output }).Should().Be(0);

        output.ToString().Should().Contain(BootstrapperStrings.SelfUpdateCommandDescription)
            .And.Contain("--channel")
            .And.Contain("--no-progress");
    }

    [TestMethod]
    [DataRow(nameof(BootstrapperStrings.SelfCommandDescription), 0)]
    [DataRow(nameof(BootstrapperStrings.SelfUpdateCommandDescription), 0)]
    [DataRow(nameof(BootstrapperStrings.SelfUpdateChannelOptionDescription), 0)]
    [DataRow(nameof(BootstrapperStrings.SelfUpdateDownloading), 0)]
    [DataRow(nameof(BootstrapperStrings.SelfUpdateAlreadyUpToDate), 2)]
    [DataRow(nameof(BootstrapperStrings.SelfUpdateCurrentVersionNewer), 2)]
    [DataRow(nameof(BootstrapperStrings.SelfUpdateSucceeded), 1)]
    [DataRow(nameof(BootstrapperStrings.SelfUpdateInProgress), 0)]
    [DataRow(nameof(BootstrapperStrings.SelfUpdateExecutableChanged), 0)]
    [DataRow(nameof(BootstrapperStrings.SelfUpdateIdentityUnavailable), 0)]
    [DataRow(nameof(BootstrapperStrings.SelfUpdateBusyUpdate), 0)]
    [DataRow(nameof(BootstrapperStrings.SelfUpdateBusyCommand), 0)]
    [DataRow(nameof(BootstrapperStrings.SelfUpdateStagedIdentityMismatch), 0)]
    [DataRow(nameof(BootstrapperStrings.SelfUpdateRollbackFailed), 0)]
    [DataRow(nameof(BootstrapperStrings.SelfUpdateVerificationFailed), 0)]
    [DataRow(nameof(BootstrapperStrings.SelfUpdateAccessFailed), 0)]
    [DataRow(nameof(BootstrapperStrings.SelfUpdateUnsupportedHost), 0)]
    [DataRow(nameof(BootstrapperStrings.SelfUpdateRequiresCanonicalName), 2)]
    [DataRow(nameof(BootstrapperStrings.SelfUpdateUnsupportedLocation), 1)]
    [DataRow(nameof(BootstrapperStrings.SelfUpdateDirectoryAccessDenied), 1)]
    public void ResourcesHaveExpectedFormatArguments(string key, int argumentCount)
    {
        var resources = new ResourceManager("Microsoft.DotNet.Tools.Bootstrapper.Strings", typeof(SelfUpdateCommand).Assembly);
        var value = resources.GetString(key, CultureInfo.InvariantCulture);

        value.Should().NotBeNullOrWhiteSpace();
        CompositeFormat.Parse(value!).MinimumArgumentCount.Should().Be(argumentCount);
    }

    [TestMethod]
    public void SuccessMessageContainsVersionAndInstallationLink()
    {
        var message = string.Format(CultureInfo.InvariantCulture, BootstrapperStrings.SelfUpdateSucceeded, "0.2.0-preview.1");

        message.Should().Contain("0.2.0-preview.1").And.Contain("https://aka.ms/dotnet/dotnetup");
    }

    [TestMethod]
    [DataRow(true, true)]
    [DataRow(true, false)]
    [DataRow(false, true)]
    [DataRow(false, false)]
    public void DownloadProgressSupportsKnownAndUnknownLengths(bool noProgress, bool hasLength)
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var originalConsole = AnsiConsole.Console;
        AnsiConsole.Console = CreateConsole(output);
        try
        {
            var called = false;
            SelfUpdateDownloadProgress.Run(noProgress, progress =>
            {
                called = true;
                progress.Report(new DownloadProgress(512, hasLength ? 1024 : null));
                progress.Report(new DownloadProgress(1024, hasLength ? 1024 : null));
            });

            called.Should().BeTrue();
            if (noProgress)
            {
                output.ToString().Should().Contain(BootstrapperStrings.SelfUpdateDownloading)
                    .And.Contain("Completed:").And.NotContain("\u001b");
            }
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    [TestMethod]
    public void DownloadFailureIsNotSwallowedOrReportedAsCompleted()
    {
        using var output = new StringWriter(CultureInfo.InvariantCulture);
        var originalConsole = AnsiConsole.Console;
        AnsiConsole.Console = CreateConsole(output);
        try
        {
            var failure = new DotnetInstallException(DotnetInstallErrorCode.DownloadFailed, "Download failed.");
            Action download = () => SelfUpdateDownloadProgress.Run(true, progress =>
            {
                progress.Report(new DownloadProgress(512, null));
                throw failure;
            });

            download.Should().Throw<DotnetInstallException>().Which.Should().BeSameAs(failure);
            output.ToString().Should().NotContain("Completed:");
        }
        finally
        {
            AnsiConsole.Console = originalConsole;
        }
    }

    private static IAnsiConsole CreateConsole(StringWriter output) => AnsiConsole.Create(new AnsiConsoleSettings
    {
        Ansi = AnsiSupport.No,
        ColorSystem = ColorSystemSupport.NoColors,
        Interactive = InteractionSupport.No,
        Out = new AnsiConsoleOutput(output),
    });
}