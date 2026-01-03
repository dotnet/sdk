// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Build;
using Microsoft.DotNet.Cli.Commands.Restore;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Pack;

internal static class PackCommandDefinition
{
    public static readonly string DocsLink = "https://aka.ms/dotnet-pack";

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

    public static Command Create()
    {
        var command = new Command("pack", CliCommandStrings.PackAppFullName)
        {
            DocsLink = DocsLink
        };

        command.Arguments.Add(SlnOrProjectOrFileArgument);
        command.Options.Add(OutputOption);
        command.Options.Add(ArtifactsPathOption);
        command.Options.Add(NoBuildOption);
        command.Options.Add(IncludeSymbolsOption);
        command.Options.Add(IncludeSourceOption);
        command.Options.Add(ServiceableOption);
        command.Options.Add(NoLogoOption);
        command.Options.Add(InteractiveOption);
        command.Options.Add(NoRestoreOption);
        command.Options.Add(VerbosityOption);
        command.Options.Add(VersionSuffixOption);
        command.Options.Add(VersionOption);
        command.Options.Add(ConfigurationOption);
        command.Options.Add(DisableBuildServersOption);
        command.Options.Add(TargetOption);
        command.Options.Add(GetPropertyOption);
        command.Options.Add(GetItemOption);
        command.Options.Add(GetTargetResultOption);
        command.Options.Add(GetResultOutputFileOption);

        ImplicitRestoreOptions.AddTo(command.Options);

        command.Options.Add(NoDependenciesOption);
        command.Options.Add(RuntimeOption);

        return command;
    }
}
