﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.CommandLine.Parsing;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Configurer;
using Microsoft.DotNet.ShellShim;
using Microsoft.Extensions.EnvironmentAbstractions;
using LocalizableStrings = Microsoft.DotNet.Cli.Utils.LocalizableStrings;
using NuGet.Frameworks;
using System.Linq;
using Microsoft.DotNet.Tools.Help;
using Microsoft.DotNet.CommandFactory;

namespace Microsoft.DotNet.Cli
{
    public class Program
    {
        private static readonly string ToolPathSentinelFileName = $"{Product.Version}.toolpath.sentinel";

        public static int Main(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);

            // Capture the current timestamp to calculate the host overhead.
            DateTime mainTimeStamp = DateTime.Now;

            bool perfLogEnabled = Env.GetEnvironmentVariableAsBool("DOTNET_CLI_PERF_LOG", false);
            PerformanceLogStartupInformation startupInfo = null;
            if (perfLogEnabled)
            {
                startupInfo = new PerformanceLogStartupInformation(mainTimeStamp);
                PerformanceLogManager.InitializeAndStartCleanup(FileSystemWrapper.Default);
            }

            PerformanceLogEventListener perLogEventListener = null;
            try
            {
                if (perfLogEnabled)
                {
                    perLogEventListener = PerformanceLogEventListener.Create(FileSystemWrapper.Default, PerformanceLogManager.Instance.CurrentLogDirectory);
                }

                new MulticoreJitActivator().TryActivateMulticoreJit();

                PerformanceLogEventSource.Log.LogStartUpInformation(startupInfo);
                PerformanceLogEventSource.Log.CLIStart();

                if (Env.GetEnvironmentVariableAsBool("DOTNET_CLI_CAPTURE_TIMING", false))
                {
                    PerfTrace.Enabled = true;
                }

                InitializeProcess();

                try
                {
                    using (PerfTrace.Current.CaptureTiming())
                    {
                        return ProcessArgs(args);
                    }
                }
                catch (HelpException e)
                {
                    Reporter.Output.WriteLine(e.Message);
                    return 0;
                }
                catch (Exception e) when (e.ShouldBeDisplayedAsError())
                {
                    Reporter.Error.WriteLine(CommandContext.IsVerbose()
                        ? e.ToString().Red().Bold()
                        : e.Message.Red().Bold());

                    var commandParsingException = e as CommandParsingException;
                    if (commandParsingException != null)
                    {
                        Reporter.Output.WriteLine(commandParsingException.HelpText);
                    }

                    return 1;
                }
                catch (Exception e) when (!e.ShouldBeDisplayedAsError())
                {
                    // If telemetry object has not been initialized yet. It cannot be collected
                    TelemetryEventEntry.SendFiltered(e);
                    Reporter.Error.WriteLine(e.ToString().Red().Bold());

                    return 1;
                }
                finally
                {
                    if (PerfTrace.Enabled)
                    {
                        Reporter.Output.WriteLine("Performance Summary:");
                        PerfTraceOutput.Print(Reporter.Output, PerfTrace.GetEvents());
                    }

                    PerformanceLogEventSource.Log.CLIStop();
                }
            }
            finally
            {
                if(perLogEventListener != null)
                {
                    perLogEventListener.Dispose();
                }
            }
        }

