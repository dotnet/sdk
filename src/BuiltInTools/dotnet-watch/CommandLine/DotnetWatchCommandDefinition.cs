// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli.CommandLine;

namespace Microsoft.DotNet.Watch;

internal sealed class DotnetWatchCommandDefinition : RootCommand
{
    // dotnet-watch specific options:
    public readonly Option<bool> QuietOption = new("--quiet", "-q") { Description = Resources.Help_Quiet, Arity = ArgumentArity.Zero };
    public readonly Option<bool> VerboseOption = new("--verbose") { Description = Resources.Help_Verbose, Arity = ArgumentArity.Zero };
    public readonly Option<bool> ListOption = new("--list") { Description = Resources.Help_List, Arity = ArgumentArity.Zero };
    public readonly Option<bool> NoHotReloadOption = new("--no-hot-reload") { Description = Resources.Help_NoHotReload, Arity = ArgumentArity.Zero };
    public readonly Option<bool> NonInteractiveOption = new("--non-interactive") { Description = Resources.Help_NonInteractive, Arity = ArgumentArity.Zero };

    // Options we need to know about. They are passed through to the subcommand if the subcommand supports them.
    public readonly Option<string> ShortProjectOption = new("-p") { Hidden = true, Arity = ArgumentArity.ZeroOrOne, AllowMultipleArgumentsPerToken = false };
    public readonly Option<string> LongProjectOption = new("--project") { Hidden = true, Arity = ArgumentArity.ZeroOrOne, AllowMultipleArgumentsPerToken = false };
    public readonly Option<string> FileOption = new("--file") { Hidden = true, Arity = ArgumentArity.ZeroOrOne, AllowMultipleArgumentsPerToken = false };
    public readonly Option<string> LaunchProfileOption = new("--launch-profile", "-lp") { Hidden = true, Arity = ArgumentArity.ZeroOrOne, AllowMultipleArgumentsPerToken = false };
    public readonly Option<bool> NoLaunchProfileOption = new("--no-launch-profile") { Hidden = true, Arity = ArgumentArity.Zero };

    public DotnetWatchCommandDefinition()
        : base(Resources.Help)
    {
        Directives.Add(new EnvironmentVariablesDirective());

        // We process all tokens that do not match any of the above options
        // to find the subcommand (the first unmatched token preceding "--")
        // and all its options and arguments.
        TreatUnmatchedTokensAsErrors = false;

        Options.Add(QuietOption);
        Options.Add(VerboseOption);
        Options.Add(ListOption);
        Options.Add(NoHotReloadOption);
        Options.Add(NonInteractiveOption);

        Options.Add(LongProjectOption);
        Options.Add(ShortProjectOption);
        Options.Add(FileOption);
        Options.Add(LaunchProfileOption);
        Options.Add(NoLaunchProfileOption);

        VerboseOption.Validators.Add(v =>
        {
            if (v.HasOption(QuietOption) && v.HasOption(VerboseOption))
            {
                v.AddError(string.Format(Resources.Cannot_specify_both_0_and_1_options, QuietOption.Name, VerboseOption.Name));
            }

            var hasLongProjectOption = v.HasOption(LongProjectOption);
            var hasShortProjectOption = v.HasOption(ShortProjectOption);

            if (hasLongProjectOption && hasShortProjectOption)
            {
                v.AddError(string.Format(Resources.Cannot_specify_both_0_and_1_options, LongProjectOption.Name, ShortProjectOption.Name));
            }

            if (v.HasOption(FileOption) && (hasLongProjectOption || hasShortProjectOption))
            {
                v.AddError(string.Format(
                    Resources.Cannot_specify_both_0_and_1_options,
                    FileOption.Name,
                    hasLongProjectOption ? LongProjectOption.Name : ShortProjectOption.Name));
            }
        });
    }

    public bool IsWatchOption(Option option)
        => option == QuietOption ||
           option == VerboseOption ||
           option == ListOption ||
           option == NoHotReloadOption ||
           option == NonInteractiveOption;
}
