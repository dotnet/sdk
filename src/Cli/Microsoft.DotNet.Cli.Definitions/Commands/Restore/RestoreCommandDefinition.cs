// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.StaticCompletions;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Restore;

internal sealed class RestoreCommandDefinition : Command
{
    private const string Link = "https://aka.ms/dotnet-restore";

    public readonly Argument<string[]> SlnOrProjectOrFileArgument = new(CommandDefinitionStrings.SolutionOrProjectOrFileArgumentName)
    {
        Description = CommandDefinitionStrings.SolutionOrProjectOrFileArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public readonly ImplicitRestoreOptions ImplicitRestoreOptions = new(showHelp: true, useShortOptions: true);

    public readonly Option<string[]> TargetOption = CreateTargetOption();
    public readonly Option<Utils.VerbosityOptions> VerbosityOption = CommonOptions.CreateVerbosityOption(Utils.VerbosityOptions.minimal);
    public readonly Option<bool> NoLogoOption = CommonOptions.CreateNoLogoOption();

    public readonly Option<bool> DisableBuildServersOption = CommonOptions.CreateDisableBuildServersOption();

    public readonly TargetPlatformOptions TargetPlatformOptions = new(CreateRuntimeOption());

    public readonly Option<bool> NoDependenciesOption = CreateNoDependenciesOption(showHelp: true);
    public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveMsBuildForwardOption();
    public readonly Option<string> ArtifactsPathOption = CommonOptions.CreateArtifactsPathOption();

    public readonly Option<bool> UseLockFileOption = new Option<bool>("--use-lock-file")
    {
        Description = CommandDefinitionStrings.CmdUseLockFileOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:RestorePackagesWithLockFile=true");

    public readonly Option<bool> LockedModeOption = new Option<bool>("--locked-mode")
    {
        Description = CommandDefinitionStrings.CmdLockedModeOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:RestoreLockedMode=true");

    public readonly Option<string> LockFilePathOption = new Option<string>("--lock-file-path")
    {
        Description = CommandDefinitionStrings.CmdLockFilePathOptionDescription,
        HelpName = CommandDefinitionStrings.CmdLockFilePathOption
    }.ForwardAsSingle(o => $"-property:NuGetLockFilePath={o}");

    public readonly Option<bool> ForceEvaluateOption = new Option<bool>("--force-evaluate")
    {
        Description = CommandDefinitionStrings.CmdReevaluateOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:RestoreForceEvaluate=true");

    public readonly Option<string[]?> GetPropertyOption = CommonOptions.CreateGetPropertyOption();
    public readonly Option<string[]?> GetItemOption = CommonOptions.CreateGetItemOption();
    public readonly Option<string[]?> GetTargetResultOption = CommonOptions.CreateGetTargetResultOption();
    public readonly Option<string[]?> GetResultOutputFileOption = CommonOptions.CreateGetResultOutputFileOption();

    public RestoreCommandDefinition()
        : base("restore", CommandDefinitionStrings.RestoreAppFullName)
    {
        this.DocsLink = Link;

        Arguments.Add(SlnOrProjectOrFileArgument);
        Options.Add(DisableBuildServersOption);

        ImplicitRestoreOptions.AddTo(Options);

        Options.Add(NoDependenciesOption);
        Options.Add(VerbosityOption);
        Options.Add(InteractiveOption);
        Options.Add(ArtifactsPathOption);
        Options.Add(UseLockFileOption);
        Options.Add(LockedModeOption);
        Options.Add(LockFilePathOption);
        Options.Add(ForceEvaluateOption);
        Options.Add(TargetOption);
        Options.Add(NoLogoOption);

        TargetPlatformOptions.AddTo(Options);

        Options.Add(GetPropertyOption);
        Options.Add(GetItemOption);
        Options.Add(GetTargetResultOption);
        Options.Add(GetResultOutputFileOption);
    }

    private static string GetOsFromRid(string rid)
        => rid.Substring(0, rid.LastIndexOf("-", StringComparison.InvariantCulture));

    private static string GetArchFromRid(string rid)
        => rid.Substring(rid.LastIndexOf("-", StringComparison.InvariantCulture) + 1, rid.Length - rid.LastIndexOf("-", StringComparison.InvariantCulture) - 1);

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

    private static Option<IEnumerable<string>> CreateRuntimeOption() => new Option<IEnumerable<string>>("--runtime", "-r")
    {
        Description = CommandDefinitionStrings.CmdRuntimeOptionDescription,
        HelpName = CommandDefinitionStrings.CmdRuntimeOption,
        IsDynamic = true
    }.ForwardAsSingle(RestoreRuntimeArgFunc)
     .AllowSingleArgPerToken();

    public static Option<bool> CreateNoDependenciesOption(bool showHelp)
        => new Option<bool>("--no-dependencies")
        {
            Description = CommandDefinitionStrings.CmdNoDependenciesOptionDescription,
            Arity = ArgumentArity.Zero,
            Hidden = !showHelp
        }.ForwardAs("-property:RestoreRecursive=false");

    public static Option<string[]> CreateTargetOption() => CommonOptions.CreateRequiredMSBuildTargetOption("Restore");


}
