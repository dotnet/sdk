// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Build;
using Microsoft.DotNet.Cli.Commands.Restore;

namespace Microsoft.DotNet.Cli.Commands.Pack;

internal static class PackCommandDefinition
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-pack";

    public static readonly Argument<string[]> SlnOrProjectOrFileArgument = new(CliDefinitionResources.SolutionOrProjectOrFileArgumentName)
    {
        Description = CliDefinitionResources.SolutionOrProjectOrFileArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public static readonly Option<string> OutputOption = new Option<string>("--output", "-o")
    {
        Description = CliDefinitionResources.PackCmdOutputDirDescription,
        HelpName = CliDefinitionResources.PackCmdOutputDir
    }.ForwardAsSingle(o => $"-property:PackageOutputPath={CommandDirectoryContext.GetFullPath(o)}");

    public static readonly Option<bool> NoBuildOption = new Option<bool>("--no-build")
    {
        Description = CliDefinitionResources.CmdNoBuildOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:NoBuild=true");

    public static readonly Option<bool> IncludeSymbolsOption = new Option<bool>("--include-symbols")
    {
        Description = CliDefinitionResources.CmdIncludeSymbolsDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:IncludeSymbols=true");

    public static readonly Option<bool> IncludeSourceOption = new Option<bool>("--include-source")
    {
        Description = CliDefinitionResources.CmdIncludeSourceDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:IncludeSource=true");

    public static readonly Option<bool> ServiceableOption = new Option<bool>("--serviceable", "-s")
    {
        Description = CliDefinitionResources.CmdServiceableDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:Serviceable=true");

    public static readonly Option<bool> NoLogoOption = CommonOptions.NoLogoOption();

    public static readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

    public static readonly Option<string?> ConfigurationOption = CommonOptions.ConfigurationOption(CliDefinitionResources.PackConfigurationOptionDescription);

    public static readonly Option<string[]> TargetOption = CommonOptions.RequiredMSBuildTargetOption("Pack", [("_IsPacking", "true")]);
    public static readonly Option<Utils.VerbosityOptions?> VerbosityOption = BuildCommandDefinition.VerbosityOption;

    public static Option<NuGetVersion> VersionOption =
        new Option<NuGetVersion>("--version")
        {
            Description = CliDefinitionResources.PackCmdVersionDescription,
            HelpName = CliDefinitionResources.PackCmdVersion,
            Arity = ArgumentArity.ExactlyOne,
            CustomParser = r =>
            {
                if (r.Tokens.Count == 0)
                    return null;
                var value = r.Tokens[0].Value;
                if (NuGetVersion.TryParse(value, out var version))
                    return version;
                r.AddError(string.Format(CliDefinitionResources.InvalidVersion, value));
                return null;

            }
        }.ForwardAsSingle(o => $"--property:PackageVersion={o}");

    public static Command Create()
    {
        var command = new Command("pack", CliDefinitionResources.PackAppFullName)
        {
            DocsLink = DocsLink
        };

        command.Arguments.Add(SlnOrProjectOrFileArgument);
        command.Options.Add(OutputOption);
        command.Options.Add(CommonOptions.ArtifactsPathOption);
        command.Options.Add(NoBuildOption);
        command.Options.Add(IncludeSymbolsOption);
        command.Options.Add(IncludeSourceOption);
        command.Options.Add(ServiceableOption);
        command.Options.Add(NoLogoOption);
        command.Options.Add(CommonOptions.InteractiveMsBuildForwardOption);
        command.Options.Add(NoRestoreOption);
        command.Options.Add(VerbosityOption);
        command.Options.Add(CommonOptions.VersionSuffixOption);
        command.Options.Add(VersionOption);
        command.Options.Add(ConfigurationOption);
        command.Options.Add(CommonOptions.DisableBuildServersOption);
        command.Options.Add(TargetOption);
        command.Options.Add(CommonOptions.GetPropertyOption);
        command.Options.Add(CommonOptions.GetItemOption);
        command.Options.Add(CommonOptions.GetTargetResultOption);
        command.Options.Add(CommonOptions.GetResultOutputFileOption);

        // Don't include runtime option because we want to include it specifically and allow the short version ("-r") to be used
        RestoreCommandDefinition.AddImplicitRestoreOptions(command, includeRuntimeOption: false, includeNoDependenciesOption: true);
        command.Options.Add(CommonOptions.RuntimeOption(CliDefinitionResources.BuildRuntimeOptionDescription));

        return command;
    }
}
