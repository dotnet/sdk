// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Build.Evaluation;
using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.CommandFactory;
using Microsoft.DotNet.Tools.Run.LaunchSettings;

namespace Microsoft.DotNet.Tools.Run
{
    public partial class RunCommand
    {
        private record RunProperties(string? RunCommand, string? RunArguments, string? RunWorkingDirectory);

        public bool NoBuild { get; private set; }
        public string ProjectFileFullPath { get; private set; }
        public string[] Args { get; set; }
        public bool NoRestore { get; private set; }
        public VerbosityOptions? Verbosity { get; }
        public bool Interactive { get; private set; }
        public string[] RestoreArgs { get; private set; }

        private bool ShouldBuild => !NoBuild;

        public string LaunchProfile { get; private set; }
        public bool NoLaunchProfile { get; private set; }
        private bool UseLaunchProfile => !NoLaunchProfile;

        public RunCommand(
            bool noBuild,
            string? projectFileOrDirectory,
            string launchProfile,
            bool noLaunchProfile,
            bool noRestore,
            bool interactive,
            VerbosityOptions? verbosity,
            string[] restoreArgs,
            string[] args)
        {
            NoBuild = noBuild;
            ProjectFileFullPath = DiscoverProjectFilePath(projectFileOrDirectory);
            LaunchProfile = launchProfile;
            NoLaunchProfile = noLaunchProfile;
            Args = args;
            Interactive = interactive;
            NoRestore = noRestore;
            Verbosity = verbosity;
            RestoreArgs = GetRestoreArguments(restoreArgs);
        }

        public int Execute()
        {
            if (!TryGetLaunchProfileSettingsIfNeeded(out var launchSettings))
            {
                return 1;
            }

            if (ShouldBuild)
            {
                if (string.Equals("true", launchSettings?.DotNetRunMessages, StringComparison.OrdinalIgnoreCase))
                {
                    Reporter.Output.WriteLine(LocalizableStrings.RunCommandBuilding);
                }

                EnsureProjectIsBuilt();
            }

            try
            {
                ICommand targetCommand = GetTargetCommand();
                var launchSettingsCommand = ApplyLaunchSettingsProfileToCommand(targetCommand, launchSettings);
                // Ignore Ctrl-C for the remainder of the command's execution
                Console.CancelKeyPress += (sender, e) => { e.Cancel = true; };
                return launchSettingsCommand.Execute().ExitCode;
            }
            catch (InvalidProjectFileException e)
            {
                throw new GracefulException(
                    string.Format(LocalizableStrings.RunCommandSpecifiedFileIsNotAValidProject, ProjectFileFullPath),
                    e);
            }
        }

        private ICommand ApplyLaunchSettingsProfileToCommand(ICommand targetCommand, ProjectLaunchSettingsModel? launchSettings)
        {
            if (launchSettings != null)
            {
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
                if (string.IsNullOrEmpty(targetCommand.CommandArgs) && launchSettings.CommandLineArgs != null)
                {
                    targetCommand.SetCommandArgs(launchSettings.CommandLineArgs);
                }
            }
            return targetCommand;
        }

