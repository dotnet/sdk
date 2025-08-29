// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Commands.Run.LaunchSettings;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;

namespace Microsoft.DotNet.Cli.Commands.Run;

public class RunCommand
{
    public bool NoBuild { get; }

    /// <summary>
    /// Full path to a project file to run.
    /// <see langword="null"/> if running without a project file
    /// (then <see cref="EntryPointFileFullPath"/> is not <see langword="null"/>).
    /// </summary>
    public string? ProjectFileFullPath { get; }

    /// <summary>
    /// Full path to an entry-point <c>.cs</c> file to run without a project file.
    /// </summary>
    public string? EntryPointFileFullPath { get; }

    /// <summary>
    /// Whether <c>dotnet run -</c> is being executed.
    /// In that case, <see cref="EntryPointFileFullPath"/> points to a temporary file
    /// containing all text read from the standard input.
    /// </summary>
    public bool ReadCodeFromStdin { get; }

    public ReadOnlyDictionary<string, string>? RestoreProperties { get; }

    /// <summary>
    /// unparsed/arbitrary CLI tokens to be passed to the running application
    /// </summary>
    public string[] ApplicationArgs { get; set; }
    public bool NoRestore { get; }
    public bool NoCache { get; }

    /// <summary>
    /// Parsed structure representing the MSBuild arguments that will be used to build the project.
    /// </summary>
    public MSBuildArgs MSBuildArgs { get; }
    public bool Interactive { get; }

