// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Tool.Store;

internal sealed class StoreCommandDefinition : Command
{
    private const string Link = "https://aka.ms/dotnet-store";

    public readonly Argument<IEnumerable<string>> Argument = new("argument")
    {
        Arity = ArgumentArity.ZeroOrMore,
    };

    public readonly Option<IEnumerable<string>> ManifestOption = new Option<IEnumerable<string>>("--manifest", "-m")
    {
        Description = CommandDefinitionStrings.ProjectManifestDescription,
        HelpName = CommandDefinitionStrings.ProjectManifest,
        Arity = ArgumentArity.OneOrMore
    }.ForwardAsMany(o =>
    {
        // the first path doesn't need to go through CommandDirectoryContext.ExpandPath
        // since it is a direct argument to MSBuild, not a property
        var materializedString = $"{o!.First()}";

        if (o!.Count() == 1)
        {
            return [materializedString];
        }
        else
        {
            return [materializedString, $"-property:AdditionalProjects={string.Join("%3B", o!.Skip(1).Select(CommandDirectoryContext.GetFullPath))}"];
        }
    }).AllowSingleArgPerToken();

    public readonly Option<string> FrameworkVersionOption = new Option<string>("--framework-version")
    {
        Description = CommandDefinitionStrings.FrameworkVersionOptionDescription,
        HelpName = CommandDefinitionStrings.FrameworkVersionOption
    }.ForwardAsSingle(o => $"-property:RuntimeFrameworkVersion={o}");

    public readonly Option<string> OutputOption = new Option<string>("--output", "-o")
    {
        Description = CommandDefinitionStrings.StoreOutputOptionDescription,
        HelpName = CommandDefinitionStrings.StoreOutputOption
    }.ForwardAsOutputPath("ComposeDir");

    public readonly Option<string> WorkingDirOption = new Option<string>("--working-dir", "-w")
    {
        Description = CommandDefinitionStrings.IntermediateWorkingDirOptionDescription,
        HelpName = CommandDefinitionStrings.IntermediateWorkingDirOption
    }.ForwardAsSingle(o => $"-property:ComposeWorkingDir={CommandDirectoryContext.GetFullPath(o)}");

    public readonly Option<bool> SkipOptimizationOption = new Option<bool>("--skip-optimization")
    {
        Description = CommandDefinitionStrings.SkipOptimizationOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:SkipOptimization=true");

    public readonly Option<bool> SkipSymbolsOption = new Option<bool>("--skip-symbols")
    {
        Description = CommandDefinitionStrings.SkipSymbolsOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:CreateProfilingSymbols=false");

    public readonly Option<string> FrameworkOption = CommonOptions.CreateFrameworkOption(CommandDefinitionStrings.StoreFrameworkOptionDescription);
    public readonly Option<string> RuntimeOption = TargetPlatformOptions.CreateRuntimeOption(CommandDefinitionStrings.StoreRuntimeOptionDescription);
    public readonly Option<Utils.VerbosityOptions?> VerbosityOption = CommonOptions.CreateVerbosityOption();
    public readonly Option<bool> UseCurrentRuntimeOption = CommonOptions.CreateUseCurrentRuntimeOption(CommandDefinitionStrings.CurrentRuntimeOptionDescription);
    public readonly Option<bool> DisableBuildServersOption = CommonOptions.CreateDisableBuildServersOption();
    public readonly Option<bool> NoLogoOption = CommonOptions.CreateNoLogoOption(true);

    public StoreCommandDefinition()
        : base("store", CommandDefinitionStrings.StoreAppDescription)
    {
        this.DocsLink = Link;

        Arguments.Add(Argument);
        Options.Add(ManifestOption);
        Options.Add(FrameworkVersionOption);
        Options.Add(OutputOption);
        Options.Add(WorkingDirOption);
        Options.Add(SkipOptimizationOption);
        Options.Add(SkipSymbolsOption);
        Options.Add(FrameworkOption);
        Options.Add(RuntimeOption);
        Options.Add(VerbosityOption);
        Options.Add(UseCurrentRuntimeOption);
        Options.Add(DisableBuildServersOption);
        Options.Add(NoLogoOption);
    }
}
