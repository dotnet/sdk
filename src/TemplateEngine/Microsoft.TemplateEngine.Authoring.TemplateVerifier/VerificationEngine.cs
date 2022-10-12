// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier.Commands;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Authoring.TemplateVerifier
{
    public class VerificationEngine
    {
        private static readonly IReadOnlyList<string> DefaultVerificationExcludePatterns = new List<string>()
        {
            @"obj/*",
            @"obj\*",
            @"bin/*",
            @"bin\*",
            "*.exe",
            "*.dll",
            "*.",
            "*.exe",
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

        public async Task Execute(
            IOptions<TemplateVerifierOptions> optionsAccessor,
            CancellationToken cancellationToken = default,
            [CallerFilePath] string sourceFile = "")
        {
            if (optionsAccessor == null)
            {
                throw new ArgumentNullException(nameof(optionsAccessor));
            }

            TemplateVerifierOptions options = optionsAccessor.Value;

            CommandResultData commandResult = RunDotnetNewCommand(options, _commandRunner, _loggerFactory, _logger);

            if (options.IsCommandExpectedToFail ?? false)
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
                if (!(options.VerifyCommandOutput ?? false) && !string.IsNullOrEmpty(commandResult.StdErr))
                {
                    throw new TemplateVerificationException(
                        string.Format(
                            LocalizableStrings.VerificationEngine_Error_UnexpectedStdErr,
                            Environment.NewLine,
                            commandResult.StdErr),
                        TemplateVerificationErrorCode.InstantiationFailed);
                }
            }

            await VerifyResult(options, commandResult, string.IsNullOrEmpty(sourceFile) ? string.Empty : Path.GetDirectoryName(sourceFile)!)
                .ConfigureAwait(false);
        }

        internal static Task CreateVerificationTask(
            string contentDir,
            string callerDir,
            TemplateVerifierOptions options,
            IPhysicalFileSystemEx fileSystem)
        {
            List<string> exclusionsList = (options.DisableDefaultVerificationExcludePatterns ?? false)
                ? new()
                : new(DefaultVerificationExcludePatterns);

            if (options.VerificationExcludePatterns != null)
            {
                exclusionsList.AddRange(options.VerificationExcludePatterns);
            }

            List<Glob> globs = exclusionsList.Select(pattern => Glob.Parse(pattern)).ToList();

            if (options.CustomDirectoryVerifier != null)
            {
                return options.CustomDirectoryVerifier(
                    contentDir,
                    new Lazy<IAsyncEnumerable<(string FilePath, string ScrubbedContent)>>(
                        GetVerificationContent(contentDir, globs, options.CustomScrubbers, fileSystem)));
            }

            VerifySettings verifySettings = new();

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

            verifySettings.UseTypeName(options.TemplateName);
            string expectationsDir = options.ExpectationsDirectory ?? "VerifyExpectations";
            if (!string.IsNullOrEmpty(callerDir) && !Path.IsPathRooted(expectationsDir))
            {
                expectationsDir = Path.Combine(callerDir, expectationsDir);
            }
            verifySettings.UseDirectory(expectationsDir);
            verifySettings.UseMethodName(EncodeArgsAsPath(options.TemplateSpecificArgs));

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

            if (options.DisableDiffTool ?? false)
            {
                verifySettings.DisableDiff();
            }

            return Verifier.VerifyDirectory(
                contentDir,
                (filePath) => !globs.Any(g => g.IsMatch(filePath)),
                settings: verifySettings);
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
            string? customHiveLocation = null;

            if (!string.IsNullOrEmpty(options.TemplatePath))
            {
                customHiveLocation = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName(), "home");
                var installCommand =
                    new DotnetCommand(commandLogger, "new", "install", options.TemplatePath)
                        .WithCustomHive(customHiveLocation)
                        .WithWorkingDirectory(workingDir);

                CommandResultData installCommandResult = commandRunner.RunCommand(installCommand);

                if (installCommandResult.ExitCode != 0)
                {
                    throw new TemplateVerificationException(
                        string.Format(LocalizableStrings.VerificationEngine_Error_InstallUnexpectedFail, installCommandResult.ExitCode),
                        TemplateVerificationErrorCode.InstantiationFailed);
                }
            }

            List<string> cmdArgs = new();
            if (!string.IsNullOrEmpty(options.DotnetNewCommandAssemblyPath))
            {
                cmdArgs.Add(options.DotnetNewCommandAssemblyPath);
            }
            cmdArgs.Add(options.TemplateName);
            if (options.TemplateSpecificArgs != null)
            {
                cmdArgs.AddRange(options.TemplateSpecificArgs);
            }

            if (!string.IsNullOrEmpty(customHiveLocation))
            {
                cmdArgs.Add("--debug:custom-hive");
                cmdArgs.Add(customHiveLocation);
            }
            else
            {
                cmdArgs.Add("--debug:ephemeral-hive");
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

            var command = new DotnetCommand(loggerFactory?.CreateLogger(typeof(DotnetCommand)) ?? logger, "new", cmdArgs.ToArray())
                    .WithWorkingDirectory(workingDir);
            var result = commandRunner.RunCommand(command);
            if (!string.IsNullOrEmpty(customHiveLocation))
            {
                Directory.Delete(customHiveLocation, true);
            }
            return result;
        }

        private static void DummyMethod()
        { }

        private static async IAsyncEnumerable<(string FilePath, string ScrubbedContent)> GetVerificationContent(
            string contentDir,
            List<Glob> globs,
            ScrubbersDefinition? scrubbers,
            IPhysicalFileSystemEx fileSystem)
        {
            foreach (string filePath in fileSystem.EnumerateFiles(contentDir, "*", SearchOption.AllDirectories))
            {
                if (globs.Any(g => g.IsMatch(filePath)))
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

                    if (!string.IsNullOrEmpty(extension) && scrubbers.ScrubersByExtension.TryGetValue(extension, out Action<StringBuilder>? scrubber))
                    {
                        sb = new StringBuilder(content);
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

                yield return new(filePath, content);
            }
        }

        private async Task VerifyResult(TemplateVerifierOptions args, CommandResultData commandResultData, string callerDir)
        {
            UsesVerifyAttribute a = new UsesVerifyAttribute();
            // https://github.com/VerifyTests/Verify/blob/d8cbe38f527d6788ecadd6205c82803bec3cdfa6/src/Verify.Xunit/Verifier.cs#L10
            //  need to simulate execution from tests
            var v = DummyMethod;
            MethodInfo mi = v.Method;
            a.Before(mi);

            if (args.VerifyCommandOutput ?? false)
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

            Task verifyTask = CreateVerificationTask(commandResultData.WorkingDirectory, callerDir, args, _fileSystem);

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
