// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Build;
using Microsoft.DotNet.Cli.Commands.Restore;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Pack;

internal sealed class PackCommandDefinition : Command
{
    private const string Link = "https://aka.ms/dotnet-pack";

    public readonly Argument<string[]> SlnOrProjectOrFileArgument = new(CommandDefinitionStrings.SolutionOrProjectOrFileArgumentName)
    {
        Description = CommandDefinitionStrings.SolutionOrProjectOrFileArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public readonly ImplicitRestoreOptions ImplicitRestoreOptions = new(showHelp: false, useShortOptions: false);

    public readonly Option<string> OutputOption = new Option<string>("--output", "-o")
    {
        Description = CommandDefinitionStrings.PackCmdOutputDirDescription,
        HelpName = CommandDefinitionStrings.PackCmdOutputDir
    }.ForwardAsSingle(o => $"-property:PackageOutputPath={CommandDirectoryContext.GetFullPath(o)}");

    public readonly Option<bool> NoBuildOption = new Option<bool>("--no-build")
    {
        Description = CommandDefinitionStrings.CmdNoBuildOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:NoBuild=true");

    public readonly Option<bool> IncludeSymbolsOption = new Option<bool>("--include-symbols")
    {
        Description = CommandDefinitionStrings.CmdIncludeSymbolsDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:IncludeSymbols=true");

    public readonly Option<bool> IncludeSourceOption = new Option<bool>("--include-source")
    {
        Description = CommandDefinitionStrings.CmdIncludeSourceDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:IncludeSource=true");

    public readonly Option<bool> ServiceableOption = new Option<bool>("--serviceable", "-s")
    {
        Description = CommandDefinitionStrings.CmdServiceableDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:Serviceable=true");

    public readonly Option<bool> NoLogoOption = CommonOptions.CreateNoLogoOption();

    public readonly Option<bool> NoRestoreOption = CommonOptions.CreateNoRestoreOption();

    public readonly Option<string?> ConfigurationOption = CommonOptions.CreateConfigurationOption(CommandDefinitionStrings.PackConfigurationOptionDescription);

    public readonly Option<string[]> TargetOption = CreateTargetOption();

    public readonly Option<Utils.VerbosityOptions?> VerbosityOption = CommonOptions.CreateVerbosityOption();

    public readonly Option<string> ArtifactsPathOption = CommonOptions.CreateArtifactsPathOption();
    public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveMsBuildForwardOption();
    public readonly Option<string> VersionSuffixOption = CommonOptions.CreateVersionSuffixOption();
    public readonly Option<bool> DisableBuildServersOption = CommonOptions.CreateDisableBuildServersOption();
    public readonly Option<string[]?> GetPropertyOption = CommonOptions.CreateGetPropertyOption();
    public readonly Option<string[]?> GetItemOption = CommonOptions.CreateGetItemOption();
    public readonly Option<string[]?> GetTargetResultOption = CommonOptions.CreateGetTargetResultOption();
    public readonly Option<string[]?> GetResultOutputFileOption = CommonOptions.CreateGetResultOutputFileOption();
    public readonly Option<bool> NoDependenciesOption = RestoreCommandDefinition.CreateNoDependenciesOption(showHelp: false);
    public readonly Option<string> RuntimeOption = TargetPlatformOptions.CreateRuntimeOption(CommandDefinitionStrings.BuildRuntimeOptionDescription);

    public readonly Option<NuGetVersion> VersionOption = new Option<NuGetVersion>("--version")
    {
        Description = CommandDefinitionStrings.PackCmdVersionDescription,
        HelpName = CommandDefinitionStrings.PackCmdVersion,
        Arity = ArgumentArity.ExactlyOne,
        CustomParser = r =>
        {
            if (r.Tokens.Count == 0)
                return null;
            var value = r.Tokens[0].Value;
            if (NuGetVersion.TryParse(value, out var version))
                return version;
            r.AddError(string.Format(CommandDefinitionStrings.InvalidVersion, value));
            return null;

        }
    }.ForwardAsSingle(o => $"--property:PackageVersion={o}");

    public PackCommandDefinition()
        : base("pack", CommandDefinitionStrings.PackAppFullName)
    {
        this.DocsLink = Link;

        Arguments.Add(SlnOrProjectOrFileArgument);
        Options.Add(OutputOption);
        Options.Add(ArtifactsPathOption);
        Options.Add(NoBuildOption);
        Options.Add(IncludeSymbolsOption);
        Options.Add(IncludeSourceOption);
        Options.Add(ServiceableOption);
        Options.Add(NoLogoOption);
        Options.Add(InteractiveOption);
        Options.Add(NoRestoreOption);
        Options.Add(VerbosityOption);
        Options.Add(VersionSuffixOption);
        Options.Add(VersionOption);
        Options.Add(ConfigurationOption);
        Options.Add(DisableBuildServersOption);
        Options.Add(TargetOption);
        Options.Add(GetPropertyOption);
        Options.Add(GetItemOption);
        Options.Add(GetTargetResultOption);
        Options.Add(GetResultOutputFileOption);

        ImplicitRestoreOptions.AddTo(Options);

        Options.Add(NoDependenciesOption);
        Options.Add(RuntimeOption);
    }

    public static Option<string[]> CreateTargetOption()
        => CommonOptions.CreateRequiredMSBuildTargetOption("Pack", [("_IsPacking", "true")]);
}
