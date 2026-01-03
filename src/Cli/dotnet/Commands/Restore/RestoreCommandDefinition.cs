// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.StaticCompletions;
using Microsoft.DotNet.Cli.CommandLine;
using NuGet.Packaging;

namespace Microsoft.DotNet.Cli.Commands.Restore;

internal static class RestoreCommandDefinition
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-restore";

    public static readonly Argument<string[]> SlnOrProjectOrFileArgument = new(CliStrings.SolutionOrProjectOrFileArgumentName)
    {
        Description = CliStrings.SolutionOrProjectOrFileArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public static readonly Option<string[]> TargetOption = CommonOptions.RequiredMSBuildTargetOption("Restore");
    public static readonly Option<Utils.VerbosityOptions> VerbosityOption = CommonOptions.CreateVerbosityOption(Utils.VerbosityOptions.minimal);
    public static readonly Option<bool> NoLogoOption = CommonOptions.NoLogoOption();

    public static Command Create()
    {
        var command = new Command("restore", CliCommandStrings.RestoreAppFullName)
        {
            DocsLink = DocsLink
        };

        command.Arguments.Add(SlnOrProjectOrFileArgument);
        command.Options.Add(CommonOptions.DisableBuildServersOption);

        var implicitOptions = new ImplicitRestoreOptions(showHelp: true, useShortOptions: true);
        implicitOptions.AddTo(command.Options);

        command.Options.Add(CreateRuntimeOption(showHelp: true, useShortOptions: true));
        command.Options.Add(CreateNoDependenciesOption(showHelp: true));
        command.Options.Add(VerbosityOption);
        command.Options.Add(CommonOptions.InteractiveMsBuildForwardOption);
        command.Options.Add(CommonOptions.ArtifactsPathOption);
        command.Options.Add(new Option<bool>("--use-lock-file")
        {
            Description = CliCommandStrings.CmdUseLockFileOptionDescription,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestorePackagesWithLockFile=true"));
        command.Options.Add(new Option<bool>("--locked-mode")
        {
            Description = CliCommandStrings.CmdLockedModeOptionDescription,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestoreLockedMode=true"));
        command.Options.Add(new Option<string>("--lock-file-path")
        {
            Description = CliCommandStrings.CmdLockFilePathOptionDescription,
            HelpName = CliCommandStrings.CmdLockFilePathOption
        }.ForwardAsSingle(o => $"-property:NuGetLockFilePath={o}"));
        command.Options.Add(new Option<bool>("--force-evaluate")
        {
            Description = CliCommandStrings.CmdReevaluateOptionDescription,
            Arity = ArgumentArity.Zero
        }.ForwardAs("-property:RestoreForceEvaluate=true"));
        command.Options.Add(TargetOption);
        command.Options.Add(NoLogoOption);

        command.Options.Add(CommonOptions.ArchitectureOption);
        command.Options.Add(CommonOptions.OperatingSystemOption);
        command.Options.Add(CommonOptions.GetPropertyOption);
        command.Options.Add(CommonOptions.GetItemOption);
        command.Options.Add(CommonOptions.GetTargetResultOption);
        command.Options.Add(CommonOptions.GetResultOutputFileOption);

        return command;
    }

    private static string GetOsFromRid(string rid) => rid.Substring(0, rid.LastIndexOf("-", StringComparison.InvariantCulture));
    private static string GetArchFromRid(string rid) => rid.Substring(rid.LastIndexOf("-", StringComparison.InvariantCulture) + 1, rid.Length - rid.LastIndexOf("-", StringComparison.InvariantCulture) - 1);
    public static string RestoreRuntimeArgFunc(IEnumerable<string> rids)
    {
        List<string> convertedRids = [];
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

    private static Option<IEnumerable<string>> CreateRuntimeOption(bool showHelp, bool useShortOptions)
    {
        Option<IEnumerable<string>> runtimeOption = new Option<IEnumerable<string>>("--runtime")
        {
            Description = CliCommandStrings.CmdRuntimeOptionDescription,
            HelpName = CliCommandStrings.CmdRuntimeOption,
            Hidden = !showHelp,
            IsDynamic = true
        }.ForwardAsSingle(RestoreRuntimeArgFunc)
         .AllowSingleArgPerToken()
         .AddCompletions(CliCompletion.RunTimesFromProjectFile);

        if (useShortOptions)
        {
            runtimeOption.Aliases.Add("-r");
        }

        return runtimeOption;
    }

    public static Option<bool> CreateNoDependenciesOption(bool showHelp)
        => new Option<bool>("--no-dependencies")
        {
            Description = CliCommandStrings.CmdNoDependenciesOptionDescription,
            Arity = ArgumentArity.Zero,
            Hidden = !showHelp
        }.ForwardAs("-property:RestoreRecursive=false");
}
