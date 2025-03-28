// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.CommandFactory;
using Microsoft.DotNet.Cli.Extensions;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.Tools.Run.LaunchSettings;

namespace Microsoft.DotNet.Tools.Run;

public partial class RunCommand
{
    public bool NoBuild { get; }

    /// <summary>
    /// Value of the <c>--project</c> option.
    /// </summary>
    public string? ProjectFileOrDirectory { get; }

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

    public string[] Args { get; set; }
    public bool NoRestore { get; }
    public VerbosityOptions? Verbosity { get; }
    public bool Interactive { get; }
    public string[] RestoreArgs { get; }

    /// <summary>
    /// Environment variables specified on command line via -e option.
    /// </summary>
    public IReadOnlyDictionary<string, string> EnvironmentVariables { get; }

    private bool ShouldBuild => !NoBuild;

    public string LaunchProfile { get; }
    public bool NoLaunchProfile { get; }

    /// <summary>
    /// True to ignore command line arguments specified by launch profile.
    /// </summary>
    public bool NoLaunchProfileArguments { get; }

    public RunCommand(
        bool noBuild,
        string? projectFileOrDirectory,
        string launchProfile,
        bool noLaunchProfile,
        bool noLaunchProfileArguments,
        bool noRestore,
        bool interactive,
        VerbosityOptions? verbosity,
        string[] restoreArgs,
        string[] args,
        IReadOnlyDictionary<string, string> environmentVariables)
    {
        NoBuild = noBuild;
        ProjectFileOrDirectory = projectFileOrDirectory;
        ProjectFileFullPath = DiscoverProjectFilePath(projectFileOrDirectory, ref args, out string? entryPointFileFullPath);
        EntryPointFileFullPath = entryPointFileFullPath;
        LaunchProfile = launchProfile;
        NoLaunchProfile = noLaunchProfile;
        NoLaunchProfileArguments = noLaunchProfileArguments;
        Args = args;
        Interactive = interactive;
        NoRestore = noRestore;
        Verbosity = verbosity;
        RestoreArgs = GetRestoreArguments(restoreArgs);
        EnvironmentVariables = environmentVariables;
    }

