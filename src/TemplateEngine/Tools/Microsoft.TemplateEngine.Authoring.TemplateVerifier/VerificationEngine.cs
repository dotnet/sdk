// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier.Commands;
using Microsoft.TemplateEngine.CommandUtils;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Authoring.TemplateVerifier
{
    public class VerificationEngine
    {
        private static readonly IReadOnlyList<string> DefaultVerificationExcludePatterns = new List<string>()
        {
            @"**/obj/*",
            @"**\obj\*",
            @"**/bin/*",
            @"**\bin\*",
            "*.exe",
            "*.dll",
            "*.",
        };

        private readonly ILogger _logger;
        private readonly ILoggerFactory? _loggerFactory;
        private readonly ICommandRunner _commandRunner = new CommandRunner();
        private readonly IPhysicalFileSystemEx _fileSystem = new PhysicalFileSystemEx();

        static VerificationEngine()
        {
            // Customize diff output of verifier
            VerifyDiffPlex.Initialize(OutputType.Compact);
            VerifierSettings.UseSplitModeForUniqueDirectory();
        }

        public VerificationEngine(ILogger logger)
        {
            _logger = logger;
        }

        public VerificationEngine(ILoggerFactory loggerFactory)
        : this(loggerFactory.CreateLogger(typeof(VerificationEngine)))
        {
            _loggerFactory = loggerFactory;
        }

        internal VerificationEngine(ICommandRunner commandRunner, ILogger logger)
        : this(logger)
        {
            _commandRunner = commandRunner;
        }

        /// <summary>
        /// Asynchronously performs the scenario and its verification based on given configuration options.
        /// </summary>
        /// <param name="optionsAccessor">Configuration of the scenario and verification.</param>
        /// <param name="cancellationToken"></param>
        /// <param name="sourceFile"></param>
        /// <param name="callerMethod"></param>
        /// <returns>A <see cref="Task"/> Task to be awaited.</returns>
        public async Task Execute(
            IOptions<TemplateVerifierOptions> optionsAccessor,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string sourceFile = "",
            [CallerMemberName] string callerMethod = "")
        {
            if (optionsAccessor == null)
            {
                throw new ArgumentNullException(nameof(optionsAccessor));
            }

            TemplateVerifierOptions options = optionsAccessor.Value;

            CommandResultData commandResult = RunDotnetNewCommand(options, _commandRunner, _loggerFactory, _logger);

            if (options.IsCommandExpectedToFail)
            {
                if (commandResult.ExitCode == 0)
                {
                    throw new TemplateVerificationException(
                        LocalizableStrings.VerificationEngine_Error_UnexpectedPass,
                        TemplateVerificationErrorCode.VerificationFailed);
                }
            }
            else
            {
                if (commandResult.ExitCode != 0)
                {
                    throw new TemplateVerificationException(
                        string.Format(LocalizableStrings.VerificationEngine_Error_UnexpectedFail, commandResult.ExitCode),
                        TemplateVerificationErrorCode.InstantiationFailed);
                }

                // We do not expect stderr in passing command.
                // However if verification of stdout and stderr is opted-in - we will let that verification validate the stderr content
                if (!options.VerifyCommandOutput && !string.IsNullOrEmpty(commandResult.StdErr))
                {
                    throw new TemplateVerificationException(
                        string.Format(
                            LocalizableStrings.VerificationEngine_Error_UnexpectedStdErr,
                            Environment.NewLine,
                            commandResult.StdErr),
                        TemplateVerificationErrorCode.InstantiationFailed);
                }
            }

            await VerifyResult(options, commandResult, string.IsNullOrEmpty(sourceFile) ? string.Empty : Path.GetDirectoryName(sourceFile)!, callerMethod)
                .ConfigureAwait(false);

            // if everything is successful - let's delete the created files (unless placed into explicitly requested dir)
            if (string.IsNullOrEmpty(options.OutputDirectory) && _fileSystem.DirectoryExists(commandResult.WorkingDirectory))
            {
                _fileSystem.DirectoryDelete(commandResult.WorkingDirectory, true);
            }
        }

        internal static Task CreateVerificationTask(
            string contentDir,
            string callerDir,
            string? callerMethodName,
            TemplateVerifierOptions options,
            IPhysicalFileSystemEx fileSystem)
        {
            List<string> exclusionsList = options.DisableDefaultVerificationExcludePatterns
                ? new()
                : new(DefaultVerificationExcludePatterns);

            if (options.VerificationExcludePatterns != null)
            {
                exclusionsList.AddRange(options.VerificationExcludePatterns);
            }

            List<IPatternMatcher> excludeGlobs = exclusionsList.Select(pattern => (IPatternMatcher)Glob.Parse(pattern)).ToList();
            List<IPatternMatcher> includeGlobs = new();

            if (options.VerificationIncludePatterns != null)
            {
                includeGlobs.AddRange(options.VerificationIncludePatterns.Select(pattern => Glob.Parse(pattern)));
            }

            if (!includeGlobs.Any())
            {
                includeGlobs.Add(Glob.MatchAll);
            }

            if (options.CustomDirectoryVerifier != null)
            {
                return options.CustomDirectoryVerifier(
                    contentDir,
                    new Lazy<IAsyncEnumerable<(string FilePath, string ScrubbedContent)>>(
                        GetVerificationContent(contentDir, includeGlobs, excludeGlobs, options.CustomScrubbers, fileSystem)));
            }

            VerifySettings verifySettings = new();

            // Scrubbers by file: https://github.com/VerifyTests/Verify/issues/673
            if (options.CustomScrubbers != null)
            {
                if (options.CustomScrubbers.GeneralScrubber != null)
                {
                    verifySettings.AddScrubber(options.CustomScrubbers.GeneralScrubber);
                }

                foreach (var pair in options.CustomScrubbers.ScrubersByExtension)
                {
                    verifySettings.AddScrubber(pair.Key, pair.Value);
                }
            }

            string scenarioPrefix = options.DoNotPrependTemplateNameToScenarioName ? string.Empty : options.TemplateName;
            if (!options.DoNotPrependCallerMethodNameToScenarioName && !string.IsNullOrEmpty(callerMethodName))
            {
                scenarioPrefix = callerMethodName + (string.IsNullOrEmpty(scenarioPrefix) ? null : ".") + scenarioPrefix;
            }
            scenarioPrefix = string.IsNullOrEmpty(scenarioPrefix) ? "_" : scenarioPrefix;
            verifySettings.UseTypeName(scenarioPrefix);
            string snapshotsDir = options.SnapshotsDirectory ?? "Snapshots";
            if (!string.IsNullOrEmpty(callerDir) && !Path.IsPathRooted(snapshotsDir))
            {
                snapshotsDir = Path.Combine(callerDir, snapshotsDir);
            }
            verifySettings.UseDirectory(snapshotsDir);
            verifySettings.UseMethodName(GetScenarioName(options));

            if ((options.UniqueFor ?? UniqueForOption.None) != UniqueForOption.None)
            {
                foreach (UniqueForOption value in Enum.GetValues(typeof(UniqueForOption)))
                {
                    if ((options.UniqueFor & value) == value)
                    {
                        switch (value)
                        {
                            case UniqueForOption.None:
                                break;
                            case UniqueForOption.Architecture:
                                verifySettings.UniqueForArchitecture();
                                break;
                            case UniqueForOption.OsPlatform:
                                verifySettings.UniqueForOSPlatform();
                                break;
                            case UniqueForOption.Runtime:
                                verifySettings.UniqueForRuntime();
                                break;
                            case UniqueForOption.RuntimeAndVersion:
                                verifySettings.UniqueForRuntimeAndVersion();
                                break;
                            case UniqueForOption.TargetFramework:
                                verifySettings.UniqueForTargetFramework();
                                break;
                            case UniqueForOption.TargetFrameworkAndVersion:
                                verifySettings.UniqueForTargetFrameworkAndVersion();
                                break;
                            default:
                                throw new ArgumentOutOfRangeException();
                        }
                    }
                }
            }

            if (options.DisableDiffTool)
            {
                verifySettings.DisableDiff();
            }

            return Verifier.VerifyDirectory(
                contentDir,
                include: (filePath) =>
                {
                    string relativePath = fileSystem.PathRelativeTo(filePath, contentDir);
                    return includeGlobs.Any(g => g.IsMatch(relativePath)) && !excludeGlobs.Any(g => g.IsMatch(relativePath));
                },
                options: new EnumerationOptions()
                {
                    RecurseSubdirectories = true,
                    // Workarounding Verify bug https://github.com/VerifyTests/Verify/issues/699
                    AttributesToSkip = 0
                },
                fileScrubber: ExtractFileScrubber(options, contentDir, fileSystem),
                settings: verifySettings);
        }

        private static FileScrubber? ExtractFileScrubber(TemplateVerifierOptions options, string contentDir, IPhysicalFileSystemEx fileSystem)
        {
            if (!(options.CustomScrubbers?.ByPathScrubbers.Any() ?? false))
            {
                return null;
            }

            return (fullPath, builder) =>
            {
                string relativePath = fileSystem.PathRelativeTo(fullPath, contentDir);
                options.CustomScrubbers.ByPathScrubbers.ForEach(scrubberByPath => scrubberByPath(relativePath, builder));
            };
        }

        private static string GetScenarioName(TemplateVerifierOptions options)
        {
            // TBD: once the custom SDK switching feature is implemented - here we should append the sdk distinguisher if UniqueForOption.Runtime requested

            var scenarioName = options.ScenarioName + (options.DoNotAppendTemplateArgsToScenarioName ? null : EncodeArgsAsPath(options.TemplateSpecificArgs));
            if (string.IsNullOrEmpty(scenarioName))
            {
                scenarioName = "_";
            }
            return scenarioName;
        }

        private static string EncodeArgsAsPath(IEnumerable<string>? args)
        {
            if (args == null || !args.Any())
            {
                return string.Empty;
            }

            Regex r = new Regex(string.Format("[{0}]", Regex.Escape(new string(Path.GetInvalidFileNameChars()))));
            return r.Replace(string.Join('#', args), string.Empty);
        }

        private static CommandResultData RunDotnetNewCommand(TemplateVerifierOptions options, ICommandRunner commandRunner, ILoggerFactory? loggerFactory, ILogger logger)
        {
            // Create temp folder and instantiate there
            string workingDir = options.OutputDirectory ?? Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            if (Directory.Exists(workingDir) && Directory.EnumerateFileSystemEntries(workingDir).Any())
            {
                throw new TemplateVerificationException(LocalizableStrings.VerificationEngine_Error_WorkDirExists, TemplateVerificationErrorCode.WorkingDirectoryExists);
            }

            Directory.CreateDirectory(workingDir);
            ILogger commandLogger = loggerFactory?.CreateLogger(typeof(DotnetCommand)) ?? logger;
            string? customHiveLocation = options.SettingsDirectory;

            if (!string.IsNullOrEmpty(options.TemplatePath))
            {
                customHiveLocation ??= Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "home");
                var installCommand =
                    new DotnetNewCommand(commandLogger, "install", options.TemplatePath)
                        .WithCustomHive(customHiveLocation)
                        .WithCustomExecutablePath(options.DotnetExecutablePath)
                        .WithEnvironmentVariables(options.Environment)
                        .WithWorkingDirectory(workingDir);

                CommandResultData installCommandResult = commandRunner.RunCommand(installCommand);

                if (installCommandResult.ExitCode != 0)
                {
                    throw new TemplateVerificationException(
                        string.Format(LocalizableStrings.VerificationEngine_Error_InstallUnexpectedFail, installCommandResult.ExitCode),
                        TemplateVerificationErrorCode.InstantiationFailed);
                }
            }

            List<string> cmdArgs = new()
            {
                options.TemplateName
            };
            if (options.TemplateSpecificArgs != null)
            {
                cmdArgs.AddRange(options.TemplateSpecificArgs);
            }

            // let's make sure the template outputs are named and placed deterministically
            if (!cmdArgs.Select(arg => arg.Trim())
                    .Any(arg => new[] { "-n", "--name" }.Contains(arg, StringComparer.OrdinalIgnoreCase)))
            {
                cmdArgs.Add("-n");
                cmdArgs.Add(options.TemplateName);
            }
            if (!cmdArgs.Select(arg => arg.Trim())
                    .Any(arg => new[] { "-o", "--output" }.Contains(arg, StringComparer.OrdinalIgnoreCase)))
            {
                cmdArgs.Add("-o");
                cmdArgs.Add(options.TemplateName);
            }

            var command = new DotnetNewCommand(loggerFactory?.CreateLogger(typeof(DotnetCommand)) ?? logger, cmdArgs.ToArray())
                .WithCustomOrVirtualHive(customHiveLocation)
                .WithCustomExecutablePath(options.DotnetExecutablePath)
                .WithEnvironmentVariables(options.Environment)
                .WithWorkingDirectory(workingDir);

            var result = commandRunner.RunCommand(command);
            // Cleanup, unless the settings dir was externally passed
            if (!string.IsNullOrEmpty(customHiveLocation) && string.IsNullOrEmpty(options.SettingsDirectory))
            {
                Directory.Delete(customHiveLocation, true);
            }
            return result;
        }

        private static void DummyMethod()
        { }

        private static async IAsyncEnumerable<(string FilePath, string ScrubbedContent)> GetVerificationContent(
            string contentDir,
            List<IPatternMatcher> includeMatchers,
            List<IPatternMatcher> excludeMatchers,
            ScrubbersDefinition? scrubbers,
            IPhysicalFileSystemEx fileSystem)
        {
            foreach (string filePath in fileSystem.EnumerateFiles(contentDir, "*", SearchOption.AllDirectories))
            {
                string relativePath = fileSystem.PathRelativeTo(filePath, contentDir);

                if (!includeMatchers.Any(g => g.IsMatch(relativePath)))
                {
                    continue;
                }

                if (excludeMatchers.Any(g => g.IsMatch(relativePath)))
                {
                    continue;
                }

                string content = await fileSystem.ReadAllTextAsync(filePath).ConfigureAwait(false);

                if (scrubbers != null)
                {
                    string extension = Path.GetExtension(filePath);
                    // This is to get the same behavior as Verify.NET
                    if (extension.Length > 0)
                    {
                        extension = extension[1..];
                    }
                    StringBuilder? sb = null;

                    if (scrubbers.ByPathScrubbers.Any())
                    {
                        sb = new StringBuilder(content);
                        scrubbers.ByPathScrubbers.ForEach(scrubberByPath => scrubberByPath(relativePath, sb));
                    }

                    if (!string.IsNullOrEmpty(extension) && scrubbers.ScrubersByExtension.TryGetValue(extension, out Action<StringBuilder>? scrubber))
                    {
                        sb ??= new StringBuilder(content);
                        scrubber(sb);
                    }

                    if (scrubbers.GeneralScrubber != null)
                    {
                        sb ??= new StringBuilder(content);
                        scrubbers.GeneralScrubber(sb);
                    }

                    if (sb != null)
                    {
                        content = sb.ToString();
                    }
                }

                yield return new(relativePath, content);
            }
        }

        private async Task VerifyResult(TemplateVerifierOptions args, CommandResultData commandResultData, string callerDir, string callerMethodName)
        {
            UsesVerifyAttribute a = new UsesVerifyAttribute();
            // https://github.com/VerifyTests/Verify/blob/d8cbe38f527d6788ecadd6205c82803bec3cdfa6/src/Verify.Xunit/Verifier.cs#L10
            //  need to simulate execution from tests
            var v = DummyMethod;
            MethodInfo mi = v.Method;
            a.Before(mi);

            if (args.VerifyCommandOutput)
            {
                if (_fileSystem.DirectoryExists(Path.Combine(commandResultData.WorkingDirectory, SpecialFiles.StandardStreamsDir)))
                {
                    throw new TemplateVerificationException(
                        string.Format(
                            LocalizableStrings.VerificationEngine_Error_StdOutFolderExists,
                            SpecialFiles.StandardStreamsDir),
                        TemplateVerificationErrorCode.InternalError);
                }

                _fileSystem.CreateDirectory(Path.Combine(commandResultData.WorkingDirectory, SpecialFiles.StandardStreamsDir));

                await _fileSystem.WriteAllTextAsync(
                    Path.Combine(commandResultData.WorkingDirectory, SpecialFiles.StandardStreamsDir, SpecialFiles.StdOut + (args.StandardOutputFileExtension ?? SpecialFiles.DefaultExtension)),
                    commandResultData.StdOut)
                    .ConfigureAwait(false);

                await _fileSystem.WriteAllTextAsync(
                        Path.Combine(commandResultData.WorkingDirectory, SpecialFiles.StandardStreamsDir, SpecialFiles.StdErr + (args.StandardOutputFileExtension ?? SpecialFiles.DefaultExtension)),
                        commandResultData.StdErr)
                    .ConfigureAwait(false);
            }

            Task verifyTask = CreateVerificationTask(commandResultData.WorkingDirectory, callerDir, callerMethodName, args, _fileSystem);

            try
            {
                await verifyTask.ConfigureAwait(false);
            }
            catch (Exception e)
            {
                if (e is TemplateVerificationException)
                {
                    throw;
                }
                if (e.GetType().Name == "VerifyException")
                {
                    throw new TemplateVerificationException(e.Message, TemplateVerificationErrorCode.VerificationFailed);
                }
                else
                {
                    _logger.LogError(e, LocalizableStrings.VerificationEngine_Error_Unexpected);
                    throw;
                }
            }
        }

        private static class SpecialFiles
        {
            public const string StandardStreamsDir = "std-streams";
            public const string StdOut = "stdout";
            public const string StdErr = "stderr";
            public const string DefaultExtension = ".txt";
            public static readonly string[] FileNames = { StdOut, StdErr };
        }
    }
}
