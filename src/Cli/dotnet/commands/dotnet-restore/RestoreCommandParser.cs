// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Reflection.Metadata.Ecma335;
using Microsoft.DotNet.Tools;
using Microsoft.DotNet.Tools.Restore;
using Microsoft.TemplateEngine.Cli.Commands;
using LocalizableStrings = Microsoft.DotNet.Tools.Restore.LocalizableStrings;

namespace Microsoft.DotNet.Cli
{
    internal static class RestoreCommandParser
    {
        public static readonly string DocsLink = "https://aka.ms/dotnet-restore";

        public static readonly Argument<IEnumerable<string>> SlnOrProjectArgument = new Argument<IEnumerable<string>>(CommonLocalizableStrings.SolutionOrProjectArgumentName)
        {
            Description = CommonLocalizableStrings.SolutionOrProjectArgumentDescription,
            Arity = ArgumentArity.ZeroOrMore
        };

        public static readonly Option<IEnumerable<string>> SourceOption = new ForwardedOption<IEnumerable<string>>(
            new string[] { "-s", "--source" },
            LocalizableStrings.CmdSourceOptionDescription)
        {
            ArgumentHelpName = LocalizableStrings.CmdSourceOption
        }.ForwardAsSingle(o => $"-property:RestoreSources={string.Join("%3B", o)}")
        .AllowSingleArgPerToken();

        private static Option[] FullRestoreOptions() => 
            ImplicitRestoreOptions(true, true, true, true).Concat(
                new Option[] {
                    CommonOptions.VerbosityOption,
                    CommonOptions.InteractiveMsBuildForwardOption,
                    new ForwardedOption<bool>(
                        "--use-lock-file",
                        LocalizableStrings.CmdUseLockFileOptionDescription)
                            .ForwardAs("-property:RestorePackagesWithLockFile=true"),
                    new ForwardedOption<bool>(
                        "--locked-mode",
                        LocalizableStrings.CmdLockedModeOptionDescription)
                            .ForwardAs("-property:RestoreLockedMode=true"),
                    new ForwardedOption<string>(
                        "--lock-file-path",
                        LocalizableStrings.CmdLockFilePathOptionDescription)
                    {
                        ArgumentHelpName = LocalizableStrings.CmdLockFilePathOption
                    }.ForwardAsSingle(o => $"-property:NuGetLockFilePath={o}"),
                    new ForwardedOption<bool>(
                        "--force-evaluate",
                        LocalizableStrings.CmdReevaluateOptionDescription)
                            .ForwardAs("-property:RestoreForceEvaluate=true") })
                .ToArray();

        private static readonly Command Command = ConstructCommand();

        public static Command GetCommand()
        {
            return Command;
        }

        private static Command ConstructCommand()
        {
            var command = new DocumentedCommand("restore", DocsLink, LocalizableStrings.AppFullName);

            command.AddArgument(SlnOrProjectArgument);
            command.AddOption(CommonOptions.DisableBuildServersOption);

            foreach (var option in FullRestoreOptions())
            {
                command.AddOption(option);
            }
            command.AddOption(CommonOptions.ArchitectureOption);
            command.SetHandler(RestoreCommand.Run);

            return command;
        }

        public static void AddImplicitRestoreOptions(Command command, bool showHelp = false, bool useShortOptions = false, bool includeRuntimeOption = true, bool includeNoDependenciesOption = true)
        {
            foreach (var option in ImplicitRestoreOptions(showHelp, useShortOptions, includeRuntimeOption, includeNoDependenciesOption))
            {
                command.AddOption(option);
            }
        }
        private static string GetOsFromRid(string rid) => rid.Substring(0, rid.LastIndexOf("-"));
        private static string GetArchFromRid(string rid) => rid.Substring(rid.LastIndexOf("-") + 1, rid.Length - rid.LastIndexOf("-") - 1);
        public static string RestoreRuntimeArgFunc(IEnumerable<string> rids) 
        {
            List<string> convertedRids = new();
            foreach (string rid in rids)
            {
                if (GetArchFromRid(rid.ToString()) == "amd64")
                {
                    convertedRids.Add($"{GetOsFromRid(rid.ToString())}-x64");
                }
                else
                {
                    convertedRids.Add($"{rid}");
                }
            }
            return $"-property:RuntimeIdentifiers={string.Join("%3B", convertedRids)}";
        }