    public int Execute()
    {
        if (!TryGetLaunchProfileSettingsIfNeeded(out var launchSettings))
        {
            return 1;
        }

        Func<ProjectCollection, ProjectInstance>? projectFactory = null;
        if (ShouldBuild)
        {
            if (string.Equals("true", launchSettings?.DotNetRunMessages, StringComparison.OrdinalIgnoreCase))
            {
                Reporter.Output.WriteLine(LocalizableStrings.RunCommandBuilding);
            }

            EnsureProjectIsBuilt(out projectFactory);
        }
        else if (EntryPointFileFullPath is not null)
        {
            projectFactory = new VirtualProjectBuildingCommand
            {
                EntryPointFileFullPath = EntryPointFileFullPath,
            }.PrepareProjectInstance().CreateProjectInstance;
        }

        try
        {
            ICommand targetCommand = GetTargetCommand(projectFactory);
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
                string.Format(LocalizableStrings.RunCommandSpecifiedFileIsNotAValidProject, ProjectFileFullPath),
                e);
        }
    }

    private void ApplyLaunchSettingsProfileToCommand(ICommand targetCommand, ProjectLaunchSettingsModel? launchSettings)
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

    private bool TryGetLaunchProfileSettingsIfNeeded(out ProjectLaunchSettingsModel? launchSettingsModel)
    {
        launchSettingsModel = default;
        if (NoLaunchProfile)
        {
            return true;
        }

        var launchSettingsPath = TryFindLaunchSettings(ProjectFileFullPath ?? EntryPointFileFullPath!);
        if (!File.Exists(launchSettingsPath))
        {
            if (!string.IsNullOrEmpty(LaunchProfile))
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunCommandExceptionCouldNotLocateALaunchSettingsFile, launchSettingsPath).Bold().Red());
            }
            return true;
        }

        if (Verbosity?.IsQuiet() != true)
        {
            Reporter.Output.WriteLine(string.Format(LocalizableStrings.UsingLaunchSettingsFromMessage, launchSettingsPath));
        }

        string profileName = string.IsNullOrEmpty(LaunchProfile) ? LocalizableStrings.DefaultLaunchProfileDisplayName : LaunchProfile;

        try
        {
            var launchSettingsFileContents = File.ReadAllText(launchSettingsPath);
            var applyResult = LaunchSettingsManager.TryApplyLaunchSettings(launchSettingsFileContents, LaunchProfile);
            if (!applyResult.Success)
            {
                Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunCommandExceptionCouldNotApplyLaunchSettings, profileName, applyResult.FailureReason).Bold().Red());
            }
            else
            {
                launchSettingsModel = applyResult.LaunchSettings;
            }
        }
        catch (IOException ex)
        {
            Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunCommandExceptionCouldNotApplyLaunchSettings, profileName).Bold().Red());
            Reporter.Error.WriteLine(ex.Message.Bold().Red());
            return false;
        }

        return true;

        static string? TryFindLaunchSettings(string projectOrEntryPointFilePath)
        {
            var buildPathContainer = File.Exists(projectOrEntryPointFilePath) ? Path.GetDirectoryName(projectOrEntryPointFilePath) : projectOrEntryPointFilePath;
            if (buildPathContainer is null)
            {
                return null;
            }

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

            var launchSettingsPath = Path.Combine(buildPathContainer, propsDirectory, "launchSettings.json");
            return launchSettingsPath;
        }
    }

    private void EnsureProjectIsBuilt(out Func<ProjectCollection, ProjectInstance>? projectFactory)
    {
        int buildResult;
        if (EntryPointFileFullPath is not null)
        {
            var command = new VirtualProjectBuildingCommand
            {
                EntryPointFileFullPath = EntryPointFileFullPath,
            };

            CommonRunHelpers.AddUserPassedProperties(command.GlobalProperties, RestoreArgs);

            projectFactory = command.CreateProjectInstance;
            buildResult = command.Execute(
                binaryLoggerArgs: RestoreArgs,
                consoleLogger: MakeTerminalLogger(Verbosity ?? GetDefaultVerbosity()));
        }
        else
        {
            Debug.Assert(ProjectFileFullPath is not null);

            projectFactory = null;
            buildResult = new RestoringCommand(
                RestoreArgs.Prepend(ProjectFileFullPath),
                NoRestore,
                advertiseWorkloadUpdates: false
            ).Execute();
        }

        if (buildResult != 0)
        {
            Reporter.Error.WriteLine();
            throw new GracefulException(LocalizableStrings.RunCommandException);
        }
    }

    private string[] GetRestoreArguments(IEnumerable<string> cliRestoreArgs)
    {
        List<string> args = ["-nologo"];

        if (Verbosity is null)
        {
            args.Add($"-verbosity:{GetDefaultVerbosity()}");
        }

        args.AddRange(cliRestoreArgs);

        return args.ToArray();
    }

    private VerbosityOptions GetDefaultVerbosity()
    {
        // --interactive need to output guide for auth. It cannot be
        // completely "quiet"
        return Interactive ? VerbosityOptions.minimal : VerbosityOptions.quiet;
    }

    private ICommand GetTargetCommand(Func<ProjectCollection, ProjectInstance>? projectFactory)
    {
        FacadeLogger? logger = LoggerUtility.DetermineBinlogger(RestoreArgs, "dotnet-run");
        var project = EvaluateProject(ProjectFileFullPath, projectFactory, RestoreArgs, logger);
        ValidatePreconditions(project);
        InvokeRunArgumentsTarget(project, RestoreArgs, Verbosity, logger);
        logger?.ReallyShutdown();
        var runProperties = ReadRunPropertiesFromProject(project, Args);
        var command = CreateCommandFromRunProperties(project, runProperties);
        return command;

        static ProjectInstance EvaluateProject(string? projectFilePath, Func<ProjectCollection, ProjectInstance>? projectFactory, string[]? restoreArgs, ILogger? binaryLogger)
        {
            Debug.Assert(projectFilePath is not null || projectFactory is not null);

            var globalProperties = CommonRunHelpers.GetGlobalPropertiesFromArgs(restoreArgs);

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

        static RunProperties ReadRunPropertiesFromProject(ProjectInstance project, string[] applicationArgs)
        {
            var runProperties = RunProperties.FromProjectAndApplicationArguments(project, applicationArgs, fallbackToTargetPath: false);
            if (string.IsNullOrEmpty(runProperties.RunCommand))
            {
                ThrowUnableToRunError(project);
            }

            return runProperties;
        }

        static ICommand CreateCommandFromRunProperties(ProjectInstance project, RunProperties runProperties)
        {
            CommandSpec commandSpec = new(runProperties.RunCommand, runProperties.RunArguments);

            var command = CommandFactoryUsingResolver.Create(commandSpec)
                .WorkingDirectory(runProperties.RunWorkingDirectory);

            var rootVariableName = EnvironmentVariableNames.TryGetDotNetRootVariableName(
                project.GetPropertyValue("RuntimeIdentifier"),
                project.GetPropertyValue("DefaultAppHostRuntimeIdentifier"),
                project.GetPropertyValue("TargetFrameworkVersion"));

            if (rootVariableName != null && Environment.GetEnvironmentVariable(rootVariableName) == null)
            {
                command.EnvironmentVariable(rootVariableName, Path.GetDirectoryName(new Muxer().MuxerPath));
            }
            return command;
        }

        static void InvokeRunArgumentsTarget(ProjectInstance project, string[] restoreArgs, VerbosityOptions? verbosity, FacadeLogger? binaryLogger)
        {
            // if the restoreArgs contain a `-bl` then let's probe it
            List<ILogger> loggersForBuild = [
                MakeTerminalLogger(verbosity)
            ];
            if (binaryLogger is not null)
            {
                loggersForBuild.Add(binaryLogger);
            }

            if (!project.Build([ComputeRunArgumentsTarget], loggers: loggersForBuild, remoteLoggers: null, out var _targetOutputs))
            {
                throw new GracefulException(LocalizableStrings.RunCommandEvaluationExceptionBuildFailed, ComputeRunArgumentsTarget);
            }
        }
    }


    static ILogger MakeTerminalLogger(VerbosityOptions? verbosity)
    {
        var msbuildVerbosity = ToLoggerVerbosity(verbosity);

        // Temporary fix for 9.0.1xx. 9.0.2xx will use the TerminalLogger in the safe way.
        var thing = new ConsoleLogger(msbuildVerbosity);
        return thing!;
    }

    static string ComputeRunArgumentsTarget = "ComputeRunArguments";

    private static LoggerVerbosity ToLoggerVerbosity(VerbosityOptions? verbosity)
    {
        // map all cases of VerbosityOptions enum to the matching LoggerVerbosity enum
        return verbosity switch
        {
            VerbosityOptions.quiet | VerbosityOptions.q => LoggerVerbosity.Quiet,
            VerbosityOptions.minimal | VerbosityOptions.m => LoggerVerbosity.Minimal,
            VerbosityOptions.normal | VerbosityOptions.n => LoggerVerbosity.Normal,
            VerbosityOptions.detailed | VerbosityOptions.d => LoggerVerbosity.Detailed,
            VerbosityOptions.diagnostic | VerbosityOptions.diag => LoggerVerbosity.Diagnostic,
            _ => LoggerVerbosity.Quiet // default to quiet because run should be invisible if possible
        };
    }

    private static void ThrowUnableToRunError(ProjectInstance project)
    {
        string targetFrameworks = project.GetPropertyValue("TargetFrameworks");
        if (!string.IsNullOrEmpty(targetFrameworks))
        {
            string targetFramework = project.GetPropertyValue("TargetFramework");
            if (string.IsNullOrEmpty(targetFramework))
            {
                throw new GracefulException(LocalizableStrings.RunCommandExceptionUnableToRunSpecifyFramework, "--framework");
            }
        }

        throw new GracefulException(
                string.Format(
                    LocalizableStrings.RunCommandExceptionUnableToRun,
                    "dotnet run",
                    "OutputType",
                    project.GetPropertyValue("OutputType")));
    }

    private static string? DiscoverProjectFilePath(string? projectFileOrDirectoryPath, ref string[] args, out string? entryPointFilePath)
    {
        bool emptyProjectOption = false;
        if (string.IsNullOrWhiteSpace(projectFileOrDirectoryPath))
        {
            emptyProjectOption = true;
            projectFileOrDirectoryPath = Directory.GetCurrentDirectory();
        }

        string? projectFilePath = Directory.Exists(projectFileOrDirectoryPath)
            ? TryFindSingleProjectInDirectory(projectFileOrDirectoryPath)
            : projectFileOrDirectoryPath;

        // If no project exists in the directory and no --project was given,
        // try to resolve an entry-point file instead.
        entryPointFilePath = projectFilePath is null && emptyProjectOption
            ? TryFindEntryPointFilePath(ref args)
            : null;

        if (entryPointFilePath is null && projectFilePath is null)
        {
            throw new GracefulException(LocalizableStrings.RunCommandExceptionNoProjects, projectFileOrDirectoryPath, "--project");
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
                throw new GracefulException(LocalizableStrings.RunCommandExceptionMultipleProjects, directory);
            }

            return projectFiles[0];
        }

        static string? TryFindEntryPointFilePath(ref string[] args)
        {
            if (args is not [{ } arg, ..] ||
                !VirtualProjectBuildingCommand.IsValidEntryPointPath(arg))
            {
                return null;
            }

            args = args[1..];
            return Path.GetFullPath(arg);
        }
    }
}
