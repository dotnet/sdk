// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.CommandLine;
using System.CommandLine.Invocation;
using System.CommandLine.Parsing;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Tools.Logging;
using Microsoft.CodeAnalysis.Tools.MSBuild;
using Microsoft.CodeAnalysis.Tools.Utilities;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Microsoft.CodeAnalysis.Tools
{
    internal class Program
    {
        internal const int UnhandledExceptionExitCode = 1;
        internal const int CheckFailedExitCode = 2;
        internal const int UnableToLocateMSBuildExitCode = 3;
        internal const int UnableToLocateDotNetCliExitCode = 4;

        private static readonly string[] s_standardInputKeywords = { "/dev/stdin", "-" };

        private static ParseResult? s_parseResult;

        private static async Task<int> Main(string[] args)
        {
            var rootCommand = FormatCommand.CreateCommandLineOptions();
            rootCommand.Handler = CommandHandler.Create(new FormatCommand.Handler(Run));

            // Parse the incoming args so we can give warnings when deprecated options are used.
            s_parseResult = rootCommand.Parse(args);

            return await rootCommand.InvokeAsync(args);
        }

        public static async Task<int> Run(
            string? workspace,
            bool folder,
            bool fixWhitespace,
            string? fixStyle,
            string? fixAnalyzers,
            string? verbosity,
            bool check,
            string[] include,
            string[] exclude,
            string? report,
            bool includeGenerated,
            IConsole console = null!)
        {
            if (s_parseResult == null)
            {
                return 1;
            }

            // Setup logging.
            var logLevel = GetLogLevel(verbosity);
            var logger = SetupLogging(console, minimalLogLevel: logLevel, minimalErrorLevel: LogLevel.Warning);

            // Hook so we can cancel and exit when ctrl+c is pressed.
            var cancellationTokenSource = new CancellationTokenSource();
            Console.CancelKeyPress += (sender, e) =>
            {
                e.Cancel = true;
                cancellationTokenSource.Cancel();
            };

            var currentDirectory = string.Empty;

            try
            {
                currentDirectory = Environment.CurrentDirectory;

                var formatVersion = GetVersion();
                logger.LogDebug(Resources.The_dotnet_format_version_is_0, formatVersion);

                string? workspaceDirectory;
                string workspacePath;
                WorkspaceType workspaceType;

                // The folder option means we should treat the project path as a folder path.
                if (folder)
                {
                    // If folder isn't populated, then use the current directory
                    workspacePath = Path.GetFullPath(workspace ?? ".", Environment.CurrentDirectory);
                    workspaceDirectory = workspacePath;
                    workspaceType = WorkspaceType.Folder;
                }
                else
                {
                    var (isSolution, workspaceFilePath) = MSBuildWorkspaceFinder.FindWorkspace(currentDirectory, workspace);

                    workspacePath = workspaceFilePath;
                    workspaceType = isSolution
                        ? WorkspaceType.Solution
                        : WorkspaceType.Project;

                    // To ensure we get the version of MSBuild packaged with the dotnet SDK used by the
                    // workspace, use its directory as our working directory which will take into account
                    // a global.json if present.
                    workspaceDirectory = Path.GetDirectoryName(workspacePath);
                    if (workspaceDirectory is null)
                    {
                        throw new Exception($"Unable to find folder at '{workspacePath}'");
                    }
                }

                if (workspaceType != WorkspaceType.Folder)
                {
                    var runtimeVersion = GetRuntimeVersion();
                    logger.LogDebug(Resources.The_dotnet_runtime_version_is_0, runtimeVersion);

                    // Load MSBuild
                    Environment.CurrentDirectory = workspaceDirectory;

                    if (!TryGetDotNetCliVersion(out var dotnetVersion))
                    {
                        logger.LogError(Resources.Unable_to_locate_dotnet_CLI_Ensure_that_it_is_on_the_PATH);
                        return UnableToLocateDotNetCliExitCode;
                    }

                    logger.LogTrace(Resources.The_dotnet_CLI_version_is_0, dotnetVersion);

                    if (!TryLoadMSBuild(out var msBuildPath))
                    {
                        logger.LogError(Resources.Unable_to_locate_MSBuild_Ensure_the_NET_SDK_was_installed_with_the_official_installer);
                        return UnableToLocateMSBuildExitCode;
                    }

                    logger.LogTrace(Resources.Using_msbuildexe_located_in_0, msBuildPath);
                }

                var fixType = FixCategory.None;
                if (s_parseResult.WasOptionUsed("--fix-style", "-s"))
                {
                    fixType |= FixCategory.CodeStyle;
                }

                if (s_parseResult.WasOptionUsed("--fix-analyzers", "-a"))
                {
                    fixType |= FixCategory.Analyzers;
                }

                if (fixType == FixCategory.None || fixWhitespace)
                {
                    fixType |= FixCategory.Whitespace;
                }

                HandleStandardInput(logger, ref include, ref exclude);

                var fileMatcher = SourceFileMatcher.CreateMatcher(include, exclude);

                var formatOptions = new FormatOptions(
                    workspacePath,
                    workspaceType,
                    logLevel,
                    fixType,
                    codeStyleSeverity: GetSeverity(fixStyle ?? FixSeverity.Error),
                    analyzerSeverity: GetSeverity(fixAnalyzers ?? FixSeverity.Error),
                    saveFormattedFiles: !check,
                    changesAreErrors: check,
                    fileMatcher,
                    reportPath: report,
                    includeGenerated);

                var formatResult = await CodeFormatter.FormatWorkspaceAsync(
                    formatOptions,
                    logger,
                    cancellationTokenSource.Token,
                    createBinaryLog: logLevel == LogLevel.Trace).ConfigureAwait(false);

                return GetExitCode(formatResult, check);
            }
            catch (FileNotFoundException fex)
            {
                logger.LogError(fex.Message);
                return UnhandledExceptionExitCode;
            }
            catch (OperationCanceledException)
            {
                return UnhandledExceptionExitCode;
            }
            finally
            {
                if (!string.IsNullOrEmpty(currentDirectory))
                {
                    Environment.CurrentDirectory = currentDirectory;
                }
            }
        }

        private static void HandleStandardInput(ILogger logger, ref string[] include, ref string[] exclude)
        {
            var isStandardMarkerUsed = false;
            if (include.Length == 1 && s_standardInputKeywords.Contains(include[0]))
            {
                if (TryReadFromStandardInput(ref include))
                {
                    isStandardMarkerUsed = true;
                }
            }

            if (exclude.Length == 1 && s_standardInputKeywords.Contains(exclude[0]))
            {
                if (isStandardMarkerUsed)
                {
                    logger.LogCritical(Resources.Standard_input_used_multiple_times);
                    Environment.Exit(CheckFailedExitCode);
                }

                TryReadFromStandardInput(ref exclude);
            }

            static bool TryReadFromStandardInput(ref string[] subject)
            {
                if (!Console.IsInputRedirected)
                {
                    return false; // pass
                }

                // reset the subject array
                Array.Clear(subject, 0, subject.Length);
                Array.Resize(ref subject, 0);

                Console.InputEncoding = Encoding.UTF8;
                using var reader = new StreamReader(Console.OpenStandardInput(8192));
                Console.SetIn(reader);

                for (var i = 0; Console.In.Peek() != -1; ++i)
                {
                    Array.Resize(ref subject, subject.Length + 1);
                    subject[i] = Console.In.ReadLine();
                }

                return true;
            }
        }

        internal static int GetExitCode(WorkspaceFormatResult formatResult, bool check)
        {
            if (!check)
            {
                return formatResult.ExitCode;
            }

            return formatResult.FilesFormatted == 0 ? 0 : CheckFailedExitCode;
        }

        internal static LogLevel GetLogLevel(string? verbosity)
        {
            switch (verbosity)
            {
                case "q":
                case "quiet":
                    return LogLevel.Error;
                case "m":
                case "minimal":
                    return LogLevel.Warning;
                case "n":
                case "normal":
                    return LogLevel.Information;
                case "d":
                case "detailed":
                    return LogLevel.Debug;
                case "diag":
                case "diagnostic":
                    return LogLevel.Trace;
                default:
                    return LogLevel.Information;
            }
        }

        internal static DiagnosticSeverity GetSeverity(string? severity)
        {
            return severity?.ToLowerInvariant() switch
            {
                FixSeverity.Error => DiagnosticSeverity.Error,
                FixSeverity.Warn => DiagnosticSeverity.Warning,
                FixSeverity.Info => DiagnosticSeverity.Info,
                _ => throw new ArgumentOutOfRangeException(nameof(severity)),
            };
        }

        private static ILogger<Program> SetupLogging(IConsole console, LogLevel minimalLogLevel, LogLevel minimalErrorLevel)
        {
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddSingleton(new LoggerFactory().AddSimpleConsole(console, minimalLogLevel, minimalErrorLevel));
            serviceCollection.AddLogging();

            var serviceProvider = serviceCollection.BuildServiceProvider();
            var logger = serviceProvider.GetService<ILogger<Program>>();

            return logger!;
        }

        private static string GetVersion()
        {
            return Assembly.GetExecutingAssembly()
                .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
                .InformationalVersion;
        }

        private static bool TryGetDotNetCliVersion([NotNullWhen(returnValue: true)] out string? dotnetVersion)
        {
            try
            {
                var processInfo = ProcessRunner.CreateProcess("dotnet", "--version", captureOutput: true, displayWindow: false);
                var versionResult = processInfo.Result.GetAwaiter().GetResult();

                dotnetVersion = versionResult.OutputLines[0].Trim();
                return true;
            }
            catch
            {
                dotnetVersion = null;
                return false;
            }
        }

        private static bool TryLoadMSBuild([NotNullWhen(returnValue: true)] out string? msBuildPath)
        {
            try
            {
                // Since we are running as a dotnet tool we should be able to find an instance of
                // MSBuild in a .NET Core SDK.
                var msBuildInstance = Build.Locator.MSBuildLocator.QueryVisualStudioInstances().First();

                // Since we do not inherit msbuild.deps.json when referencing the SDK copy
                // of MSBuild and because the SDK no longer ships with version matched assemblies, we
                // register an assembly loader that will load assemblies from the msbuild path with
                // equal or higher version numbers than requested.
                LooseVersionAssemblyLoader.Register(msBuildInstance.MSBuildPath);
                Build.Locator.MSBuildLocator.RegisterInstance(msBuildInstance);

                msBuildPath = msBuildInstance.MSBuildPath;
                return true;
            }
            catch
            {
                msBuildPath = null;
                return false;
            }
        }

        internal static string GetRuntimeVersion()
        {
            var pathParts = typeof(string).Assembly.Location.Split('\\', '/');
            var netCoreAppIndex = Array.IndexOf(pathParts, "Microsoft.NETCore.App");
            return pathParts[netCoreAppIndex + 1];
        }
    }
}
