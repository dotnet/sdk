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

    public static readonly Argument<string[]> SlnOrProjectOrFileArgument = new(CliStrings.SolutionOrProjectOrFileArgumentName)
    {
        Description = CliStrings.SolutionOrProjectOrFileArgumentDescription,
        Arity = ArgumentArity.ZeroOrMore
    };

    public static readonly ImplicitRestoreOptions ImplicitRestoreOptions = new(showHelp: false, useShortOptions: false);

    public static readonly Option<string> OutputOption = new Option<string>("--output", "-o")
    {
        Description = CliCommandStrings.PackCmdOutputDirDescription,
        HelpName = CliCommandStrings.PackCmdOutputDir
    }.ForwardAsSingle(o => $"-property:PackageOutputPath={CommandDirectoryContext.GetFullPath(o)}");

    public static readonly Option<bool> NoBuildOption = new Option<bool>("--no-build")
    {
        Description = CliCommandStrings.CmdNoBuildOptionDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:NoBuild=true");

    public static readonly Option<bool> IncludeSymbolsOption = new Option<bool>("--include-symbols")
    {
        Description = CliCommandStrings.CmdIncludeSymbolsDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:IncludeSymbols=true");

    public static readonly Option<bool> IncludeSourceOption = new Option<bool>("--include-source")
    {
        Description = CliCommandStrings.CmdIncludeSourceDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:IncludeSource=true");

    public static readonly Option<bool> ServiceableOption = new Option<bool>("--serviceable", "-s")
    {
        Description = CliCommandStrings.CmdServiceableDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("-property:Serviceable=true");

    public static readonly Option<bool> NoLogoOption = CommonOptions.CreateNoLogoOption();

    public static readonly Option<bool> NoRestoreOption = CommonOptions.NoRestoreOption;

    public static readonly Option<string?> ConfigurationOption = CommonOptions.CreateConfigurationOption(CliCommandStrings.PackConfigurationOptionDescription);

    public static readonly Option<string[]> TargetOption = CommonOptions.CreateRequiredMSBuildTargetOption("Pack", [("_IsPacking", "true")]);
    public static readonly Option<Utils.VerbosityOptions?> VerbosityOption = BuildCommandDefinition.VerbosityOption;

    public static readonly Option<string> ArtifactsPathOption = CommonOptions.CreateArtifactsPathOption();
    public static readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveMsBuildForwardOption();
    public static readonly Option<string> VersionSuffixOption = CommonOptions.CreateVersionSuffixOption();
    public static readonly Option<bool> DisableBuildServersOption = CommonOptions.CreateDisableBuildServersOption();
    public static readonly Option<string[]?> GetPropertyOption = CommonOptions.CreateGetPropertyOption();
    public static readonly Option<string[]?> GetItemOption = CommonOptions.CreateGetItemOption();
    public static readonly Option<string[]?> GetTargetResultOption = CommonOptions.CreateGetTargetResultOption();
    public static readonly Option<string[]?> GetResultOutputFileOption = CommonOptions.CreateGetResultOutputFileOption();
    public static readonly Option<bool> NoDependenciesOption = RestoreCommandDefinition.CreateNoDependenciesOption(showHelp: false);
    public static readonly Option<string> RuntimeOption = CommonOptions.CreateRuntimeOption(CliCommandStrings.BuildRuntimeOptionDescription);

    public static Option<NuGetVersion> VersionOption =
        new Option<NuGetVersion>("--version")
        {
            Description = CliCommandStrings.PackCmdVersionDescription,
            HelpName = CliCommandStrings.PackCmdVersion,
            Arity = ArgumentArity.ExactlyOne,
            CustomParser = r =>
            {
                if (r.Tokens.Count == 0)
                    return null;
                var value = r.Tokens[0].Value;
                if (NuGetVersion.TryParse(value, out var version))
                    return version;
                r.AddError(string.Format(CliStrings.InvalidVersion, value));
                return null;

            }
        }.ForwardAsSingle(o => $"--property:PackageVersion={o}");

    public PackCommandDefinition()
        : base("pack", CliCommandStrings.PackAppFullName)
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
}
