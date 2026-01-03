// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.StaticCompletions;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Restore;

internal static class RestoreCommandDefinition
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-restore";

    public static readonly Argument<string[]> SlnOrProjectOrFileArgument = new(CliStrings.SolutionOrProjectOrFileArgumentName)
    {
        Description = CliStrings.SolutionOrProjectOrFileArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public static readonly ImplicitRestoreOptions ImplicitRestoreOptions = new(showHelp: true, useShortOptions: true);

    public static readonly Option<string[]> TargetOption = CommonOptions.CreateRequiredMSBuildTargetOption("Restore");
    public static readonly Option<Utils.VerbosityOptions> VerbosityOption = CommonOptions.CreateVerbosityOption(Utils.VerbosityOptions.minimal);
    public static readonly Option<bool> NoLogoOption = CommonOptions.CreateNoLogoOption();

    public static readonly Option<bool> DisableBuildServersOption = CommonOptions.CreateDisableBuildServersOption();
    public static readonly Option<IEnumerable<string>> RuntimeOption = CreateRuntimeOption(showHelp: true, useShortOptions: true);
    public static readonly Option<bool> NoDependenciesOption = CreateNoDependenciesOption(showHelp: true);
    public static readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveMsBuildForwardOption();
    public static readonly Option<string> ArtifactsPathOption = CommonOptions.CreateArtifactsPathOption();
    public static readonly Option<bool> UseLockFileOption = new Option<bool>("--use-lock-file")
    {
        Description = CliCommandStrings.CmdUseLockFileOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:RestorePackagesWithLockFile=true");
    public static readonly Option<bool> LockedModeOption = new Option<bool>("--locked-mode")
    {
        Description = CliCommandStrings.CmdLockedModeOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:RestoreLockedMode=true");
    public static readonly Option<string> LockFilePathOption = new Option<string>("--lock-file-path")
    {
        Description = CliCommandStrings.CmdLockFilePathOptionDescription,
        HelpName = CliCommandStrings.CmdLockFilePathOption
    }.ForwardAsSingle(o => $"-property:NuGetLockFilePath={o}");
    public static readonly Option<bool> ForceEvaluateOption = new Option<bool>("--force-evaluate")
    {
        Description = CliCommandStrings.CmdReevaluateOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:RestoreForceEvaluate=true");
    public static readonly Option<string> ArchitectureOption = CommonOptions.ArchitectureOption;
    public static readonly Option<string> OperatingSystemOption = CommonOptions.OperatingSystemOption;
    public static readonly Option<string[]?> GetPropertyOption = CommonOptions.CreateGetPropertyOption();
    public static readonly Option<string[]?> GetItemOption = CommonOptions.CreateGetItemOption();
    public static readonly Option<string[]?> GetTargetResultOption = CommonOptions.CreateGetTargetResultOption();
    public static readonly Option<string[]?> GetResultOutputFileOption = CommonOptions.CreateGetResultOutputFileOption();

    public static Command Create()
    {
        var command = new Command("restore", CliCommandStrings.RestoreAppFullName)
        {
            DocsLink = DocsLink
        };

        command.Arguments.Add(SlnOrProjectOrFileArgument);
        command.Options.Add(DisableBuildServersOption);

        ImplicitRestoreOptions.AddTo(command.Options);

        command.Options.Add(RuntimeOption);
        command.Options.Add(NoDependenciesOption);
        command.Options.Add(VerbosityOption);
        command.Options.Add(InteractiveOption);
        command.Options.Add(ArtifactsPathOption);
        command.Options.Add(UseLockFileOption);
        command.Options.Add(LockedModeOption);
        command.Options.Add(LockFilePathOption);
        command.Options.Add(ForceEvaluateOption);
        command.Options.Add(TargetOption);
        command.Options.Add(NoLogoOption);

        command.Options.Add(ArchitectureOption);
        command.Options.Add(OperatingSystemOption);
        command.Options.Add(GetPropertyOption);
        command.Options.Add(GetItemOption);
        command.Options.Add(GetTargetResultOption);
        command.Options.Add(GetResultOutputFileOption);

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
