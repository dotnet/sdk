// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.Build.Exceptions;
using Microsoft.Build.Execution;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.CommandFactory;
using Microsoft.DotNet.Tools.Run.LaunchSettings;

namespace Microsoft.DotNet.Tools.Run
{
    public partial class RunCommand
    {
        public string Configuration { get; private set; }
        public string Framework { get; private set; }
        public string Runtime { get; private set; }
        public bool NoBuild { get; private set; }
        public string Project { get; private set; }
        public IEnumerable<string> Args { get; set; }
        public bool NoRestore { get; private set; }
        public bool Interactive { get; private set; }
        public IEnumerable<string> RestoreArgs { get; private set; }

        private bool ShouldBuild => !NoBuild;
        private bool HasQuietVerbosity =>
            RestoreArgs.All(arg => !arg.StartsWith("-verbosity:", StringComparison.Ordinal) ||
                                    arg.Equals("-verbosity:q", StringComparison.Ordinal) ||
                                    arg.Equals("-verbosity:quiet", StringComparison.Ordinal));

        public string LaunchProfile { get; private set; }
        public bool NoLaunchProfile { get; private set; }
        private bool UseLaunchProfile => !NoLaunchProfile;

        public int Execute()
        {
            Initialize();

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
                    string.Format(LocalizableStrings.RunCommandSpecifiecFileIsNotAValidProject, Project),
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

        public RunCommand(string configuration,
            string framework,
            string runtime,
            bool noBuild,
            string project,
            string launchProfile,
            bool noLaunchProfile,
            bool noRestore,
            bool interactive,
            IEnumerable<string> restoreArgs,
            IEnumerable<string> args)
        {
            Configuration = configuration;
            Framework = framework;
            Runtime = runtime;
            NoBuild = noBuild;
            Project = project;
            LaunchProfile = launchProfile;
            NoLaunchProfile = noLaunchProfile;
            Args = args;
            RestoreArgs = restoreArgs;
            NoRestore = noRestore;
            Interactive = interactive;
        }

        private bool TryGetLaunchProfileSettingsIfNeeded(out ProjectLaunchSettingsModel? launchSettingsModel)
        {
            launchSettingsModel = default;
            if (!UseLaunchProfile)
            {
                return true;
            }

            var launchSettingsPath = TryFindLaunchSettings(Project);
            if (!File.Exists(launchSettingsPath))
            {
                if (!string.IsNullOrEmpty(LaunchProfile))
                {
                    Reporter.Error.WriteLine(string.Format(LocalizableStrings.RunCommandExceptionCouldNotLocateALaunchSettingsFile, launchSettingsPath).Bold().Red());
                }
                return true;
            }

            if (!HasQuietVerbosity)
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
            var restoreArgs = GetRestoreArguments();

            var buildResult =
                new RestoringCommand(
                    restoreArgs.Prepend(Project),
                    NoRestore,
                    advertiseWorkloadUpdates: false
                ).Execute();

            if (buildResult != 0)
            {
                Reporter.Error.WriteLine();
                throw new GracefulException(LocalizableStrings.RunCommandException);
            }
        }

        private List<string> GetRestoreArguments()
        {
            List<string> args = new()
            {
                "-nologo"
            };

            // --interactive need to output guide for auth. It cannot be
            // completely "quiet"
            if (!RestoreArgs.Any(a => a.StartsWith("-verbosity:")))
            {
                var defaultVerbosity = Interactive ? "minimal" : "quiet";
                args.Add($"-verbosity:{defaultVerbosity}");
            }

            args.AddRange(RestoreArgs);

            return args;
        }

        private record RunProperties(string? RunCommand, string? RunArguments, string? RunWorkingDirectory);

        private ICommand GetTargetCommand()
        {
            // TODO for MSBuild usage here: need to sync properties passed to MSBuild during the build with this evaluation
            // TODO for MSBuild usage here: need to sync loggers (primarily binlogger) used with this evaluation
            var project = EvaluateProject();
            InvokeRunArgumentsTarget(project);
            var runProperties = ReadRunPropertiesFromProject(project);
            var command = CreateCommandFromRunProperties(project, runProperties);
            return command;

            ProjectInstance EvaluateProject()
            {
                var globalProperties = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                {
                    // This property disables default item globbing to improve performance
                    // This should be safe because we are not evaluating items, only properties
                    { Constants.EnableDefaultItems,    "false" },
                    { Constants.MSBuildExtensionsPath, AppContext.BaseDirectory }
                };

                if (!string.IsNullOrWhiteSpace(Configuration))
                {
                    globalProperties.Add("Configuration", Configuration);
                }

                if (!string.IsNullOrWhiteSpace(Framework))
                {
                    globalProperties.Add("TargetFramework", Framework);
                }

                if (!string.IsNullOrWhiteSpace(Runtime))
                {
                    globalProperties.Add("RuntimeIdentifier", Runtime);
                }

                var project = new ProjectInstance(Project, globalProperties, null);
                return project;
            }

            RunProperties ReadRunPropertiesFromProject(ProjectInstance project)
            {
                string runProgram = project.GetPropertyValue("RunCommand");
                if (string.IsNullOrEmpty(runProgram))
                {
                    ThrowUnableToRunError(project);
                }

                string runArguments = project.GetPropertyValue("RunArguments");
                string runWorkingDirectory = project.GetPropertyValue("RunWorkingDirectory");

                if (Args.Any())
                {
                    runArguments += " " + ArgumentEscaper.EscapeAndConcatenateArgArrayForProcessStart(Args);
                }
                return new(runProgram, runArguments, runWorkingDirectory);
            }

            ICommand CreateCommandFromRunProperties(ProjectInstance project, RunProperties runProperties)
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

            void InvokeRunArgumentsTarget(ProjectInstance project)
            {
                if (project.Build(["ComputeRunArguments"], loggers: null, remoteLoggers: null, out var targetOutputs))
                {

                }
                else
                {
                    throw new GracefulException("boom");
                }
            }
        }

        private void ThrowUnableToRunError(ProjectInstance project)
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

        private void Initialize()
        {
            if (string.IsNullOrWhiteSpace(Project))
            {
                Project = Directory.GetCurrentDirectory();
            }

            if (Directory.Exists(Project))
            {
                Project = FindSingleProjectInDirectory(Project);
            }
        }

        private static string FindSingleProjectInDirectory(string directory)
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
