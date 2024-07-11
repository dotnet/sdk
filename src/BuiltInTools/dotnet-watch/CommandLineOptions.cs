// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.CommandLine;
using Microsoft.DotNet.Watcher.Tools;
using Microsoft.Extensions.Tools.Internal;

namespace Microsoft.DotNet.Watcher;

internal sealed class CommandLineOptions
{
    private const string Description = @"
Environment variables:

  DOTNET_USE_POLLING_FILE_WATCHER
  When set to '1' or 'true', dotnet-watch will poll the file system for
  changes. This is required for some file systems, such as network shares,
  Docker mounted volumes, and other virtual file systems.

  DOTNET_WATCH
  dotnet-watch sets this variable to '1' on all child processes launched.

  DOTNET_WATCH_ITERATION
  dotnet-watch sets this variable to '1' and increments by one each time
  a file is changed and the command is restarted.

  DOTNET_WATCH_SUPPRESS_EMOJIS
  When set to '1' or 'true', dotnet-watch will not show emojis in the 
  console output.

Remarks:
  The special option '--' is used to delimit the end of the options and
  the beginning of arguments that will be passed to the child dotnet process.

  For example: dotnet watch -- --verbose run

  Even though '--verbose' is an option dotnet-watch supports, the use of '--'
  indicates that '--verbose' should be treated instead as an argument for
  dotnet-run.

Examples:
  dotnet watch run
  dotnet watch test
";

    private const string NoLaunchProfileOptionName = "--no-launch-profile";
    private const string LaunchProfileOptionName = "--launch-profile";
    private const string TargetFrameworkOptionName = "--framework";
    private const string BuildPropertyOptionName = "--property";

    public string? ExplicitCommand { get; init; }
    public string? Project { get; init; }
    public string? TargetFramework { get; init; }
    public IReadOnlyList<(string name, string value)>? BuildProperties { get; init; }
    public bool BinaryLogger { get; init; }
    public bool WatchNoLaunchProfile { get; init; }
    public string? WatchLaunchProfileName { get; init; }
    public bool? CommandNoLaunchProfile { get; init; }
    public string? CommandLaunchProfileName { get; init; }
    public bool Quiet { get; init; }
    public bool Verbose { get; init; }
    public bool List { get; init; }
    public bool NoHotReload { get; init; }
    public bool NonInteractive { get; init; }
    public required IReadOnlyList<string> RemainingArguments { get; init; }

    public string Command => ExplicitCommand ?? "run";

