// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Cli.Commands.Package.List;

internal sealed class PackageListCommandDefinition : PackageListCommandDefinitionBase
{
    public new const string Name = "list";

    public readonly Option<string?> ProjectOption = PackageCommandDefinition.CreateProjectOption();

    public PackageListCommandDefinition()
        : base(Name)
    {
        Options.Add(ProjectOption);
    }

    public override string? GetFileOrDirectory(ParseResult parseResult)
        => parseResult.GetValue(ProjectOption);
}

internal abstract class PackageListCommandDefinitionBase : Command
{
    public readonly Option OutdatedOption = new Option<bool>("--outdated")
    {
        Description = CliCommandStrings.CmdOutdatedDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--outdated");

    public readonly Option DeprecatedOption = new Option<bool>("--deprecated")
    {
        Description = CliCommandStrings.CmdDeprecatedDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--deprecated");

    public readonly Option VulnerableOption = new Option<bool>("--vulnerable")
    {
        Description = CliCommandStrings.CmdVulnerableDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--vulnerable");

    public readonly Option FrameworkOption = new Option<IEnumerable<string>>("--framework", "-f")
    {
        Description = CliCommandStrings.PackageListCmdFrameworkDescription,
        HelpName = CliCommandStrings.PackageListCmdFramework
    }.ForwardAsManyArgumentsEachPrefixedByOption("--framework")
    .AllowSingleArgPerToken();

    public readonly Option TransitiveOption = new Option<bool>("--include-transitive")
    {
        Description = CliCommandStrings.CmdTransitiveDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--include-transitive");

    public readonly Option PrereleaseOption = new Option<bool>("--include-prerelease")
    {
        Description = CliCommandStrings.CmdPrereleaseDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--include-prerelease");

    public readonly Option HighestPatchOption = new Option<bool>("--highest-patch")
    {
        Description = CliCommandStrings.CmdHighestPatchDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--highest-patch");

    public readonly Option HighestMinorOption = new Option<bool>("--highest-minor")
    {
        Description = CliCommandStrings.CmdHighestMinorDescription,
        Arity = ArgumentArity.Zero
    }.ForwardAs("--highest-minor");

    public readonly Option ConfigOption = new Option<string>("--config", "--configfile")
    {
        Description = CliCommandStrings.CmdConfigDescription,
        HelpName = CliCommandStrings.CmdConfig
    }.ForwardAsMany(o => ["--config", o!]);

    public readonly Option SourceOption = new Option<IEnumerable<string>>("--source", "-s")
    {
        Description = CliCommandStrings.PackageListCmdSourceDescription,
        HelpName = CliCommandStrings.PackageListCmdSource
    }.ForwardAsManyArgumentsEachPrefixedByOption("--source")
    .AllowSingleArgPerToken();

    public readonly Option<bool> InteractiveOption = CommonOptions.CreateInteractiveOption().ForwardIfEnabled("--interactive");

    public readonly Option NoRestore = new Option<bool>("--no-restore")
    {
        Description = CliCommandStrings.CmdNoRestoreDescription,
        Arity = ArgumentArity.Zero
    };

    public readonly Option VerbosityOption = new Option<Utils.VerbosityOptions>("--verbosity", "-v")
    {
        Description = CliStrings.VerbosityOptionDescription,
        HelpName = CliStrings.LevelArgumentName
    }.ForwardAsSingle(o => $"--verbosity:{o}");

    public readonly Option<ReportOutputFormat> FormatOption = new Option<ReportOutputFormat>("--format")
    {
        Description = CliCommandStrings.CmdFormatDescription
    }.ForwardAsSingle(o => $"--format:{o}");

    public readonly Option OutputVersionOption = new Option<int>("--output-version")
    {
        Description = CliCommandStrings.CmdOutputVersionDescription
    }.ForwardAsSingle(o => $"--output-version:{o}");

    public PackageListCommandDefinitionBase(string name)
        : base(name, CliCommandStrings.PackageListAppFullName)
    {
        Options.Add(VerbosityOption);
        Options.Add(OutdatedOption);
        Options.Add(DeprecatedOption);
        Options.Add(VulnerableOption);
        Options.Add(FrameworkOption);
        Options.Add(TransitiveOption);
        Options.Add(PrereleaseOption);
        Options.Add(HighestPatchOption);
        Options.Add(HighestMinorOption);
        Options.Add(ConfigOption);
        Options.Add(SourceOption);
        Options.Add(InteractiveOption);
        Options.Add(FormatOption);
        Options.Add(OutputVersionOption);
        Options.Add(NoRestore);
    }

    public abstract string? GetFileOrDirectory(ParseResult parseResult);

    public void EnforceOptionRules(ParseResult parseResult)
    {
        var mutexOptionCount = 0;
        mutexOptionCount += parseResult.HasOption(DeprecatedOption) ? 1 : 0;
        mutexOptionCount += parseResult.HasOption(OutdatedOption) ? 1 : 0;
        mutexOptionCount += parseResult.HasOption(VulnerableOption) ? 1 : 0;
        if (mutexOptionCount > 1)
        {
            throw new Utils.GracefulException(CliCommandStrings.OptionsCannotBeCombined);
        }
    }
}