        private bool TryGetLaunchProfileSettingsIfNeeded(out ProjectLaunchSettingsModel? launchSettingsModel)
        {
            launchSettingsModel = default;
            if (!UseLaunchProfile)
            {
                return true;
            }

            var launchSettingsPath = TryFindLaunchSettings(ProjectFileFullPath);
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

            static string? TryFindLaunchSettings(string projectFilePath)
            {
                var buildPathContainer = File.Exists(projectFilePath) ? Path.GetDirectoryName(projectFilePath) : projectFilePath;
                if (buildPathContainer is null)
                {
                    return null;
                }

                string propsDirectory;

                // VB.NET projects store the launch settings file in the
                // "My Project" directory instead of a "Properties" directory.
                // TODO: use the `AppDesignerFolder` MSBuild property instead, which captures this logic already
                if (string.Equals(Path.GetExtension(projectFilePath), ".vbproj", StringComparison.OrdinalIgnoreCase))
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

        private void EnsureProjectIsBuilt()
        {
            var buildResult =
                new RestoringCommand(
                    RestoreArgs.Prepend(ProjectFileFullPath),
                    NoRestore,
                    advertiseWorkloadUpdates: false
                ).Execute();

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

            // --interactive need to output guide for auth. It cannot be
            // completely "quiet"
            if (Verbosity is null)
            {
                var defaultVerbosity = Interactive ? "minimal" : "quiet";
                args.Add($"-verbosity:{defaultVerbosity}");
            }

            args.AddRange(cliRestoreArgs);

            return args.ToArray();
        }

        private ICommand GetTargetCommand()
        {
            FacadeLogger? logger = DetermineBinlogger(RestoreArgs);
            var project = EvaluateProject(ProjectFileFullPath, RestoreArgs, logger);
            ValidatePreconditions(project);
            InvokeRunArgumentsTarget(project, RestoreArgs, Verbosity, logger);
            logger?.ReallyShutdown();
            var runProperties = ReadRunPropertiesFromProject(project, Args);
            var command = CreateCommandFromRunProperties(project, runProperties);
            return command;

            static ProjectInstance EvaluateProject(string projectFilePath, string[] restoreArgs, ILogger? binaryLogger)
            {
                var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    // This property disables default item globbing to improve performance
                    // This should be safe because we are not evaluating items, only properties
                    { Constants.EnableDefaultItems,  "false" },
                    { Constants.MSBuildExtensionsPath, AppContext.BaseDirectory }
                };

                var userPassedProperties = DeriveUserPassedProperties(restoreArgs);
                if (userPassedProperties is not null)
                {
                    foreach (var (key, values) in userPassedProperties)
                    {
                        globalProperties[key] = string.Join(";", values);
                    }
                }
                var collection = new ProjectCollection(globalProperties: globalProperties, loggers: binaryLogger is null ? null : [binaryLogger], toolsetDefinitionLocations: ToolsetDefinitionLocations.Default);
                return collection.LoadProject(projectFilePath).CreateProjectInstance();
            }

            static void ValidatePreconditions(ProjectInstance project)
            {
                if (string.IsNullOrWhiteSpace(project.GetPropertyValue("TargetFramework")))
                {
                    ThrowUnableToRunError(project);
                }
            }

            static Dictionary<string, List<string>>? DeriveUserPassedProperties(string[] args)
            {
                var fakeCommand = new System.CommandLine.CliCommand("dotnet") { CommonOptions.PropertiesOption };
                var propertyParsingConfiguration = new System.CommandLine.CliConfiguration(fakeCommand);
                var propertyParseResult = propertyParsingConfiguration.Parse(args);
                var propertyValues = propertyParseResult.GetValue(CommonOptions.PropertiesOption);

                if (propertyValues != null)
                {
                    var userPassedProperties = new Dictionary<string, List<string>>(propertyValues.Length, StringComparer.OrdinalIgnoreCase);
                    foreach (var property in propertyValues)
                    {
                        foreach (var (key, value) in MSBuildPropertyParser.ParseProperties(property))
                        {
                            if (userPassedProperties.TryGetValue(key, out var existingValues))
                            {
                                existingValues.Add(value);
                            }
                            else
                            {
                                userPassedProperties[key] = [value];
                            }
                        }
                    }
                    return userPassedProperties;
                }
                return null;
            }

            static RunProperties ReadRunPropertiesFromProject(ProjectInstance project, string[] applicationArgs)
            {
                string runProgram = project.GetPropertyValue("RunCommand");
                if (string.IsNullOrEmpty(runProgram))
                {
                    ThrowUnableToRunError(project);
                }

                string runArguments = project.GetPropertyValue("RunArguments");
                string runWorkingDirectory = project.GetPropertyValue("RunWorkingDirectory");

                if (applicationArgs.Any())
                {
                    runArguments += " " + ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(applicationArgs);
                }
                return new(runProgram, runArguments, runWorkingDirectory);
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

                foreach (var blArg in restoreArgs.Where(arg => arg.StartsWith("-bl", StringComparison.OrdinalIgnoreCase)))
                {
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
            var thing = Assembly.Load("MSBuild").GetType("Microsoft.Build.Logging.TerminalLogger.TerminalLogger")!.GetConstructor([typeof(LoggerVerbosity)])!.Invoke([msbuildVerbosity]) as ILogger;
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

        private string DiscoverProjectFilePath(string? projectFileOrDirectoryPath)
        {
            if (string.IsNullOrWhiteSpace(projectFileOrDirectoryPath))
            {
                projectFileOrDirectoryPath = Directory.GetCurrentDirectory();
            }

            if (Directory.Exists(projectFileOrDirectoryPath))
            {
                projectFileOrDirectoryPath = FindSingleProjectInDirectory(projectFileOrDirectoryPath);
            }
            return projectFileOrDirectoryPath;
        }

        public static string FindSingleProjectInDirectory(string directory)
        {
            string[] projectFiles = Directory.GetFiles(directory, "*.*proj");

            if (projectFiles.Length == 0)
            {
                throw new GracefulException(LocalizableStrings.RunCommandExceptionNoProjects, directory, "--project");
            }
            else if (projectFiles.Length > 1)
            {
                throw new GracefulException(LocalizableStrings.RunCommandExceptionMultipleProjects, directory);
            }

            return projectFiles[0];
        }
    }
}