    /// <summary>
    /// Command line parsing attempts to mimic `dotnet run`, in the sense that `run` can be replaced with `watch` and the arguments are processed the same way.
    /// E.g. `dotnet run x y z` runs the app with arguments `x`, `y`, `z`, and so does `dotnet watch x y z` or `dotnet watch run x y z`.
    ///
    /// `dotnet watch` also uses Hot Reload mode by default. `--no-hot-reload` can be passed to disable.
    /// However, we can't run `build` and `test` commands in Hot Reload mode. To support these commands we need to special-case them.
    ///
    /// This design prevents us from supporting arbitrary commands and passing the verb and all arguments through.
    ///
    /// To pass `run`, `build` and `test` commands through, we use the `--` delimiter.
    /// E.g. `dotnet watch -- test x y z` runs the project with arguments `x`, `y`, `z` in Hot Reload mode.
    /// </summary>
    public static CommandLineOptions? Parse(string[] args, IReporter reporter, out int errorCode, TextWriter? output = null, TextWriter? error = null)
    {
        var quietOption = new CliOption<bool>("--quiet", "-q")
        {
            Description = "Suppresses all output except warnings and errors"
        };

        var verboseOption = new CliOption<bool>("--verbose", "-v")
        {
            Description = "Show verbose output"
        };

        verboseOption.Validators.Add(v =>
        {
            if (v.GetResult(quietOption) is not null && v.GetResult(verboseOption) is not null)
            {
                v.AddError(Resources.Error_QuietAndVerboseSpecified);
            }
        });

        var listOption = new CliOption<bool>("--list") { Description = "Lists all discovered files without starting the watcher." };
        var shortProjectOption = new CliOption<string>("-p") { Description = "The project to watch.", Hidden = true };
        var longProjectOption = new CliOption<string>("--project") { Description = "The project to watch" };

        // launch profile used by dotnet-watch
        var launchProfileRootOption = new CliOption<string>(LaunchProfileOptionName, "-lp")
        {
            Description = "The launch profile to start the project with (case-sensitive)."
        };
        var noLaunchProfileRootOption = new CliOption<bool>(NoLaunchProfileOptionName)
        {
            Description = "Do not attempt to use launchSettings.json to configure the application."
        };

        // launch profile used by dotnet-run
        var launchProfileCommandOption = new CliOption<string>(LaunchProfileOptionName, "-lp") { Hidden = true };
        var noLaunchProfileCommandOption = new CliOption<bool>(NoLaunchProfileOptionName) { Hidden = true };

        var targetFrameworkOption = new CliOption<string>("--framework", "-f")
        {
            Description = "The target framework to run for. The target framework must also be specified in the project file."
        };
        var propertyOption = new CliOption<string[]>("--property")
        {
            Description = "Properties to be passed to MSBuild."
        };

        propertyOption.Validators.Add(v =>
        {
            var invalidProperty = v.GetValue(propertyOption)?.FirstOrDefault(
                property => !(property.IndexOf('=') is > 0 and var index && index < property.Length - 1 && property[..index].Trim().Length > 0));

            if (invalidProperty != null)
            {
                v.AddError($"Invalid property format: '{invalidProperty}'. Expected 'name=value'.");
            }
        });

        var noHotReloadOption = new CliOption<bool>("--no-hot-reload") { Description = "Suppress hot reload for supported apps." };
        var nonInteractiveOption = new CliOption<bool>("--non-interactive")
        {
            Description = "Runs dotnet-watch in non-interactive mode. This option is only supported when running with Hot Reload enabled. " +
            "Use this option to prevent console input from being captured."
        };

        var remainingWatchArgs = new CliArgument<string[]>("forwardedArgs") { Description = "Arguments to pass to the child dotnet process." };
        var remainingCommandArgs = new CliArgument<string[]>("remainingCommandArgs");

        var runCommand = new CliCommand("run") { Hidden = true };
        var testCommand = new CliCommand("test") { Hidden = true };
        var buildCommand = new CliCommand("build") { Hidden = true };
        var rootCommand = new CliRootCommand(Description);

        void AddSymbols(CliCommand command)
        {
            command.Options.Add(quietOption);
            command.Options.Add(verboseOption);

            if (command == runCommand || command == rootCommand)
            {
                command.Options.Add(noHotReloadOption);
            }

            command.Options.Add(nonInteractiveOption);
            command.Options.Add(longProjectOption);
            command.Options.Add(shortProjectOption);

            if (command == rootCommand)
            {
                command.Options.Add(launchProfileRootOption);
                command.Options.Add(noLaunchProfileRootOption);
            }
            else
            {
                command.Options.Add(launchProfileCommandOption);
                command.Options.Add(noLaunchProfileCommandOption);
            }

            command.Options.Add(targetFrameworkOption);
            command.Options.Add(propertyOption);
            command.Options.Add(listOption);

            command.Arguments.Add(remainingCommandArgs);
        };

        foreach (var command in new[] { runCommand, testCommand, buildCommand })
        {
            AddSymbols(command);
            rootCommand.Subcommands.Add(command);
            command.SetAction(parseResult => Handler(parseResult, command.Name));
        }

        CommandLineOptions? options = null;
        AddSymbols(rootCommand);
        rootCommand.SetAction(parseResult => Handler(parseResult, commandName: null));

        void Handler(ParseResult parseResult, string? commandName)
        {
            var projectValue = parseResult.GetValue(longProjectOption);
            if (string.IsNullOrEmpty(projectValue))
            {
                var projectShortValue = parseResult.GetValue(shortProjectOption);
                if (!string.IsNullOrEmpty(projectShortValue))
                {
                    reporter.Warn(Resources.Warning_ProjectAbbreviationDeprecated);
                    projectValue = projectShortValue;
                }
            }

            string[] remainingArgs =
            [
                .. parseResult.GetValue(remainingWatchArgs),
                .. parseResult.GetValue(remainingCommandArgs)
            ];

            options = new()
            {
                ExplicitCommand = commandName,
                Quiet = parseResult.GetValue(quietOption),
                List = parseResult.GetValue(listOption),
                NoHotReload = parseResult.GetValue(noHotReloadOption),
                NonInteractive = parseResult.GetValue(nonInteractiveOption),
                Verbose = parseResult.GetValue(verboseOption),
                Project = projectValue,
                WatchLaunchProfileName = parseResult.GetValue(launchProfileRootOption),
                WatchNoLaunchProfile = parseResult.GetValue(noLaunchProfileRootOption),
                CommandLaunchProfileName = (commandName != null) ? parseResult.GetValue(launchProfileCommandOption) : null,
                CommandNoLaunchProfile = (commandName != null) ? parseResult.GetValue(noLaunchProfileCommandOption) : null,
                TargetFramework = parseResult.GetValue(targetFrameworkOption),
                BuildProperties = parseResult.GetValue(propertyOption)?
                    .Select(p => (p[..p.IndexOf('=')].Trim(), p[(p.IndexOf('=') + 1)..])).ToArray(),
                RemainingArguments = remainingArgs,
            };
        }

        errorCode = new CliConfiguration(rootCommand)
        {
            Output = output ?? Console.Out,
            Error = error ?? Console.Error
        }.Invoke(args);

        return options;
    }

