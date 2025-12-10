// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Store;

internal static class StoreCommandDefinition
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-store";

    public static readonly Argument<IEnumerable<string>> Argument = new("argument")
    {
        Arity = ArgumentArity.ZeroOrMore,
    };

    public static readonly Option<IEnumerable<string>> ManifestOption = new Option<IEnumerable<string>>("--manifest", "-m")
    {
        Description = CliDefinitionResources.ProjectManifestDescription,
        HelpName = CliDefinitionResources.ProjectManifest,
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

    public static readonly Option<string> FrameworkVersionOption = new Option<string>("--framework-version")
    {
        Description = CliDefinitionResources.FrameworkVersionOptionDescription,
        HelpName = CliDefinitionResources.FrameworkVersionOption
    }.ForwardAsSingle(o => $"-property:RuntimeFrameworkVersion={o}");

    public static readonly Option<string> OutputOption = new Option<string>("--output", "-o")
    {
        Description = CliDefinitionResources.StoreOutputOptionDescription,
        HelpName = CliDefinitionResources.StoreOutputOption
    }.ForwardAsOutputPath("ComposeDir");

    public static readonly Option<string> WorkingDirOption = new Option<string>("--working-dir", "-w")
    {
        Description = CliDefinitionResources.IntermediateWorkingDirOptionDescription,
        HelpName = CliDefinitionResources.IntermediateWorkingDirOption
    }.ForwardAsSingle(o => $"-property:ComposeWorkingDir={CommandDirectoryContext.GetFullPath(o)}");

    public static readonly Option<bool> SkipOptimizationOption = new Option<bool>("--skip-optimization")
    {
        Description = CliDefinitionResources.SkipOptimizationOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:SkipOptimization=true");

    public static readonly Option<bool> SkipSymbolsOption = new Option<bool>("--skip-symbols")
    {
        Description = CliDefinitionResources.SkipSymbolsOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:CreateProfilingSymbols=false");

    public static Command Create()
    {
        Command command = new("store", CliDefinitionResources.StoreAppDescription)
        {
            DocsLink = DocsLink
        };

        command.Arguments.Add(Argument);
        command.Options.Add(ManifestOption);
        command.Options.Add(FrameworkVersionOption);
        command.Options.Add(OutputOption);
        command.Options.Add(WorkingDirOption);
        command.Options.Add(SkipOptimizationOption);
        command.Options.Add(SkipSymbolsOption);
        command.Options.Add(CommonOptions.FrameworkOption(CliDefinitionResources.StoreFrameworkOptionDescription));
        command.Options.Add(CommonOptions.RuntimeOption(CliDefinitionResources.StoreRuntimeOptionDescription));
        command.Options.Add(CommonOptions.VerbosityOption());
        command.Options.Add(CommonOptions.CurrentRuntimeOption(CliDefinitionResources.CurrentRuntimeOptionDescription));
        command.Options.Add(CommonOptions.DisableBuildServersOption);
        command.Options.Add(CommonOptions.NoLogoOption(true));

        return command;
    }
}