        internal static int ProcessArgs(string[] args, ITelemetry telemetryClient = null)
        {
            var parseResult = Parser.Instance.Parse(args);
            using (IFirstTimeUseNoticeSentinel disposableFirstTimeUseNoticeSentinel =
                new FirstTimeUseNoticeSentinel())
            {
                IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel = disposableFirstTimeUseNoticeSentinel;
                IAspNetCertificateSentinel aspNetCertificateSentinel = new AspNetCertificateSentinel();
                IFileSentinel toolPathSentinel = new FileSentinel(
                    new FilePath(
                        Path.Combine(
                            CliFolderPathCalculator.DotnetUserProfileFolderPath,
                            ToolPathSentinelFileName)));
                if (parseResult.ValueForOption<bool>(Parser.DiagOption))
                {
                    Environment.SetEnvironmentVariable(CommandContext.Variables.Verbose, bool.TrueString);
                    CommandContext.SetVerbose(true);
                    Reporter.Reset();
                }
                if (parseResult.HasOption(Parser.VersionOption))
                {
                    CommandLineInfo.PrintVersion();
                    return 0;
                }
                else if (parseResult.HasOption(Parser.InfoOption))
                {
                    CommandLineInfo.PrintInfo();
                    return 0;
                }
                else if (parseResult.CommandResult.Command.Equals(Parser.RootCommand) && parseResult.HasOption("-h"))
                {
                    HelpCommand.PrintHelp();
                    return 0;
                }
                else if (parseResult.Directives.Count() > 0)
                {
                    return parseResult.Invoke();
                }
                else
                {
                    PerformanceLogEventSource.Log.FirstTimeConfigurationStart();

                    var environmentProvider = new EnvironmentProvider();

                    bool generateAspNetCertificate =
                        environmentProvider.GetEnvironmentVariableAsBool("DOTNET_GENERATE_ASPNET_CERTIFICATE", defaultValue: true);
                    bool telemetryOptout =
                      environmentProvider.GetEnvironmentVariableAsBool("DOTNET_CLI_TELEMETRY_OPTOUT", defaultValue: false);
                    bool addGlobalToolsToPath =
                        environmentProvider.GetEnvironmentVariableAsBool("DOTNET_ADD_GLOBAL_TOOLS_TO_PATH", defaultValue: true);
                    bool nologo =
                        environmentProvider.GetEnvironmentVariableAsBool("DOTNET_NOLOGO", defaultValue: false);

                    ReportDotnetHomeUsage(environmentProvider);

                    var isDotnetBeingInvokedFromNativeInstaller = false;
                    if (parseResult.CommandResult.Command.Name.Equals(Parser.InstallSuccessCommand.Name))
                    {
                        aspNetCertificateSentinel = new NoOpAspNetCertificateSentinel();
                        firstTimeUseNoticeSentinel = new NoOpFirstTimeUseNoticeSentinel();
                        toolPathSentinel = new NoOpFileSentinel(exists: false);
                        isDotnetBeingInvokedFromNativeInstaller = true;
                    }

                    var dotnetFirstRunConfiguration = new DotnetFirstRunConfiguration(
                        generateAspNetCertificate: generateAspNetCertificate,
                        telemetryOptout: telemetryOptout,
                        addGlobalToolsToPath: addGlobalToolsToPath,
                        nologo: nologo);

                    ConfigureDotNetForFirstTimeUse(
                        firstTimeUseNoticeSentinel,
                        aspNetCertificateSentinel,
                        toolPathSentinel,
                        isDotnetBeingInvokedFromNativeInstaller,
                        dotnetFirstRunConfiguration,
                        environmentProvider);
                        PerformanceLogEventSource.Log.FirstTimeConfigurationStop();

                }

                PerformanceLogEventSource.Log.TelemetryRegistrationStart();

                if (telemetryClient == null)
                {
                    telemetryClient = new Telemetry.Telemetry(firstTimeUseNoticeSentinel);
                }
                TelemetryEventEntry.Subscribe(telemetryClient.TrackEvent);
                TelemetryEventEntry.TelemetryFilter = new TelemetryFilter(Sha256Hasher.HashWithNormalizedCasing);

                PerformanceLogEventSource.Log.TelemetryRegistrationStop();
            }

            if (CommandContext.IsVerbose())
            {
                Console.WriteLine($"Telemetry is: {(telemetryClient.Enabled ? "Enabled" : "Disabled")}");
            }
            PerformanceLogEventSource.Log.TelemetrySaveIfEnabledStart();
            TelemetryEventEntry.SendFiltered(parseResult);
            PerformanceLogEventSource.Log.TelemetrySaveIfEnabledStop();

            int exitCode;
            if (parseResult.CommandResult.Command.Name.Equals("dotnet") && string.IsNullOrEmpty(parseResult.ValueForArgument<string>(Parser.DotnetSubCommand)))
            {
                exitCode = 0;
            }
            else if (BuiltInCommandsCatalog.Commands.TryGetValue(parseResult.RootSubCommandResult(), out var builtIn))
            {
			    PerformanceLogEventSource.Log.BuiltInCommandParserStart();
                if (parseResult.Errors.Count <= 0)
                {
				    PerformanceLogEventSource.Log.TelemetrySaveIfEnabledStart();
                    TelemetryEventEntry.SendFiltered(parseResult);
					PerformanceLogEventSource.Log.TelemetrySaveIfEnabledStop();
                }

                PerformanceLogEventSource.Log.BuiltInCommandStart();
                var topLevelCommands = new string[] { "dotnet", parseResult.RootSubCommandResult() }.Concat(Parser.DiagOption.Aliases);

                exitCode = builtIn.Command(args.Where(t => !topLevelCommands.Contains(t)).ToArray());
				PerformanceLogEventSource.Log.BuiltInCommandStop();
            }
            else
            {
                PerformanceLogEventSource.Log.ExtensibleCommandResolverStart();
                var resolvedCommand = CommandFactoryUsingResolver.Create(
                        "dotnet-" + parseResult.ValueForArgument<string>(Parser.DotnetSubCommand),
                        parseResult.UnmatchedTokens,
                        FrameworkConstants.CommonFrameworks.NetStandardApp15);
                PerformanceLogEventSource.Log.ExtensibleCommandResolverStop();

                PerformanceLogEventSource.Log.ExtensibleCommandStart();
                var result = resolvedCommand.Execute();
                PerformanceLogEventSource.Log.ExtensibleCommandStop();
                
                exitCode = result.ExitCode;
            }

            PerformanceLogEventSource.Log.TelemetryClientFlushStart();
            telemetryClient.Flush();
            PerformanceLogEventSource.Log.TelemetryClientFlushStop();

            return exitCode;
        }

