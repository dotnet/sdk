// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Store;

internal static class StoreCommandParser
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-store";

    public static readonly Argument<IEnumerable<string>> Argument = new("argument")
    {
        Arity = ArgumentArity.ZeroOrMore,
    };

    public static readonly Option<IEnumerable<string>> ManifestOption = new ForwardedOption<IEnumerable<string>>("--manifest", "-m")
    {
        Description = CliCommandStrings.ProjectManifestDescription,
        HelpName = CliCommandStrings.ProjectManifest,
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

    public static readonly Option<string> FrameworkVersionOption = new ForwardedOption<string>("--framework-version")
    {
        Description = CliCommandStrings.FrameworkVersionOptionDescription,
        HelpName = CliCommandStrings.FrameworkVersionOption
    }.ForwardAsSingle(o => $"-property:RuntimeFrameworkVersion={o}");

    public static readonly Option<string> OutputOption = new ForwardedOption<string>("--output", "-o")
    {
        Description = CliCommandStrings.StoreOutputOptionDescription,
        HelpName = CliCommandStrings.StoreOutputOption
    }.ForwardAsOutputPath("ComposeDir");

    public static readonly Option<string> WorkingDirOption = new ForwardedOption<string>("--working-dir", "-w")
    {
        Description = CliCommandStrings.IntermediateWorkingDirOptionDescription,
        HelpName = CliCommandStrings.IntermediateWorkingDirOption
    }.ForwardAsSingle(o => $"-property:ComposeWorkingDir={CommandDirectoryContext.GetFullPath(o)}");

    public static readonly Option<bool> SkipOptimizationOption = new ForwardedOption<bool>("--skip-optimization")
    {
        Description = CliCommandStrings.SkipOptimizationOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:SkipOptimization=true");

    public static readonly Option<bool> SkipSymbolsOption = new ForwardedOption<bool>("--skip-symbols")
    {
        Description = CliCommandStrings.SkipSymbolsOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:CreateProfilingSymbols=false");

    private static readonly Command Command = ConstructCommand();

    public static Command GetCommand()
    {
        return Command;
    }

    private static Command ConstructCommand()
    {
        DocumentedCommand command = new("store", DocsLink, CliCommandStrings.StoreAppDescription);

        command.Arguments.Add(Argument);
        command.Options.Add(ManifestOption);
        command.Options.Add(FrameworkVersionOption);
        command.Options.Add(OutputOption);
        command.Options.Add(WorkingDirOption);
        command.Options.Add(SkipOptimizationOption);
        command.Options.Add(SkipSymbolsOption);
        command.Options.Add(CommonOptions.FrameworkOption(CliCommandStrings.StoreFrameworkOptionDescription));
        command.Options.Add(CommonOptions.RuntimeOption(CliCommandStrings.StoreRuntimeOptionDescription));
        command.Options.Add(CommonOptions.VerbosityOption());
        command.Options.Add(CommonOptions.CurrentRuntimeOption(CliCommandStrings.CurrentRuntimeOptionDescription));
        command.Options.Add(CommonOptions.DisableBuildServersOption);

        command.SetAction(StoreCommand.Run);

        return command;
    }
}