        private static Option[] ImplicitRestoreOptions(bool showHelp, bool useShortOptions, bool includeRuntimeOption, bool includeNoDependenciesOption)
        {
            var options = new Option[] {
                showHelp && useShortOptions ? SourceOption : new ForwardedOption<IEnumerable<string>>(
                    useShortOptions ? new string[] {"-s", "--source" }  : new string[] { "--source" },
                    showHelp ? LocalizableStrings.CmdSourceOptionDescription : string.Empty)
                {
                    ArgumentHelpName = LocalizableStrings.CmdSourceOption,
                    IsHidden = !showHelp
                }.ForwardAsSingle(o => $"-property:RestoreSources={string.Join("%3B", o)}") // '%3B' corresponds to ';'
                .AllowSingleArgPerToken(),
                new ForwardedOption<string>(
                    "--packages",
                    showHelp ? LocalizableStrings.CmdPackagesOptionDescription : string.Empty)
                {
                    ArgumentHelpName = LocalizableStrings.CmdPackagesOption,
                    IsHidden = !showHelp
                }.ForwardAsSingle(o => $"-property:RestorePackagesPath={CommandDirectoryContext.GetFullPath(o)}"),
				CommonOptions.CurrentRuntimeOption(LocalizableStrings.CmdCurrentRuntimeOptionDescription),
                new ForwardedOption<bool>(
                    "--disable-parallel",
                    showHelp ? LocalizableStrings.CmdDisableParallelOptionDescription : string.Empty)
                {
                    IsHidden = !showHelp
                }.ForwardAs("-property:RestoreDisableParallel=true"),
                new ForwardedOption<string>(
                    "--configfile",
                    showHelp ? LocalizableStrings.CmdConfigFileOptionDescription : string.Empty)
                {
                    ArgumentHelpName = LocalizableStrings.CmdConfigFileOption,
                    IsHidden = !showHelp
                }.ForwardAsSingle(o => $"-property:RestoreConfigFile={CommandDirectoryContext.GetFullPath(o)}"),
                new ForwardedOption<bool>(
                    "--no-cache",
                    showHelp ? LocalizableStrings.CmdNoCacheOptionDescription : string.Empty)
                {
                    IsHidden = !showHelp
                }.ForwardAs("-property:RestoreNoCache=true"),
                new ForwardedOption<bool>(
                    "--ignore-failed-sources",
                    showHelp ? LocalizableStrings.CmdIgnoreFailedSourcesOptionDescription : string.Empty)
                {
                    IsHidden = !showHelp
                }.ForwardAs("-property:RestoreIgnoreFailedSources=true"),
                new ForwardedOption<bool>(
                    useShortOptions ? new string[] {"-f", "--force" } : new string[] {"--force" },
                    LocalizableStrings.CmdForceRestoreOptionDescription)
                {
                    IsHidden = !showHelp
                }.ForwardAs("-property:RestoreForce=true"),
                CommonOptions.PropertiesOption
            };

            if (includeRuntimeOption)
            {
                options = options.Append(
                    new ForwardedOption<IEnumerable<string>>(
                        useShortOptions ? new string[] { "-r", "--runtime" } : new string[] { "--runtime" },
                        LocalizableStrings.CmdRuntimeOptionDescription)
                    {
                        ArgumentHelpName = LocalizableStrings.CmdRuntimeOption,
                        IsHidden = !showHelp
                    }.ForwardAsSingle(RestoreRuntimeArgFunc)
                    .AllowSingleArgPerToken()
                    .AddCompletions(Complete.RunTimesFromProjectFile)
                ).ToArray();
            }

            if (includeNoDependenciesOption)
            {
                options = options.Append(
                    new ForwardedOption<bool>(
                        "--no-dependencies",
                        LocalizableStrings.CmdNoDependenciesOptionDescription)
                    {
                        IsHidden = !showHelp
                    }.ForwardAs("-property:RestoreRecursive=false")
                ).ToArray();
            }

            return options;
        }
    }
}
