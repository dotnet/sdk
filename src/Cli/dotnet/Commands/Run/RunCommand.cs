// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Collections.ObjectModel;
using System.CommandLine;
using System.CommandLine.Parsing;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.CommandLine;
using Microsoft.DotNet.Cli.Commands.Restore;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.FileBasedPrograms;
using Microsoft.DotNet.ProjectTools;
using Microsoft.DotNet.Utilities;

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

    public string ProjectOrEntryPointPath =>
        ProjectFileFullPath ?? EntryPointFileFullPath!;

    /// <summary>
    /// Whether <c>dotnet run -</c> is being executed.
    /// In that case, <see cref="EntryPointFileFullPath"/> points to a temporary file
    /// containing all text read from the standard input.
    /// </summary>
    public bool ReadCodeFromStdin { get; }

    /// <summary>
    /// unparsed/arbitrary CLI tokens to be passed to the running application
    /// </summary>
    public string[] ApplicationArgs { get; set; }
    public bool NoRestore { get; }
    public bool NoCache { get; }

    /// <summary>
    /// Parsed structure representing the MSBuild arguments that will be used to build the project.
    ///
    /// Note: This property has a private setter and is mutated within the class when framework selection modifies it.
    /// This mutability is necessary to allow the command to update MSBuild arguments after construction based on framework selection.
    /// </summary>
    public MSBuildArgs MSBuildArgs { get; private set; }
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

    /// <summary>
    /// Device identifier to use for running the application.
    /// </summary>
    public string? Device { get; }

    /// <summary>
    /// Whether to list available devices and exit.
    /// </summary>
    public bool ListDevices { get; }

    /// <summary>
    /// Tracks whether restore was performed during device selection phase.
    /// If true, we should skip restore in the build phase to avoid redundant work.
    /// </summary>
    private bool _restoreDoneForDeviceSelection;

    /// <param name="applicationArgs">unparsed/arbitrary CLI tokens to be passed to the running application</param>
    public RunCommand(
        bool noBuild,
        string? projectFileFullPath,
        string? entryPointFileFullPath,
        string? launchProfile,
        bool noLaunchProfile,
        bool noLaunchProfileArguments,
        string? device,
        bool listDevices,
        bool noRestore,
        bool noCache,
        bool interactive,
        MSBuildArgs msbuildArgs,
        string[] applicationArgs,
        bool readCodeFromStdin,
        IReadOnlyDictionary<string, string> environmentVariables)
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
        Device = device;
        ListDevices = listDevices;
        ApplicationArgs = applicationArgs;
        Interactive = interactive;
        NoRestore = noRestore;
        NoCache = noCache;
        MSBuildArgs = SetupSilentBuildArgs(msbuildArgs);
        EnvironmentVariables = environmentVariables;
    }

    public int Execute()
    {
        if (NoBuild && NoCache)
        {
            throw new GracefulException(CliCommandStrings.CannotCombineOptions, RunCommandDefinition.NoCacheOptionName, RunCommandDefinition.NoBuildOptionName);
        }

        // Create a single logger for all MSBuild operations (device selection + build/run)
        // File-based runs (.cs files) don't support device selection and should use the existing logger behavior
        FacadeLogger? logger = ProjectFileFullPath is not null 
            ? LoggerUtility.DetermineBinlogger([.. MSBuildArgs.OtherMSBuildArgs], "dotnet-run")
            : null;
        try
        {
            // Pre-run evaluation: Handle target framework and device selection for project-based scenarios
            using var selector = ProjectFileFullPath is not null
                ? new RunCommandSelector(ProjectFileFullPath, Interactive, MSBuildArgs, logger)
                : null;
            if (selector is not null && !TrySelectTargetFrameworkAndDeviceIfNeeded(selector))
            {
                // If --list-devices was specified, this is a successful exit
                return ListDevices ? 0 : 1;
            }

            // For file-based projects, check for multi-targeting before building
            if (EntryPointFileFullPath is not null && !TrySelectTargetFrameworkForFileBasedProject())
            {
                return 1;
            }

            var launchProfileParseResult = ReadLaunchProfileSettings();
            if (launchProfileParseResult.FailureReason != null)
            {
                Reporter.Error.WriteLine(string.Format(CliCommandStrings.RunCommandExceptionCouldNotApplyLaunchSettings, LaunchProfileParser.GetLaunchProfileDisplayName(LaunchProfile), launchProfileParseResult.FailureReason).Bold().Red());
            }

            Func<ProjectCollection, ProjectInstance>? projectFactory = null;
            RunProperties? cachedRunProperties = null;
            VirtualProjectBuildingCommand? projectBuilder = null;
            if (ShouldBuild)
            {
                if (launchProfileParseResult.Profile?.DotNetRunMessages == true)
                {
                    Reporter.Output.WriteLine(CliCommandStrings.RunCommandBuilding);
                }

                EnsureProjectIsBuilt(out projectFactory, out cachedRunProperties, out projectBuilder);
            }
            else if (EntryPointFileFullPath is not null && launchProfileParseResult.Profile is not ExecutableLaunchProfile)
            {
                // The entry-point is not used to run the application if the launch profile specifies Executable command. 

                Debug.Assert(!ReadCodeFromStdin);
                projectBuilder = CreateProjectBuilder();
                projectBuilder.MarkArtifactsFolderUsed();

                var cacheEntry = projectBuilder.GetPreviousCacheEntry();
                projectFactory = CanUseRunPropertiesForCscBuiltProgram(BuildLevel.None, cacheEntry) ? null : projectBuilder.CreateProjectInstance;
                cachedRunProperties = cacheEntry?.Run;
            }

            // Deploy step: Call DeployToDevice target if available
            // This must run even with --no-build, as the user may have selected a different device
            if (selector is not null && !selector.TryDeployToDevice())
            {
                // Only error if we have a valid project (not a .sln file, etc.)
                if (selector.HasValidProject)
                {
                    throw new GracefulException(CliCommandStrings.RunCommandDeployFailed);
                }
            }

            var targetCommand = GetTargetCommand(launchProfileParseResult.Profile, projectFactory, cachedRunProperties, logger);

            // Send telemetry about the run operation
            SendRunTelemetry(launchProfileParseResult.Profile, projectBuilder);

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
        finally
        {
            logger?.ReallyShutdown();
        }
    }

    internal ICommand GetTargetCommand(LaunchProfile? launchSettings, Func<ProjectCollection, ProjectInstance>? projectFactory, RunProperties? cachedRunProperties, FacadeLogger? logger)
        => launchSettings switch
        {
            null => GetTargetCommandForProject(launchSettings: null, projectFactory, cachedRunProperties, logger),
            ProjectLaunchProfile projectSettings => GetTargetCommandForProject(projectSettings, projectFactory, cachedRunProperties, logger),
            ExecutableLaunchProfile executableSettings => GetTargetCommandForExecutable(executableSettings),
            _ => throw new InvalidOperationException()
        };

    /// <summary>
    /// Checks if target framework selection and device selection are needed.
    /// Uses a single RunCommandSelector instance for both operations, re-evaluating
    /// the project after framework selection to get the correct device list.
    /// </summary>
    /// <param name="selector">The RunCommandSelector instance to use for selection</param>
    /// <returns>True if we can continue, false if we should exit</returns>
    private bool TrySelectTargetFrameworkAndDeviceIfNeeded(RunCommandSelector selector)
    {
        var globalProperties = CommonRunHelpers.GetGlobalPropertiesFromArgs(MSBuildArgs);
        
        // If user specified --device on command line, add it to global properties and MSBuildArgs
        if (!string.IsNullOrWhiteSpace(Device))
        {
            globalProperties["Device"] = Device;
            var properties = new Dictionary<string, string> { { "Device", Device } };
            var additionalProperties = new ReadOnlyDictionary<string, string>(properties);
            MSBuildArgs = MSBuildArgs.CloneWithAdditionalProperties(additionalProperties);
        }

        // Optimization: If BOTH framework AND device are already specified (and we're not listing devices), 
        // we can skip both framework selection and device selection entirely
        bool hasFramework = globalProperties.TryGetValue("TargetFramework", out var existingFramework) && !string.IsNullOrWhiteSpace(existingFramework);
        bool hasDevice = globalProperties.TryGetValue("Device", out var preSpecifiedDevice) && !string.IsNullOrWhiteSpace(preSpecifiedDevice);
        
        if (!ListDevices && hasFramework && hasDevice)
        {
            // Both framework and device are pre-specified
            return true;
        }

        // Step 1: Select target framework if needed
        if (!selector.TrySelectTargetFramework(out string? selectedFramework))
        {
            return false;
        }

        if (selectedFramework is not null)
        {
            ApplySelectedFramework(selectedFramework);
            
            // Re-evaluate project with the selected framework so device selection sees the right devices
            var properties = CommonRunHelpers.GetGlobalPropertiesFromArgs(MSBuildArgs);
            selector.InvalidateGlobalProperties(properties);
        }

        // Step 2: Check if device is now pre-specified after framework selection
        if (!ListDevices && hasDevice)
        {
            // Device was pre-specified, we can skip device selection
            return true;
        }

        // Step 3: Select device if needed
        if (selector.TrySelectDevice(
            ListDevices,
            NoRestore,
            out string? selectedDevice,
            out string? runtimeIdentifier,
            out _restoreDoneForDeviceSelection))
        {
            // If a device was selected (either by user or by prompt), apply it to MSBuildArgs
            if (selectedDevice is not null)
            {
                var properties = new Dictionary<string, string> { { "Device", selectedDevice } };

                // If the device provided a RuntimeIdentifier, add it too
                if (!string.IsNullOrEmpty(runtimeIdentifier))
                {
                    properties["RuntimeIdentifier"] = runtimeIdentifier;

                    // If the device added a RuntimeIdentifier, we need to re-restore with that RID
                    // because the previous restore (if any) didn't include it
                    _restoreDoneForDeviceSelection = false;
                }

                var additionalProperties = new ReadOnlyDictionary<string, string>(properties);
                MSBuildArgs = MSBuildArgs.CloneWithAdditionalProperties(additionalProperties);
            }

            // If ListDevices was set, we return true but the caller will exit after listing
            return !ListDevices;
        }

        return false;
    }

    /// <summary>
    /// Checks if target framework selection is needed for file-based projects.
    /// Parses directives from the source file to detect multi-targeting.
    /// </summary>
    /// <returns>True if we can continue, false if we should exit</returns>
    private bool TrySelectTargetFrameworkForFileBasedProject()
    {
        Debug.Assert(EntryPointFileFullPath is not null);

        var globalProperties = CommonRunHelpers.GetGlobalPropertiesFromArgs(MSBuildArgs);
        
        // If a framework is already specified via --framework, no need to check
        if (globalProperties.TryGetValue("TargetFramework", out var existingFramework) && !string.IsNullOrWhiteSpace(existingFramework))
        {
            return true;
        }

        // Get frameworks from source file directives
        var frameworks = GetTargetFrameworksFromSourceFile(EntryPointFileFullPath);
        if (frameworks is null || frameworks.Length == 0)
        {
            return true; // Not multi-targeted
        }

        // Use RunCommandSelector to handle multi-target selection (or single framework selection)
        if (RunCommandSelector.TrySelectTargetFramework(frameworks, Interactive, out string? selectedFramework))
        {
            ApplySelectedFramework(selectedFramework);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Parses a source file to extract target frameworks from directives.
    /// </summary>
    /// <returns>Array of frameworks if TargetFrameworks is specified, null otherwise</returns>
    private static string[]? GetTargetFrameworksFromSourceFile(string sourceFilePath)
    {
        var sourceFile = SourceFile.Load(sourceFilePath);
        var directives = FileLevelDirectiveHelpers.FindDirectives(sourceFile, reportAllErrors: false, ErrorReporters.IgnoringReporter);
        
        var targetFrameworksDirective = directives.OfType<CSharpDirective.Property>()
            .FirstOrDefault(p => string.Equals(p.Name, "TargetFrameworks", StringComparison.OrdinalIgnoreCase));
        
        if (targetFrameworksDirective is null)
        {
            return null;
        }

        return targetFrameworksDirective.Value.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    /// <summary>
    /// Applies the selected target framework to MSBuildArgs if a framework was provided.
    /// </summary>
    /// <param name="selectedFramework">The framework to apply, or null if no framework selection was needed</param>
    private void ApplySelectedFramework(string? selectedFramework)
    {
        // If selectedFramework is null, it means no framework selection was needed
        // (e.g., user already specified --framework, or single-target project)
        if (selectedFramework is not null)
        {
            var additionalProperties = new ReadOnlyDictionary<string, string>(
                new Dictionary<string, string> { { "TargetFramework", selectedFramework } });
            MSBuildArgs = MSBuildArgs.CloneWithAdditionalProperties(additionalProperties);
        }
    }

    private ICommand GetTargetCommandForExecutable(ExecutableLaunchProfile launchSettings)
    {
        var workingDirectory = launchSettings.WorkingDirectory ?? Path.GetDirectoryName(ProjectOrEntryPointPath);

        var commandArgs = (NoLaunchProfileArguments || ApplicationArgs is not [])
            ? ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(ApplicationArgs)
            : launchSettings.CommandLineArgs ?? "";

        var commandSpec = new CommandSpec(launchSettings.ExecutablePath, commandArgs);
        var command = CommandFactoryUsingResolver.Create(commandSpec)
            .WorkingDirectory(workingDirectory);

        SetEnvironmentVariables(command, launchSettings);

        return command;
    }

    private void SetEnvironmentVariables(ICommand command, LaunchProfile? launchSettings)
    {
        // Handle Project-specific settings
        if (launchSettings is ProjectLaunchProfile projectSettings)
        {
            if (!string.IsNullOrEmpty(projectSettings.ApplicationUrl))
            {
                command.EnvironmentVariable("ASPNETCORE_URLS", projectSettings.ApplicationUrl);
            }
        }

        if (launchSettings != null)
        {
            command.EnvironmentVariable("DOTNET_LAUNCH_PROFILE", launchSettings.LaunchProfileName);

            foreach (var entry in launchSettings.EnvironmentVariables)
            {
                command.EnvironmentVariable(entry.Key, entry.Value);
            }
        }

        // Env variables specified on command line override those specified in launch profile:
        foreach (var (name, value) in EnvironmentVariables)
        {
            command.EnvironmentVariable(name, value);
        }
    }

    internal LaunchProfileParseResult ReadLaunchProfileSettings()
    {
        if (NoLaunchProfile)
        {
            return LaunchProfileParseResult.Success(model: null);
        }

        var launchSettingsPath = ReadCodeFromStdin
            ? null
            : LaunchSettings.TryFindLaunchSettingsFile(
                projectOrEntryPointFilePath: ProjectFileFullPath ?? EntryPointFileFullPath!,
                launchProfile: LaunchProfile,
                static (message, isError) => (isError ? Reporter.Error : Reporter.Output).WriteLine(message));

        if (launchSettingsPath is null)
        {
            return LaunchProfileParseResult.Success(model: null);
        }

        if (!RunCommandVerbosity.IsQuiet())
        {
            Reporter.Output.WriteLine(string.Format(CliCommandStrings.UsingLaunchSettingsFromMessage, launchSettingsPath));
        }

        return LaunchSettings.ReadProfileSettingsFromFile(launchSettingsPath, LaunchProfile);
    }

    private void EnsureProjectIsBuilt(out Func<ProjectCollection, ProjectInstance>? projectFactory, out RunProperties? cachedRunProperties, out VirtualProjectBuildingCommand? projectBuilder)
    {
        int buildResult;
        if (EntryPointFileFullPath is not null)
        {
            projectBuilder = CreateProjectBuilder();
            buildResult = projectBuilder.Execute();
            projectFactory = CanUseRunPropertiesForCscBuiltProgram(projectBuilder.LastBuild.Level, projectBuilder.LastBuild.Cache?.PreviousEntry) ? null : projectBuilder.CreateProjectInstance;
            cachedRunProperties = projectBuilder.LastBuild.Cache?.CurrentEntry.Run;
        }
        else
        {
            Debug.Assert(ProjectFileFullPath is not null);

            projectFactory = null;
            cachedRunProperties = null;
            projectBuilder = null;
            buildResult = new RestoringCommand(
                MSBuildArgs.CloneWithExplicitArgs([ProjectFileFullPath, .. MSBuildArgs.OtherMSBuildArgs]),
                NoRestore || _restoreDoneForDeviceSelection,
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

    private VirtualProjectBuildingCommand CreateProjectBuilder()
    {
        Debug.Assert(EntryPointFileFullPath != null);

        var args = MSBuildArgs.RequestedTargets is null or []
            ? MSBuildArgs.CloneWithAdditionalTargets(Constants.Build, Constants.ComputeRunArguments, Constants.CoreCompile)
            : MSBuildArgs.CloneWithAdditionalTargets(Constants.ComputeRunArguments, Constants.CoreCompile);

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
    private MSBuildArgs SetupSilentBuildArgs(MSBuildArgs msbuildArgs)
    {
        msbuildArgs = msbuildArgs.CloneWithNoLogo(true);

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

    private ICommand GetTargetCommandForProject(ProjectLaunchProfile? launchSettings, Func<ProjectCollection, ProjectInstance>? projectFactory, RunProperties? cachedRunProperties, FacadeLogger? logger)
    {
        ICommand command;
        if (cachedRunProperties != null)
        {
            // We can skip project evaluation if we already evaluated the project during virtual build
            // or we have cached run properties in previous run (and this is a --no-build or skip-msbuild run).
            Reporter.Verbose.WriteLine("Getting target command: from cache.");
            command = CreateCommandFromRunProperties(cachedRunProperties.WithApplicationArguments(ApplicationArgs));
        }
        else if (projectFactory is null && ProjectFileFullPath is null)
        {
            // If we are running a file-based app and projectFactory is null, it means csc was used instead of full msbuild.
            // So we can skip project evaluation to continue the optimized path.
            Debug.Assert(EntryPointFileFullPath is not null);
            Reporter.Verbose.WriteLine("Getting target command: for csc-built program.");
            command = CreateCommandForCscBuiltProgram(EntryPointFileFullPath, ApplicationArgs);
        }
        else
        {
            Reporter.Verbose.WriteLine("Getting target command: evaluating project.");
    
            ProjectInstance project;
            try
            {
                project = EvaluateProject(ProjectFileFullPath, projectFactory, MSBuildArgs, logger);
                ValidatePreconditions(project);
                InvokeRunArgumentsTarget(project, NoBuild, logger, MSBuildArgs);
            }
            finally
            {
                    }

            var runProperties = RunProperties.FromProject(project).WithApplicationArguments(ApplicationArgs);
            command = CreateCommandFromRunProperties(runProperties);
        }

        SetEnvironmentVariables(command, launchSettings);

        if (!NoLaunchProfileArguments && string.IsNullOrEmpty(command.CommandArgs) && launchSettings?.CommandLineArgs != null)
        {
            command.SetCommandArgs(launchSettings.CommandLineArgs);
        }

        return command;

        static ProjectInstance EvaluateProject(string? projectFilePath, Func<ProjectCollection, ProjectInstance>? projectFactory, MSBuildArgs msbuildArgs, ILogger? binaryLogger)
        {
            var globalProperties = CommonRunHelpers.GetGlobalPropertiesFromArgs(msbuildArgs);
            var collection = new ProjectCollection(globalProperties: globalProperties, loggers: binaryLogger is null ? null : [binaryLogger], toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);

            if (projectFactory != null)
            {
                return projectFactory(collection);
            }

            try
            {
                return collection.LoadProject(projectFilePath).CreateProjectInstance();
            }
            catch (InvalidProjectFileException e)
            {
                throw new GracefulException(string.Format(CliCommandStrings.RunCommandSpecifiedFileIsNotAValidProject, projectFilePath), e);
            }
        }

        static void ValidatePreconditions(ProjectInstance project)
        {
            // there must be some kind of TFM available to run a project
            if (string.IsNullOrWhiteSpace(project.GetPropertyValue("TargetFramework")) && string.IsNullOrEmpty(project.GetPropertyValue("TargetFrameworks")))
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

        static ICommand CreateCommandForCscBuiltProgram(string entryPointFileFullPath, string[] args)
        {
            var artifactsPath = VirtualProjectBuilder.GetArtifactsPath(entryPointFileFullPath);
            var exePath = Path.Join(artifactsPath, "bin", "debug", Path.GetFileNameWithoutExtension(entryPointFileFullPath) + FileNameSuffixes.CurrentPlatform.Exe);
            var commandSpec = new CommandSpec(path: exePath, args: ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(args));
            var command = CommandFactoryUsingResolver.Create(commandSpec);

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
                CommonRunHelpers.GetConsoleLogger(
                    buildArgs.CloneWithExplicitArgs([$"--verbosity:{LoggerVerbosity.Quiet.ToString().ToLowerInvariant()}", ..buildArgs.OtherMSBuildArgs])
                )
            ];
            if (binaryLogger is not null)
            {
                loggersForBuild.Add(binaryLogger);
            }

            if (!project.Build([Constants.ComputeRunArguments], loggers: loggersForBuild, remoteLoggers: null, out _))
            {
                throw new GracefulException(CliCommandStrings.RunCommandEvaluationExceptionBuildFailed, Constants.ComputeRunArguments);
            }
        }
    }

    [DoesNotReturn]
    internal static void ThrowUnableToRunError(ProjectInstance project)
    {
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

        // Normalize path separators to handle Windows-style paths on non-Windows platforms.
        // This is supported for backward compatibility in 'dotnet run' only, not for all CLI commands.
        // Converting backslashes to forward slashes allows PowerShell scripts using Windows-style paths
        // to work cross-platform, maintaining compatibility with .NET 9 behavior.
        if (Path.DirectorySeparatorChar != '\\')
        {
            projectFileOrDirectoryPath = projectFileOrDirectoryPath.Replace('\\', '/');
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
                if (VirtualProjectBuilder.IsValidEntryPointPath(arg))
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
        var definition = (RunCommandDefinition)parseResult.CommandResult.Command;

        if (UsingRunCommandShorthandProjectOption(parseResult))
        {
            Reporter.Output.WriteLine(CliCommandStrings.RunCommandProjectAbbreviationDeprecated.Yellow());
            parseResult = ModifyParseResultForShorthandProjectOption(parseResult);
        }

        // if the application arguments contain any binlog args then we need to remove them from the application arguments and apply
        // them to the restore args.
        // this is because we can't model the binlog command structure in MSbuild in the System.CommandLine parser, but we need
        // bl information to synchronize the restore and build logger configurations
        var applicationArguments = parseResult.GetValue(definition.ApplicationArguments)?.ToList();

        LoggerUtility.SeparateBinLogArguments(applicationArguments, out var binLogArgs, out var nonBinLogArgs);

        var msbuildProperties = parseResult.OptionValuesToBeForwarded(definition).ToList();
        if (binLogArgs.Count > 0)
        {
            msbuildProperties.AddRange(binLogArgs);
        }

        // Only consider `-` to mean "read code from stdin" if it is before double dash `--`
        // (otherwise it should be forwarded to the target application as its command-line argument).
        bool readCodeFromStdin = nonBinLogArgs is ["-", ..] &&
            parseResult.Tokens.TakeWhile(static t => t.Type != TokenType.DoubleDash)
                .Any(static t => t is { Type: TokenType.Argument, Value: "-" });

        string? projectOption = parseResult.GetValue(definition.ProjectOption);
        string? fileOption = parseResult.GetValue(definition.FileOption);

        if (projectOption != null && fileOption != null)
        {
            throw new GracefulException(CliCommandStrings.CannotCombineOptions, definition.ProjectOption.Name, definition.FileOption.Name);
        }

        string[] args = [.. nonBinLogArgs];
        string? projectFilePath = DiscoverProjectFilePath(
            filePath: fileOption,
            projectFileOrDirectoryPath: projectOption,
            readCodeFromStdin: readCodeFromStdin,
            ref args,
            out string? entryPointFilePath);

        bool noBuild = parseResult.HasOption(definition.NoBuildOption);
        string launchProfile = parseResult.GetValue(definition.LaunchProfileOption) ?? string.Empty;

        if (readCodeFromStdin && entryPointFilePath != null)
        {
            Debug.Assert(projectFilePath is null && entryPointFilePath is "-");

            if (noBuild)
            {
                throw new GracefulException(CliCommandStrings.InvalidOptionForStdin, definition.NoBuildOption.Name);
            }

            if (!string.IsNullOrWhiteSpace(launchProfile))
            {
                throw new GracefulException(CliCommandStrings.InvalidOptionForStdin, definition.LaunchProfileOption.Name);
            }

            // If '-' is specified as the input file, read all text from stdin into a temporary file and use that as the entry point.
            // We create a new directory for each file so other files are not included in the compilation.
            // We fail if the file already exists to avoid reusing the same file for multiple stdin runs (in case the random name is duplicate).
            string directory = VirtualProjectBuilder.GetTempSubpath(Path.GetRandomFileName());
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

        var msbuildArgs = MSBuildArgs.AnalyzeMSBuildArguments(
            msbuildProperties,
            CommonOptions.CreatePropertyOption(),
            CommonOptions.CreateRestorePropertyOption(),
            CommonOptions.CreateMSBuildTargetOption(),
            definition.VerbosityOption);

        var command = new RunCommand(
            noBuild: noBuild,
            projectFileFullPath: projectFilePath,
            entryPointFileFullPath: entryPointFilePath,
            launchProfile: launchProfile,
            noLaunchProfile: parseResult.HasOption(definition.NoLaunchProfileOption),
            noLaunchProfileArguments: parseResult.HasOption(definition.NoLaunchProfileArgumentsOption),
            device: parseResult.GetValue(definition.DeviceOption),
            listDevices: parseResult.HasOption(definition.ListDevicesOption),
            noRestore: parseResult.HasOption(definition.NoRestoreOption) || parseResult.HasOption(definition.NoBuildOption),
            noCache: parseResult.HasOption(definition.NoCacheOption),
            interactive: parseResult.GetValue(definition.InteractiveOption),
            msbuildArgs: msbuildArgs,
            applicationArgs: args,
            readCodeFromStdin: readCodeFromStdin,
            environmentVariables: parseResult.GetValue(definition.EnvOption) ?? ImmutableDictionary<string, string>.Empty
        );

        return command;

        bool UsingRunCommandShorthandProjectOption(ParseResult parseResult)
        {
            if (parseResult.HasOption(definition.PropertyOption) && parseResult.GetValue(definition.PropertyOption)!.Any())
            {
                var projVals = parseResult.GetRunCommandShorthandProjectValues();
                if (projVals?.Any() is true)
                {
                    if (projVals.Count() != 1 || parseResult.HasOption(definition.ProjectOption))
                    {
                        throw new GracefulException(CliStrings.OnlyOneProjectAllowed);
                    }
                    return true;
                }
            }
            return false;
        }
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

    /// <summary>
    /// Sends telemetry about the run operation.
    /// </summary>
    private void SendRunTelemetry(
        LaunchProfile? launchSettings,
        VirtualProjectBuildingCommand? projectBuilder)
    {
        try
        {
            if (projectBuilder != null)
            {
                SendFileBasedTelemetry(launchSettings, projectBuilder);
            }
            else
            {
                SendProjectBasedTelemetry(launchSettings);
            }
        }
        catch (Exception ex)
        {
            // Silently ignore telemetry errors to not affect the run operation
            if (CommandLoggingContext.IsVerbose)
            {
                Reporter.Verbose.WriteLine($"Failed to send run telemetry: {ex}");
            }
        }
    }

    /// <summary>
    /// Builds and sends telemetry data for file-based app runs.
    /// </summary>
    private void SendFileBasedTelemetry(
        LaunchProfile? launchSettings,
        VirtualProjectBuildingCommand projectBuilder)
    {
        Debug.Assert(EntryPointFileFullPath != null);
        var projectIdentifier = RunTelemetry.GetFileBasedIdentifier(EntryPointFileFullPath, Sha256Hasher.Hash);

        var directives = projectBuilder.Directives;
        var sdkCount = RunTelemetry.CountSdks(directives);
        var packageReferenceCount = RunTelemetry.CountPackageReferences(directives);
        var projectReferenceCount = RunTelemetry.CountProjectReferences(directives);
        var additionalPropertiesCount = RunTelemetry.CountAdditionalProperties(directives);

        RunTelemetry.TrackRunEvent(
            isFileBased: true,
            projectIdentifier: projectIdentifier,
            launchProfile: LaunchProfile,
            noLaunchProfile: NoLaunchProfile,
            launchSettings: launchSettings,
            sdkCount: sdkCount,
            packageReferenceCount: packageReferenceCount,
            projectReferenceCount: projectReferenceCount,
            additionalPropertiesCount: additionalPropertiesCount,
            usedMSBuild: projectBuilder.LastBuild.Level is BuildLevel.All,
            usedRoslynCompiler: projectBuilder.LastBuild.Level is BuildLevel.Csc);
    }

    /// <summary>
    /// Builds and sends telemetry data for project-based app runs.
    /// </summary>
    private void SendProjectBasedTelemetry(LaunchProfile? launchSettings)
    {
        Debug.Assert(ProjectFileFullPath != null);
        var projectIdentifier = RunTelemetry.GetProjectBasedIdentifier(ProjectFileFullPath, GetRepositoryRoot(), Sha256Hasher.Hash);

        // Get package and project reference counts for project-based apps
        int packageReferenceCount = 0;
        int projectReferenceCount = 0;

        // Try to get project information for telemetry if we built the project
        if (ShouldBuild)
        {
            try
            {
                var globalProperties = MSBuildArgs.GlobalProperties?.ToDictionary() ?? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                globalProperties[Constants.EnableDefaultItems] = "false";
                globalProperties[Constants.MSBuildExtensionsPath] = AppContext.BaseDirectory;

                using var collection = new ProjectCollection(globalProperties: globalProperties);
                var project = collection.LoadProject(ProjectFileFullPath).CreateProjectInstance();

                packageReferenceCount = RunTelemetry.CountPackageReferences(project);
                projectReferenceCount = RunTelemetry.CountProjectReferences(project);
            }
            catch
            {
                // If project evaluation fails for telemetry, use defaults
                // We don't want telemetry collection to affect the run operation
            }
        }

        RunTelemetry.TrackRunEvent(
            isFileBased: false,
            projectIdentifier: projectIdentifier,
            launchProfile: LaunchProfile,
            noLaunchProfile: NoLaunchProfile,
            launchSettings: launchSettings,
            packageReferenceCount: packageReferenceCount,
            projectReferenceCount: projectReferenceCount);
    }

    /// <summary>
    /// Attempts to find the repository root directory.
    /// </summary>
    /// <returns>Repository root path if found, null otherwise</returns>
    private string? GetRepositoryRoot()
    {
        try
        {
            var currentDir = ProjectFileFullPath != null
                ? Path.GetDirectoryName(ProjectFileFullPath)
                : Directory.GetCurrentDirectory();

            while (currentDir != null)
            {
                if (Directory.Exists(Path.Combine(currentDir, ".git")))
                {
                    return currentDir;
                }
                currentDir = Directory.GetParent(currentDir)?.FullName;
            }
        }
        catch
        {
            // Ignore errors when trying to find repo root
        }

        return null;
    }
}
