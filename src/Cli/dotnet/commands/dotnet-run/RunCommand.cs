// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System.Diagnostics;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
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
            }.CreateProjectInstance;
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
        List<string> args = new()
        {
            "-nologo"
        };

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
        FacadeLogger? logger = DetermineBinlogger(RestoreArgs);
        var project = EvaluateProject(ProjectFileFullPath, projectFactory, RestoreArgs, logger);
        ValidatePreconditions(project);
        InvokeRunArgumentsTarget(project, RestoreArgs, Verbosity, logger);
        logger?.ReallyShutdown();
        var runProperties = ReadRunPropertiesFromProject(project, Args);
        var command = CreateCommandFromRunProperties(project, runProperties);
        return command;

        static ProjectInstance EvaluateProject(string? projectFilePath, Func<ProjectCollection, ProjectInstance>? projectFactory, string[]? args, ILogger? binaryLogger)
        {
            Debug.Assert(projectFilePath is not null || projectFactory is not null);

            var collection = new ProjectCollection(globalProperties: CommonRunHelpers.GetGlobalPropertiesFromArgs(args), loggers: binaryLogger is null ? null : [binaryLogger], toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);

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

        static FacadeLogger? DetermineBinlogger(string[] restoreArgs)
        {
            List<BinaryLogger> binaryLoggers = new();

            for (int i = restoreArgs.Length - 1; i >= 0; i--)
            {
                string blArg = restoreArgs[i];
                if (!IsBinLogArgument(blArg))
                {
                    continue;
                }

                if (blArg.Contains(':'))
                {
                    // split and forward args
                    var split = blArg.Split(':', 2);
                    var filename = split[1];
                    if (filename.EndsWith(".binlog"))
                    {
                        filename = filename.Substring(0, filename.Length - ".binlog".Length);
                        filename = filename + "-dotnet-run" + ".binlog";
                    }
                    binaryLoggers.Add(new BinaryLogger { Parameters = filename });
                }
                else
                {
                    // the same name will be used for the build and run-restore-exec steps, so we need to make sure they don't conflict
                    var filename = "msbuild-dotnet-run" + ".binlog";
                    binaryLoggers.Add(new BinaryLogger { Parameters = filename });
                }

                // Like in MSBuild, only the last binary logger is used.
                break;
            }

            // this binaryLogger needs to be used for both evaluation and execution, so we need to only call it with a single IEventSource across
            // both of those phases.
            // We need a custom logger to handle this, because the MSBuild API for evaluation and execution calls logger Initialize and Shutdown methods, so will not allow us to do this.
            if (binaryLoggers.Count > 0)
            {
                var fakeLogger = ConfigureDispatcher(binaryLoggers);

                return fakeLogger;
            }
            return null;
        }

        static FacadeLogger ConfigureDispatcher(List<BinaryLogger> binaryLoggers)
        {
            var dispatcher = new PersistentDispatcher(binaryLoggers);
            return new FacadeLogger(dispatcher);
        }
    }

    /// <summary>
    /// This class acts as a wrapper around the BinaryLogger, to allow us to keep the BinaryLogger alive across multiple phases of the build.
    /// The methods here are stubs so that the real binarylogger sees that we support these functionalities.
    /// We need to ensure that the child logger is Initialized and Shutdown only once, so this fake event source
    /// acts as a buffer. We'll provide this dispatcher to another fake logger, and that logger will
    /// bind to this dispatcher to foward events from the actual build to the binary logger through this dispatcher.
    /// </summary>
    /// <param name="innerLogger"></param>
    private class PersistentDispatcher : EventArgsDispatcher, IEventSource4
    {
        private List<BinaryLogger> innerLoggers;

        public PersistentDispatcher(List<BinaryLogger> innerLoggers)
        {
            this.innerLoggers = innerLoggers;
            foreach (var logger in innerLoggers)
            {
                logger.Initialize(this);
            }
        }
        public event TelemetryEventHandler TelemetryLogged { add { } remove { } }

        public void IncludeEvaluationMetaprojects() { }
        public void IncludeEvaluationProfiles() { }
        public void IncludeEvaluationPropertiesAndItems() { }
        public void IncludeTaskInputs() { }

        public void Destroy()
        {
            foreach (var innerLogger in innerLoggers)
            {
                innerLogger.Shutdown();
            }
        }
    }

    /// <summary>
    /// This logger acts as a forwarder to the provided dispatcher, so that multiple different build engine operations
    /// can be forwarded to the shared binary logger held by the dispatcher.
    /// We opt into lots of data to ensure that we can forward all events to the binary logger.
    /// </summary>
    /// <param name="dispatcher"></param>
    private class FacadeLogger(PersistentDispatcher dispatcher) : ILogger
    {
        public PersistentDispatcher Dispatcher => dispatcher;

        public LoggerVerbosity Verbosity { get => LoggerVerbosity.Diagnostic; set { } }
        public string? Parameters { get => ""; set { } }

        public void Initialize(IEventSource eventSource)
        {
            if (eventSource is IEventSource3 eventSource3)
            {
                eventSource3.IncludeEvaluationMetaprojects();
                dispatcher.IncludeEvaluationMetaprojects();

                eventSource3.IncludeEvaluationProfiles();
                dispatcher.IncludeEvaluationProfiles();

                eventSource3.IncludeTaskInputs();
                dispatcher.IncludeTaskInputs();
            }

            eventSource.AnyEventRaised += (sender, args) => dispatcher.Dispatch(args);

            if (eventSource is IEventSource4 eventSource4)
            {
                eventSource4.IncludeEvaluationPropertiesAndItems();
                dispatcher.IncludeEvaluationPropertiesAndItems();
            }
        }

        public void ReallyShutdown()
        {
            dispatcher.Destroy();
        }

        // we don't do anything on shutdown, because we want to keep the dispatcher alive for the next phase
        public void Shutdown()
        {
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
                !arg.EndsWith(".cs", StringComparison.OrdinalIgnoreCase) ||
                !File.Exists(arg))
            {
                return null;
            }

            if (!HasTopLevelStatements(arg))
            {
                throw new GracefulException(LocalizableStrings.NoTopLevelStatements, arg);
            }

            args = args[1..];
            return Path.GetFullPath(arg);
        }

        static bool HasTopLevelStatements(string entryPointFilePath)
        {
            var tree = ParseCSharp(entryPointFilePath);
            return tree.GetRoot().ChildNodes().OfType<GlobalStatementSyntax>().Any();
        }

        static CSharpSyntaxTree ParseCSharp(string filePath)
        {
            using var stream = File.OpenRead(filePath);
            return (CSharpSyntaxTree)CSharpSyntaxTree.ParseText(SourceText.From(stream, Encoding.UTF8), path: filePath);
        }
    }
}