        private static void ReportDotnetHomeUsage(IEnvironmentProvider provider)
        {
            var home = provider.GetEnvironmentVariable(CliFolderPathCalculator.DotnetHomeVariableName);
            if (string.IsNullOrEmpty(home))
            {
                return;
            }

            Reporter.Verbose.WriteLine(
                string.Format(
                    LocalizableStrings.DotnetCliHomeUsed,
                    home,
                    CliFolderPathCalculator.DotnetHomeVariableName));
        }

        private static void ConfigureDotNetForFirstTimeUse(
            IFirstTimeUseNoticeSentinel firstTimeUseNoticeSentinel,
            IAspNetCertificateSentinel aspNetCertificateSentinel,
            IFileSentinel toolPathSentinel,
            bool isDotnetBeingInvokedFromNativeInstaller,
            DotnetFirstRunConfiguration dotnetFirstRunConfiguration,
            IEnvironmentProvider environmentProvider)
        {
            using (PerfTrace.Current.CaptureTiming())
            {
                var environmentPath = EnvironmentPathFactory.CreateEnvironmentPath(isDotnetBeingInvokedFromNativeInstaller, environmentProvider);
                var commandFactory = new DotNetCommandFactory(alwaysRunOutOfProc: true);
                var aspnetCertificateGenerator = new AspNetCoreCertificateGenerator();
                var dotnetConfigurer = new DotnetFirstTimeUseConfigurer(
                    firstTimeUseNoticeSentinel,
                    aspNetCertificateSentinel,
                    aspnetCertificateGenerator,
                    toolPathSentinel,
                    dotnetFirstRunConfiguration,
                    Reporter.Output,
                    CliFolderPathCalculator.CliFallbackFolderPath,
                    environmentPath);

                dotnetConfigurer.Configure();

                if (isDotnetBeingInvokedFromNativeInstaller && OperatingSystem.IsWindows())
                {
                    DotDefaultPathCorrector.Correct();
                }
            }
        }

        private static void InitializeProcess()
        {
            // by default, .NET Core doesn't have all code pages needed for Console apps.
            // see the .NET Core Notes in https://docs.microsoft.com/dotnet/api/system.diagnostics.process#-notes
            Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

            UILanguageOverride.Setup();
        }

        internal static bool TryGetBuiltInCommand(string commandName, out BuiltInCommandMetadata builtInCommand)
        {
            return BuiltInCommandsCatalog.Commands.TryGetValue(commandName, out builtInCommand);
        }
    }
}