    /// <summary>
    /// Environment variables specified on command line via -e option.
    /// </summary>
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; }

    private bool ShouldBuild => !NoBuild;

    public string? LaunchProfile { get; }
    public bool NoLaunchProfile { get; }

    /// <summary>
    /// The verbosity of the run-portion of this command specifically. If implicit builds are performed, they will always happen
    /// at a quiet verbosity by default, but it's important that we enable separate verbosity for the run command itself.
    /// </summary>
    public VerbosityOptions RunCommandVerbosity { get; private set; }

    /// <summary>
    /// True to ignore command line arguments specified by launch profile.
    /// </summary>
    public bool NoLaunchProfileArguments { get; }

    /// <param name="applicationArgs">unparsed/arbitrary CLI tokens to be passed to the running application</param>
    public RunCommand(
        bool noBuild,
        string? projectFileFullPath,
        string? entryPointFileFullPath,
        string? launchProfile,
        bool noLaunchProfile,
        bool noLaunchProfileArguments,
        bool noRestore,
        bool noCache,
        bool interactive,
        MSBuildArgs msbuildArgs,
        string[] applicationArgs,
        bool readCodeFromStdin,
        IReadOnlyDictionary<string, string> environmentVariables,
        ReadOnlyDictionary<string, string>? msbuildRestoreProperties)
    {
        Debug.Assert(projectFileFullPath is null ^ entryPointFileFullPath is null);
        Debug.Assert(!readCodeFromStdin || entryPointFileFullPath is not null);

        NoBuild = noBuild;
        ProjectFileFullPath = projectFileFullPath;
        EntryPointFileFullPath = entryPointFileFullPath;
        ReadCodeFromStdin = readCodeFromStdin;
        LaunchProfile = launchProfile;
        NoLaunchProfile = noLaunchProfile;
        NoLaunchProfileArguments = noLaunchProfileArguments;
        ApplicationArgs = applicationArgs;
        Interactive = interactive;
        NoRestore = noRestore;
        NoCache = noCache;
        MSBuildArgs = SetupSilentBuildArgs(msbuildArgs);
        EnvironmentVariables = environmentVariables;
        RestoreProperties = msbuildRestoreProperties;
    }

    public int Execute()
    {
        if (!TryGetLaunchProfileSettingsIfNeeded(out var launchSettings))
        {
            return 1;
        }

        Func<ProjectCollection, ProjectInstance>? projectFactory = null;
        RunProperties? cachedRunProperties = null;
        if (ShouldBuild)
        {
            if (string.Equals("true", launchSettings?.DotNetRunMessages, StringComparison.OrdinalIgnoreCase))
            {
                Reporter.Output.WriteLine(CliCommandStrings.RunCommandBuilding);
            }

            EnsureProjectIsBuilt(out projectFactory, out cachedRunProperties);
        }
        else
        {
            if (NoCache)
            {
                throw new GracefulException(CliCommandStrings.CannotCombineOptions, RunCommandParser.NoCacheOption.Name, RunCommandParser.NoBuildOption.Name);
            }

            if (EntryPointFileFullPath is not null)
            {
                Debug.Assert(!ReadCodeFromStdin);
                var command = CreateVirtualCommand();
                command.MarkArtifactsFolderUsed();

                var cacheEntry = command.GetPreviousCacheEntry();
                projectFactory = CanUseRunPropertiesForCscBuiltProgram(BuildLevel.None, cacheEntry) ? null : command.CreateProjectInstance;
                cachedRunProperties = cacheEntry?.Run;
            }
        }

        try
        {
            ICommand targetCommand = GetTargetCommand(projectFactory, cachedRunProperties);
            ApplyLaunchSettingsProfileToCommand(targetCommand, launchSettings);

            // Env variables specified on command line override those specified in launch profile:
            foreach (var (name, value) in EnvironmentVariables)
            {
                targetCommand.EnvironmentVariable(name, value);
            }

            // Ignore Ctrl-C for the remainder of the command's execution
            Console.CancelKeyPress += (sender, e) => { e.Cancel = true; };

            return targetCommand.Execute().ExitCode;
        }
        catch (InvalidProjectFileException e)
        {
            throw new GracefulException(
                string.Format(CliCommandStrings.RunCommandSpecifiedFileIsNotAValidProject, ProjectFileFullPath),
                e);
        }
    }

    internal void ApplyLaunchSettingsProfileToCommand(ICommand targetCommand, ProjectLaunchSettingsModel? launchSettings)
    {
        if (launchSettings == null)
        {
            return;
        }

        if (!string.IsNullOrEmpty(launchSettings.ApplicationUrl))
        {
            targetCommand.EnvironmentVariable("ASPNETCORE_URLS", launchSettings.ApplicationUrl);
        }

        targetCommand.EnvironmentVariable("DOTNET_LAUNCH_PROFILE", launchSettings.LaunchProfileName);

        foreach (var entry in launchSettings.EnvironmentVariables)
        {
            string value = Environment.ExpandEnvironmentVariables(entry.Value);
            //NOTE: MSBuild variables are not expanded like they are in VS
            targetCommand.EnvironmentVariable(entry.Key, value);
        }

        if (!NoLaunchProfileArguments && string.IsNullOrEmpty(targetCommand.CommandArgs) && launchSettings.CommandLineArgs != null)
        {
            targetCommand.SetCommandArgs(launchSettings.CommandLineArgs);
        }
    }

    internal bool TryGetLaunchProfileSettingsIfNeeded(out ProjectLaunchSettingsModel? launchSettingsModel)
    {
        launchSettingsModel = default;
        if (NoLaunchProfile)
        {
            return true;
        }

        var launchSettingsPath = ReadCodeFromStdin ? null : TryFindLaunchSettings(projectOrEntryPointFilePath: ProjectFileFullPath ?? EntryPointFileFullPath!, launchProfile: LaunchProfile);
        if (launchSettingsPath is null)
        {
            return true;
        }

        if (!RunCommandVerbosity.IsQuiet())
        {
            Reporter.Output.WriteLine(string.Format(CliCommandStrings.UsingLaunchSettingsFromMessage, launchSettingsPath));
        }

        string profileName = string.IsNullOrEmpty(LaunchProfile) ? CliCommandStrings.DefaultLaunchProfileDisplayName : LaunchProfile;

        try
        {
            var applyResult = LaunchSettingsManager.TryApplyLaunchSettings(launchSettingsPath, LaunchProfile);
            if (!applyResult.Success)
            {
                Reporter.Error.WriteLine(string.Format(CliCommandStrings.RunCommandExceptionCouldNotApplyLaunchSettings, profileName, applyResult.FailureReason).Bold().Red());
            }
            else
            {
                launchSettingsModel = applyResult.LaunchSettings;
            }
        }
        catch (IOException ex)
        {
            Reporter.Error.WriteLine(string.Format(CliCommandStrings.RunCommandExceptionCouldNotApplyLaunchSettings, profileName).Bold().Red());
            Reporter.Error.WriteLine(ex.Message.Bold().Red());
            return false;
        }

        return true;

        static string? TryFindLaunchSettings(string projectOrEntryPointFilePath, string? launchProfile)
        {
            var buildPathContainer = Path.GetDirectoryName(projectOrEntryPointFilePath)!;

            string propsDirectory;

            // VB.NET projects store the launch settings file in the
            // "My Project" directory instead of a "Properties" directory.
            // TODO: use the `AppDesignerFolder` MSBuild property instead, which captures this logic already
            if (string.Equals(Path.GetExtension(projectOrEntryPointFilePath), ".vbproj", StringComparison.OrdinalIgnoreCase))
            {
                propsDirectory = "My Project";
            }
            else
            {
                propsDirectory = "Properties";
            }

            string launchSettingsPath = CommonRunHelpers.GetPropertiesLaunchSettingsPath(buildPathContainer, propsDirectory);
            bool hasLaunchSetttings = File.Exists(launchSettingsPath);

            string appName = Path.GetFileNameWithoutExtension(projectOrEntryPointFilePath);
            string runJsonPath = CommonRunHelpers.GetFlatLaunchSettingsPath(buildPathContainer, appName);
            bool hasRunJson = File.Exists(runJsonPath);

            if (hasLaunchSetttings)
            {
                if (hasRunJson)
                {
                    Reporter.Output.WriteLine(string.Format(CliCommandStrings.RunCommandWarningRunJsonNotUsed, runJsonPath, launchSettingsPath).Yellow());
                }

                return launchSettingsPath;
            }

            if (hasRunJson)
            {
                return runJsonPath;
            }

            if (!string.IsNullOrEmpty(launchProfile))
            {
                Reporter.Error.WriteLine(string.Format(CliCommandStrings.RunCommandExceptionCouldNotLocateALaunchSettingsFile, launchProfile, $"""
                    {launchSettingsPath}
                    {runJsonPath}
                    """).Bold().Red());
            }

            return null;
        }
    }

    private void EnsureProjectIsBuilt(out Func<ProjectCollection, ProjectInstance>? projectFactory, out RunProperties? cachedRunProperties)
    {
        int buildResult;
        if (EntryPointFileFullPath is not null)
        {
            var command = CreateVirtualCommand();
            buildResult = command.Execute();
            projectFactory = CanUseRunPropertiesForCscBuiltProgram(command.LastBuild.Level, command.LastBuild.Cache?.PreviousEntry) ? null : command.CreateProjectInstance;
            cachedRunProperties = command.LastBuild.Cache?.CurrentEntry.Run;
        }
        else
        {
            Debug.Assert(ProjectFileFullPath is not null);

            projectFactory = null;
            cachedRunProperties = null;
            buildResult = new RestoringCommand(
                MSBuildArgs.CloneWithExplicitArgs([ProjectFileFullPath, .. MSBuildArgs.OtherMSBuildArgs]),
                NoRestore,
                advertiseWorkloadUpdates: false
            ).Execute();
        }

        if (buildResult != 0)
        {
            Reporter.Error.WriteLine();
            throw new GracefulException(CliCommandStrings.RunCommandException);
        }
    }

    private static bool CanUseRunPropertiesForCscBuiltProgram(BuildLevel level, RunFileBuildCacheEntry? previousCache)
    {
        return level == BuildLevel.Csc ||
            (level == BuildLevel.None && previousCache?.BuildLevel == BuildLevel.Csc);
    }

    private VirtualProjectBuildingCommand CreateVirtualCommand()
    {
        Debug.Assert(EntryPointFileFullPath != null);

        var args = MSBuildArgs.RequestedTargets is null or []
            ? MSBuildArgs.CloneWithAdditionalTargets("Build", ComputeRunArgumentsTarget)
            : MSBuildArgs.CloneWithAdditionalTargets(ComputeRunArgumentsTarget);

        return new(
            entryPointFileFullPath: EntryPointFileFullPath,
            msbuildArgs: args)
        {
            NoRestore = NoRestore,
            NoCache = NoCache,
        };
    }

    /// <summary>
    /// Applies run-specific customization to the MSBuild arguments
    /// that will be used to build the project. `run` wants to operate silently if possible,
    /// so we disable as much MSBuild output as possible, unless we're forced to be interactive.
    /// </summary>
    /// <returns></returns>
    private MSBuildArgs SetupSilentBuildArgs(MSBuildArgs msbuildArgs)
    {
        msbuildArgs = msbuildArgs.CloneWithAdditionalArgs("-nologo");

        if (msbuildArgs.Verbosity is VerbosityOptions userVerbosity)
        {
            // if the user had a desired verbosity, we use that for the run command
            RunCommandVerbosity = userVerbosity;
            return msbuildArgs;
        }
        else
        {
            // Apply defaults if the user didn't expressly set the verbosity.
            // Setting RunCommandVerbosity to minimal ensures that we keep the previous launchsettings
            // and related diagnostics messages on by default.
            RunCommandVerbosity = VerbosityOptions.minimal;
            return msbuildArgs.CloneWithVerbosity(VerbosityOptions.quiet);
        }
    }

    internal ICommand GetTargetCommand(Func<ProjectCollection, ProjectInstance>? projectFactory, RunProperties? cachedRunProperties)
    {
        if (projectFactory is null && ProjectFileFullPath is null)
        {
            // If we are running a file-based app and projectFactory is null, it means csc was used instead of full msbuild.
            // So we can skip project evaluation to continue the optimized path.
            Debug.Assert(EntryPointFileFullPath is not null);
            Reporter.Verbose.WriteLine("Getting target command: for csc-built program.");
            return CreateCommandForCscBuiltProgram(EntryPointFileFullPath);
        }

        if (cachedRunProperties != null)
        {
            // We can also skip project evaluation if we already evaluated the project during virtual build
            // or we have cached run properties in previous run (and this is a --no-build run).
            Reporter.Verbose.WriteLine("Getting target command: from cache.");
            return CreateCommandFromRunProperties(cachedRunProperties.WithApplicationArguments(ApplicationArgs));
        }

        Reporter.Verbose.WriteLine("Getting target command: evaluating project.");
        FacadeLogger? logger = LoggerUtility.DetermineBinlogger([.. MSBuildArgs.OtherMSBuildArgs], "dotnet-run");
        var project = EvaluateProject(ProjectFileFullPath, projectFactory, MSBuildArgs, logger);
        ValidatePreconditions(project);
        InvokeRunArgumentsTarget(project, NoBuild, logger, MSBuildArgs);
        logger?.ReallyShutdown();
        var runProperties = RunProperties.FromProject(project).WithApplicationArguments(ApplicationArgs);
        var command = CreateCommandFromRunProperties(runProperties);
        return command;

        static ProjectInstance EvaluateProject(string? projectFilePath, Func<ProjectCollection, ProjectInstance>? projectFactory, MSBuildArgs msbuildArgs, ILogger? binaryLogger)
        {
            Debug.Assert(projectFilePath is not null || projectFactory is not null);

            var globalProperties = msbuildArgs.GlobalProperties?.ToDictionary() ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            globalProperties[Constants.EnableDefaultItems] = "false"; // Disable default item globbing to improve performance
            globalProperties[Constants.MSBuildExtensionsPath] = AppContext.BaseDirectory;

            var collection = new ProjectCollection(globalProperties: globalProperties, loggers: binaryLogger is null ? null : [binaryLogger], toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);

            if (projectFilePath is not null)
            {
                return collection.LoadProject(projectFilePath).CreateProjectInstance();
            }

            Debug.Assert(projectFactory is not null);
            return projectFactory(collection);
        }

        static void ValidatePreconditions(ProjectInstance project)
        {
            if (string.IsNullOrWhiteSpace(project.GetPropertyValue("TargetFramework")))
            {
                ThrowUnableToRunError(project);
            }
        }

        static ICommand CreateCommandFromRunProperties(RunProperties runProperties)
        {
            CommandSpec commandSpec = new(runProperties.Command, runProperties.Arguments);

            var command = CommandFactoryUsingResolver.Create(commandSpec)
                .WorkingDirectory(runProperties.WorkingDirectory);

            SetRootVariableName(
                command,
                runtimeIdentifier: runProperties.RuntimeIdentifier,
                defaultAppHostRuntimeIdentifier: runProperties.DefaultAppHostRuntimeIdentifier,
                targetFrameworkVersion: runProperties.TargetFrameworkVersion);

            return command;
        }

        static void SetRootVariableName(ICommand command, string runtimeIdentifier, string defaultAppHostRuntimeIdentifier, string targetFrameworkVersion)
        {
            var rootVariableName = EnvironmentVariableNames.TryGetDotNetRootVariableName(
                runtimeIdentifier,
                defaultAppHostRuntimeIdentifier,
                targetFrameworkVersion);
            if (rootVariableName != null && string.IsNullOrEmpty(Environment.GetEnvironmentVariable(rootVariableName)))
            {
                command.EnvironmentVariable(rootVariableName, Path.GetDirectoryName(new Muxer().MuxerPath));
            }
        }

        static ICommand CreateCommandForCscBuiltProgram(string entryPointFileFullPath)
        {
            var artifactsPath = VirtualProjectBuildingCommand.GetArtifactsPath(entryPointFileFullPath);
            var exePath = Path.Join(artifactsPath, "bin", "debug", Path.GetFileNameWithoutExtension(entryPointFileFullPath) + FileNameSuffixes.CurrentPlatform.Exe);
            var commandSpec = new CommandSpec(path: exePath, args: null);
            var command = CommandFactoryUsingResolver.Create(commandSpec)
                .WorkingDirectory(Path.GetDirectoryName(entryPointFileFullPath));

            SetRootVariableName(
                command,
                runtimeIdentifier: RuntimeInformation.RuntimeIdentifier,
                defaultAppHostRuntimeIdentifier: RuntimeInformation.RuntimeIdentifier,
                targetFrameworkVersion: $"v{VirtualProjectBuildingCommand.TargetFrameworkVersion}");

            return command;
        }

        static void InvokeRunArgumentsTarget(ProjectInstance project, bool noBuild, FacadeLogger? binaryLogger, MSBuildArgs buildArgs)
        {
            List<ILogger> loggersForBuild = [
                TerminalLogger.CreateTerminalOrConsoleLogger([$"--verbosity:{LoggerVerbosity.Quiet.ToString().ToLowerInvariant()}", ..buildArgs.OtherMSBuildArgs])
            ];
            if (binaryLogger is not null)
            {
                loggersForBuild.Add(binaryLogger);
            }

            if (!project.Build([ComputeRunArgumentsTarget], loggers: loggersForBuild, remoteLoggers: null, out _))
            {
                throw new GracefulException(CliCommandStrings.RunCommandEvaluationExceptionBuildFailed, ComputeRunArgumentsTarget);
            }
        }
    }

    static readonly string ComputeRunArgumentsTarget = "ComputeRunArguments";

    internal static void ThrowUnableToRunError(ProjectInstance project)
    {
        string targetFrameworks = project.GetPropertyValue("TargetFrameworks");
        if (!string.IsNullOrEmpty(targetFrameworks))
        {
            string targetFramework = project.GetPropertyValue("TargetFramework");
            if (string.IsNullOrEmpty(targetFramework))
            {
                throw new GracefulException(CliCommandStrings.RunCommandExceptionUnableToRunSpecifyFramework, "--framework");
            }
        }

        throw new GracefulException(
                string.Format(
                    CliCommandStrings.RunCommandExceptionUnableToRun,
                    project.GetPropertyValue("MSBuildProjectFullPath"),
                    Product.TargetFrameworkVersion,
                    project.GetPropertyValue("OutputType")));
    }

    private static string? DiscoverProjectFilePath(string? filePath, string? projectFileOrDirectoryPath, bool readCodeFromStdin, ref string[] args, out string? entryPointFilePath)
    {
        // If `--file` is explicitly specified, just use that.
        if (filePath != null)
        {
            Debug.Assert(projectFileOrDirectoryPath == null);
            entryPointFilePath = Path.GetFullPath(filePath);
            return null;
        }

        bool emptyProjectOption = false;
        if (string.IsNullOrWhiteSpace(projectFileOrDirectoryPath))
        {
            emptyProjectOption = true;
            projectFileOrDirectoryPath = Directory.GetCurrentDirectory();
        }

        string? projectFilePath = Directory.Exists(projectFileOrDirectoryPath)
            ? TryFindSingleProjectInDirectory(projectFileOrDirectoryPath)
            : projectFileOrDirectoryPath;

        // Check if the project file actually exists when it's specified as a direct file path
        if (projectFilePath is not null && !emptyProjectOption && !File.Exists(projectFilePath))
        {
            throw new GracefulException(CliCommandStrings.CmdNonExistentFileErrorDescription, projectFilePath);
        }

        // If no project exists in the directory and no --project was given,
        // try to resolve an entry-point file instead.
        entryPointFilePath = projectFilePath is null && emptyProjectOption
            ? TryFindEntryPointFilePath(readCodeFromStdin, ref args)
            : null;

        if (entryPointFilePath is null && projectFilePath is null)
        {
            throw new GracefulException(CliCommandStrings.RunCommandExceptionNoProjects, projectFileOrDirectoryPath, "--project");
        }

        return projectFilePath;

        static string? TryFindSingleProjectInDirectory(string directory)
        {
            string[] projectFiles = Directory.GetFiles(directory, "*.*proj");

            if (projectFiles.Length == 0)
            {
                return null;
            }

            if (projectFiles.Length > 1)
            {
                throw new GracefulException(CliCommandStrings.RunCommandExceptionMultipleProjects, directory);
            }

            return projectFiles[0];
        }

        static string? TryFindEntryPointFilePath(bool readCodeFromStdin, ref string[] args)
        {
            if (args is not [{ } arg, ..])
            {
                return null;
            }

            if (!readCodeFromStdin)
            {
                if (VirtualProjectBuildingCommand.IsValidEntryPointPath(arg))
                {
                    arg = Path.GetFullPath(arg);
                }
                else
                {
                    return null;
                }
            }

            args = args[1..];
            return arg;
        }
    }

    public static RunCommand FromArgs(string[] args)
    {
        var parseResult = Parser.Parse(["dotnet", "run", .. args]);
        return FromParseResult(parseResult);
    }

    public static RunCommand FromParseResult(ParseResult parseResult)
    {
        if (parseResult.UsingRunCommandShorthandProjectOption())
        {
            Reporter.Output.WriteLine(CliCommandStrings.RunCommandProjectAbbreviationDeprecated.Yellow());
            parseResult = ModifyParseResultForShorthandProjectOption(parseResult);
        }

        // if the application arguments contain any binlog args then we need to remove them from the application arguments and apply
        // them to the restore args.
        // this is because we can't model the binlog command structure in MSbuild in the System.CommandLine parser, but we need
        // bl information to synchronize the restore and build logger configurations
        var applicationArguments = parseResult.GetValue(RunCommandParser.ApplicationArguments)?.ToList();

        LoggerUtility.SeparateBinLogArguments(applicationArguments, out var binLogArgs, out var nonBinLogArgs);

        var msbuildProperties = parseResult.OptionValuesToBeForwarded(RunCommandParser.GetCommand()).ToList();
        if (binLogArgs.Count > 0)
        {
            msbuildProperties.AddRange(binLogArgs);
        }

        // Only consider `-` to mean "read code from stdin" if it is before double dash `--`
        // (otherwise it should be forwarded to the target application as its command-line argument).
        bool readCodeFromStdin = nonBinLogArgs is ["-", ..] &&
            parseResult.Tokens.TakeWhile(static t => t.Type != TokenType.DoubleDash)
                .Any(static t => t is { Type: TokenType.Argument, Value: "-" });

        string? projectOption = parseResult.GetValue(RunCommandParser.ProjectOption);
        string? fileOption = parseResult.GetValue(RunCommandParser.FileOption);

        if (projectOption != null && fileOption != null)
        {
            throw new GracefulException(CliCommandStrings.CannotCombineOptions, RunCommandParser.ProjectOption.Name, RunCommandParser.FileOption.Name);
        }

        string[] args = [.. nonBinLogArgs];
        string? projectFilePath = DiscoverProjectFilePath(
            filePath: fileOption,
            projectFileOrDirectoryPath: projectOption,
            readCodeFromStdin: readCodeFromStdin,
            ref args,
            out string? entryPointFilePath);

        bool noBuild = parseResult.HasOption(RunCommandParser.NoBuildOption);
        string launchProfile = parseResult.GetValue(RunCommandParser.LaunchProfileOption) ?? string.Empty;

        if (readCodeFromStdin && entryPointFilePath != null)
        {
            Debug.Assert(projectFilePath is null && entryPointFilePath is "-");

            if (noBuild)
            {
                throw new GracefulException(CliCommandStrings.InvalidOptionForStdin, RunCommandParser.NoBuildOption.Name);
            }

            if (!string.IsNullOrWhiteSpace(launchProfile))
            {
                throw new GracefulException(CliCommandStrings.InvalidOptionForStdin, RunCommandParser.LaunchProfileOption.Name);
            }

            // If '-' is specified as the input file, read all text from stdin into a temporary file and use that as the entry point.
            // We create a new directory for each file so other files are not included in the compilation.
            // We fail if the file already exists to avoid reusing the same file for multiple stdin runs (in case the random name is duplicate).
            string directory = VirtualProjectBuildingCommand.GetTempSubpath(Path.GetRandomFileName());
            VirtualProjectBuildingCommand.CreateTempSubdirectory(directory);
            entryPointFilePath = Path.Join(directory, "app.cs");
            using (var stdinStream = Console.OpenStandardInput())
            using (var fileStream = new FileStream(entryPointFilePath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                stdinStream.CopyTo(fileStream);
            }

            Debug.Assert(nonBinLogArgs[0] == "-");
            nonBinLogArgs[0] = entryPointFilePath;
        }

        var msbuildArgs = MSBuildArgs.AnalyzeMSBuildArguments(msbuildProperties, CommonOptions.PropertiesOption, CommonOptions.RestorePropertiesOption, CommonOptions.MSBuildTargetOption(), RunCommandParser.VerbosityOption);

        var command = new RunCommand(
            noBuild: noBuild,
            projectFileFullPath: projectFilePath,
            entryPointFileFullPath: entryPointFilePath,
            launchProfile: launchProfile,
            noLaunchProfile: parseResult.HasOption(RunCommandParser.NoLaunchProfileOption),
            noLaunchProfileArguments: parseResult.HasOption(RunCommandParser.NoLaunchProfileArgumentsOption),
            noRestore: parseResult.HasOption(RunCommandParser.NoRestoreOption) || parseResult.HasOption(RunCommandParser.NoBuildOption),
            noCache: parseResult.HasOption(RunCommandParser.NoCacheOption),
            interactive: parseResult.GetValue(RunCommandParser.InteractiveOption),
            msbuildArgs: msbuildArgs,
            applicationArgs: args,
            readCodeFromStdin: readCodeFromStdin,
            environmentVariables: parseResult.GetValue(CommonOptions.EnvOption) ?? ImmutableDictionary<string, string>.Empty,
            msbuildRestoreProperties: parseResult.GetValue(CommonOptions.RestorePropertiesOption)
        );

        return command;
    }

    public static int Run(ParseResult parseResult)
    {
        parseResult.HandleDebugSwitch();

        return FromParseResult(parseResult).Execute();
    }

    public static ParseResult ModifyParseResultForShorthandProjectOption(ParseResult parseResult)
    {
        // we know the project is going to be one of the following forms:
        //   -p:project
        //   -p project
        // so try to find those and filter them out of the arguments array
        var possibleProject = parseResult.GetRunCommandShorthandProjectValues()!.FirstOrDefault()!; // ! are ok because of precondition check in method called before this.
        var tokensMinusProject = new List<string>();
        var nextTokenMayBeProject = false;
        foreach (var token in parseResult.Tokens)
        {
            if (token.Value == "-p")
            {
                // skip this token, if the next token _is_ the project then we'll skip that too
                // if the next token _isn't_ the project then we'll backfill
                nextTokenMayBeProject = true;
                continue;
            }
            else if (token.Value == possibleProject && nextTokenMayBeProject)
            {
                // skip, we've successfully stripped this option and value entirely
                nextTokenMayBeProject = false;
                continue;
            }
            else if (token.Value.StartsWith("-p") && token.Value.EndsWith(possibleProject))
            {
                // both option and value in the same token, skip and carry on
            }
            else
            {
                if (nextTokenMayBeProject)
                {
                    //we skipped a -p, so backfill it
                    tokensMinusProject.Add("-p");
                }
                nextTokenMayBeProject = false;
            }

            tokensMinusProject.Add(token.Value);
        }

        tokensMinusProject.Add("--project");
        tokensMinusProject.Add(possibleProject);

        var tokensToParse = tokensMinusProject.ToArray();
        var newParseResult = Parser.Parse(tokensToParse);
        return newParseResult;
    }
}