    public IReadOnlyList<string> GetLaunchProcessArguments(bool hotReload, IReporter reporter, out bool watchNoLaunchProfile, out string? watchLaunchProfileName)
    {
        var argsBuilder = new List<string>();
        if (!hotReload)
        {
            // Arguments are passed to dotnet and the first argument is interpreted as a command.
            argsBuilder.Add(Command);

            // add options that are applicable to dotnet run:

            if (TargetFramework != null)
            {
                argsBuilder.Add(TargetFrameworkOptionName);
                argsBuilder.Add(TargetFramework);
            }

            if (BuildProperties != null)
            {
                foreach (var (name, value) in BuildProperties)
                {
                    if (Command == "build")
                    {
                        argsBuilder.Add($"/p:{name}={value}");
                    }
                    else
                    {
                        argsBuilder.Add(BuildPropertyOptionName);
                        argsBuilder.Add($"{name}={value}");
                    }
                }
            }
        }

        // launch profile:
        if (hotReload)
        {
            watchNoLaunchProfile = WatchNoLaunchProfile || CommandNoLaunchProfile == true;
            watchLaunchProfileName = WatchLaunchProfileName ?? CommandLaunchProfileName;

            if (WatchLaunchProfileName != null && CommandLaunchProfileName != null)
            {
                reporter.Warn($"Using launch profile name '{WatchLaunchProfileName}', ignoring '{CommandLaunchProfileName}'.");
            }
        }
        else
        {
            var runNoLaunchProfile = (ExplicitCommand != null) ? CommandNoLaunchProfile == true : WatchNoLaunchProfile;
            watchNoLaunchProfile = WatchNoLaunchProfile;

            var runLaunchProfileName = (ExplicitCommand != null) ? CommandLaunchProfileName : WatchLaunchProfileName;
            watchLaunchProfileName = WatchLaunchProfileName;

            if (runNoLaunchProfile)
            {
                argsBuilder.Add(NoLaunchProfileOptionName);
            }

            if (runLaunchProfileName != null)
            {
                argsBuilder.Add(LaunchProfileOptionName);
                argsBuilder.Add(runLaunchProfileName);
            }
        }

        argsBuilder.AddRange(RemainingArguments);

        return argsBuilder.ToArray();
    }
}
