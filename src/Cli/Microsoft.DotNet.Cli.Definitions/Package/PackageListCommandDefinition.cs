// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Package.List;

internal static class PackageListCommandDefinition
{
    public const string Name = "list";

    public static readonly Option OutdatedOption = new Option<bool>("--outdated")
    {
        Description = CliDefinitionResources.CmdOutdatedDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--outdated");

    public static readonly Option DeprecatedOption = new Option<bool>("--deprecated")
    {
        Description = CliDefinitionResources.CmdDeprecatedDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--deprecated");

    public static readonly Option VulnerableOption = new Option<bool>("--vulnerable")
    {
        Description = CliDefinitionResources.CmdVulnerableDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--vulnerable");

    public static readonly Option FrameworkOption = new Option<IEnumerable<string>>("--framework", "-f")
    {
        Description = CliDefinitionResources.PackageListCmdFrameworkDescription,
        HelpName = CliDefinitionResources.PackageListCmdFramework
    }.ForwardAsManyArgumentsEachPrefixedByOption("--framework")
    .AllowSingleArgPerToken();

    public static readonly Option TransitiveOption = new Option<bool>("--include-transitive")
    {
        Description = CliDefinitionResources.CmdTransitiveDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--include-transitive");

    public static readonly Option PrereleaseOption = new Option<bool>("--include-prerelease")
    {
        Description = CliDefinitionResources.CmdPrereleaseDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--include-prerelease");

    public static readonly Option HighestPatchOption = new Option<bool>("--highest-patch")
    {
        Description = CliDefinitionResources.CmdHighestPatchDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--highest-patch");

    public static readonly Option HighestMinorOption = new Option<bool>("--highest-minor")
    {
        Description = CliDefinitionResources.CmdHighestMinorDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--highest-minor");

    public static readonly Option ConfigOption = new Option<string>("--config", "--configfile")
    {
        Description = CliDefinitionResources.CmdConfigDescription,
        HelpName = CliDefinitionResources.CmdConfig
    }.ForwardAsMany(o => ["--config", o]);

    public static readonly Option SourceOption = new Option<IEnumerable<string>>("--source", "-s")
    {
        Description = CliDefinitionResources.PackageListCmdSourceDescription,
        HelpName = CliDefinitionResources.PackageListCmdSource
    }.ForwardAsManyArgumentsEachPrefixedByOption("--source")
    .AllowSingleArgPerToken();

    public static readonly Option InteractiveOption = CommonOptions.InteractiveOption().ForwardIfEnabled("--interactive");

    public static readonly Option NoRestore = new Option<bool>("--no-restore")
    {
        Description = CliDefinitionResources.CmdNoRestoreDescription,
        Arity = ArgumentArity.Zero
    };

    public static readonly Option VerbosityOption = new Option<Utils.VerbosityOptions>("--verbosity", "-v")
    {
        Description = CliStrings.VerbosityOptionDescription,
        HelpName = CliStrings.LevelArgumentName
    }.ForwardAsSingle(o => $"--verbosity:{o}");

    public static readonly Option FormatOption = new Option<ReportOutputFormat>("--format")
    {
        Description = CliDefinitionResources.CmdFormatDescription
    }.ForwardAsSingle(o => $"--format:{o}");

    public static readonly Option OutputVersionOption = new Option<int>("--output-version")
    {
        Description = CliDefinitionResources.CmdOutputVersionDescription
    }.ForwardAsSingle(o => $"--output-version:{o}");

    public static Command Create()
    {
        Command command = new(Name, CliDefinitionResources.PackageListAppFullName);

        command.Options.Add(VerbosityOption);
        command.Options.Add(OutdatedOption);
        command.Options.Add(DeprecatedOption);
        command.Options.Add(VulnerableOption);
        command.Options.Add(FrameworkOption);
        command.Options.Add(TransitiveOption);
        command.Options.Add(PrereleaseOption);
        command.Options.Add(HighestPatchOption);
        command.Options.Add(HighestMinorOption);
        command.Options.Add(ConfigOption);
        command.Options.Add(SourceOption);
        command.Options.Add(InteractiveOption);
        command.Options.Add(FormatOption);
        command.Options.Add(OutputVersionOption);
        command.Options.Add(NoRestore);
        command.Options.Add(PackageCommandDefinition.ProjectOption);

        return command;
    }
}
