// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.TestFramework.Utilities;
using static Microsoft.DotNet.Cli.Utils.ExponentialRetry;

namespace Microsoft.NET.TestFramework.Commands
{
    public abstract class TestCommand
    {
        private readonly Dictionary<string, string> _environment = [];
        private readonly HashSet<int> _retryOnExitCodes = [];
        private bool _doNotEscapeArguments;
        public ITestOutputHelper Log { get; }
        public string? WorkingDirectory { get; set; }
        public List<string> Arguments { get; set; } = [];
        public List<string> EnvironmentToRemove { get; } = [];
        public bool RedirectStandardInput { get; set; }
        public bool DisableOutputAndErrorRedirection { get; set; }

        /// <summary>
        /// When true, streams all stdout/stderr lines to test output in real-time,
        /// regardless of exit code. Useful for tests that need to diagnose output
        /// from commands that succeed but produce unexpected results.
        /// Can also be enabled globally via the DOTNET_SDK_TEST_VERBOSE=1 environment variable.
        /// </summary>
        public bool VerboseOutput { get; set; }

        /// <summary>
        /// When true, the child process is launched in a new process group so that
        /// console signals (e.g. Ctrl+C) sent to it do not propagate to the test host.
        /// </summary>
        public bool CreateNewProcessGroup { get; set; }

        //  These only work via Execute(), not when using GetProcessStartInfo()
        public Action<string>? CommandOutputHandler { get; set; }
        public Action<Process>? ProcessStartedHandler { get; set; }

        public Encoding? StandardOutputEncoding { get; set; }

        protected TestCommand(ITestOutputHelper log)
        {
            Log = log;
        }

        protected abstract SdkCommandSpec CreateCommand(IEnumerable<string> args);

        public TestCommand WithEnvironmentVariable(string name, string value)
        {
            _environment[name] = value;
            return this;
        }

        public TestCommand WithWorkingDirectory(string workingDirectory)
        {
            WorkingDirectory = workingDirectory;
            return this;
        }

        public TestCommand WithDisableOutputAndErrorRedirection()
        {
            DisableOutputAndErrorRedirection = true;
            return this;
        }

        public TestCommand WithStandardInput(string stdin)
        {
            Debug.Assert(ProcessStartedHandler == null);
            RedirectStandardInput = true;
            ProcessStartedHandler = (process) =>
            {
                process.StandardInput.Write(stdin);
                process.StandardInput.Close();
            };
            return this;
        }

        public TestCommand WithStandardOutputEncoding(Encoding encoding)
        {
            StandardOutputEncoding = encoding;
            return this;
        }

        /// <summary>
        /// Instructs not to escape the arguments when launching command.
        /// This may be used to pass ready arguments line as single string argument.
        /// </summary>
        public TestCommand WithRawArguments()
        {
            _doNotEscapeArguments = true;
            return this;
        }

        public TestCommand WithCulture(string locale) => WithEnvironmentVariable(UILanguageOverride.DOTNET_CLI_UI_LANGUAGE, locale);

        /// <summary>
        /// Configures the command to retry when the specified exit code is returned (only when executing via Execute()/Execute(params string[])).
        /// Useful for transient errors like file locks from background processes.
        /// </summary>
        public TestCommand WithRetryOnExitCode(int exitCode)
        {
            _retryOnExitCodes.Add(exitCode);
            return this;
        }

        public TestCommand WithTraceOutput()
        {
            WithEnvironmentVariable("DOTNET_CLI_VSTEST_TRACE", "1");
            return this;
        }

        private SdkCommandSpec CreateCommandSpec(IEnumerable<string> args)
        {
            var commandSpec = CreateCommand(args);
            foreach (var kvp in _environment)
            {
                commandSpec.Environment[kvp.Key] = kvp.Value;
            }

            foreach (var envToRemove in EnvironmentToRemove)
            {
                commandSpec.EnvironmentToRemove.Add(envToRemove);
            }

            if (WorkingDirectory != null)
            {
                commandSpec.WorkingDirectory = WorkingDirectory;
            }

            if (Arguments.Any())
            {
                commandSpec.Arguments = Arguments.Concat(commandSpec.Arguments).ToList();
            }

            commandSpec.RedirectStandardInput = RedirectStandardInput;
            commandSpec.DisableOutputAndErrorRedirection = DisableOutputAndErrorRedirection;
            commandSpec.CreateNewProcessGroup = CreateNewProcessGroup;

            return commandSpec;
        }

        public ProcessStartInfo GetProcessStartInfo(params string[] args)
        {
            var commandSpec = CreateCommandSpec(args);

            var psi = commandSpec.ToProcessStartInfo();

            return psi;
        }

        public CommandResult Execute(params string[] args)
        {
            IEnumerable<string> enumerableArgs = args;
            return ExecuteWithRetry(
                    action: () => Execute(enumerableArgs),
                    shouldStopRetry: ShouldStopRetry,
                    maxRetryCount: 3,
                    timer: () => Timer(Intervals),
                    taskDescription: "Run command while retrying transient errors")
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private bool ShouldStopRetry(CommandResult result)
        {
            if (result.ExitCode == 0)
            {
                return true;
            }

            if (_retryOnExitCodes.Contains(result.ExitCode))
            {
                return false;
            }

            return !NuGetTransientErrorDetector.IsTransientError(result.StdOut)
                && !NuGetTransientErrorDetector.IsTransientError(result.StdErr)
                && !TransientSdkResolutionErrorDetector.IsTransientError(result.StdOut);
        }

        public virtual CommandResult Execute(IEnumerable<string> args)
        {
            var spec = CreateCommandSpec(args);

            var command = spec
                .ToCommand(_doNotEscapeArguments);

            bool verboseTestOutput = VerboseOutput || string.Equals(
                Environment.GetEnvironmentVariable("DOTNET_SDK_TEST_VERBOSE"),
                "1",
                StringComparison.Ordinal);

            if (!spec.DisableOutputAndErrorRedirection)
            {
                command
                    .CaptureStdOut()
                    .CaptureStdErr();

                if (verboseTestOutput)
                {
                    // Stream output in real-time for verbose mode.
                    command
                        .OnOutputLine(line =>
                        {
                            Log.WriteLine($"》{line}");
                            CommandOutputHandler?.Invoke(line);
                        })
                        .OnErrorLine(line =>
                        {
                            Log.WriteLine($"❌{line}");
                        });
                }
                else if (CommandOutputHandler is not null)
                {
                    // Still invoke the handler for tests that process output,
                    // but don't log to test output.
                    command.OnOutputLine(line => CommandOutputHandler.Invoke(line));
                }

                // Decode captured stdout as UTF-8 by default so non-ASCII output (e.g. localized
                // strings such as the French "Bienvenue à .Net!") is not corrupted by the host
                // console's active code page. Child dotnet/MSBuild processes emit UTF-8; when
                // StandardOutputEncoding is left null, Process decodes the redirected stream using
                // Console.OutputEncoding, which on Windows is the OEM code page (e.g. CP437/850) and
                // turns UTF-8 bytes like 0xC3 0xA0 ('à') into mojibake ('├á'). Because the active code
                // page varies by agent, tests asserting on non-ASCII output failed intermittently. On
                // non-Windows the default capture encoding is already UTF-8, so this is a no-op there.
                command.StandardOutputEncoding(StandardOutputEncoding ?? Encoding.UTF8);
            }

            string fileToShow = Path.GetFileNameWithoutExtension(spec.FileName!).Equals("dotnet", StringComparison.OrdinalIgnoreCase) ?
                "dotnet" :
                spec.FileName!;
            var display = $"{fileToShow} {string.Join(" ", spec.Arguments)}";

            Log.WriteLine($"Executing '{display}':");
            var result = command.Execute(ProcessStartedHandler);
            Log.WriteLine($"Command '{display}' exited with exit code {result.ExitCode}.");

            // On failure, dump the already-captured output so the cause is visible in test logs.
            // Uses result.StdOut/StdErr (captured by CaptureStdOut/CaptureStdErr) to avoid
            // buffering output a second time in memory.
            if (!verboseTestOutput && result.ExitCode != 0)
            {
                if (!string.IsNullOrEmpty(result.StdOut))
                {
                    foreach (var line in result.StdOut.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
                    {
                        Log.WriteLine($"》{line}");
                    }
                }
                if (!string.IsNullOrEmpty(result.StdErr))
                {
                    foreach (var line in result.StdErr.Split(new[] { Environment.NewLine }, StringSplitOptions.None))
                    {
                        Log.WriteLine($"❌{line}");
                    }
                }
            }
            else if (!verboseTestOutput)
            {
                int stdOutLines = string.IsNullOrEmpty(result.StdOut) ? 0 : result.StdOut.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Length;
                int stdErrLines = string.IsNullOrEmpty(result.StdErr) ? 0 : result.StdErr.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Length;
                if (stdOutLines + stdErrLines > 0)
                {
                    Log.WriteLine($"  ({stdOutLines} stdout + {stdErrLines} stderr lines suppressed — set DOTNET_SDK_TEST_VERBOSE=1 or command.VerboseOutput=true for full output)");
                }
            }

            if (Environment.GetEnvironmentVariable("HELIX_WORKITEM_UPLOAD_ROOT") is string uploadRoot)
            {
                var workingDir = spec.WorkingDirectory ?? Environment.CurrentDirectory;
                var binlogFiles = Directory.GetFiles(workingDir, "*.binlog");
                // Multiple tests in the same Helix work item often produce binlogs with the same
                // relative filename (e.g. "msbuild0.binlog"). Prefix with the last two segments of
                // the working directory (typically "<TestInstance>---<GUID>-<ProjectDir>") so each
                // upload is uniquely identifiable.
                var parts = workingDir
                    .TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                    .Split([Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar], StringSplitOptions.RemoveEmptyEntries);
                var prefix = parts.Length >= 2
                    ? $"{parts[parts.Length - 2]}-{parts[parts.Length - 1]}"
                    : (parts.Length == 1 ? parts[0] : string.Empty);
                foreach (string binlogFile in binlogFiles)
                {
                    var destName = string.IsNullOrEmpty(prefix)
                        ? Path.GetFileName(binlogFile)
                        : $"{prefix}-{Path.GetFileName(binlogFile)}";
                    var destPath = Path.Combine(uploadRoot, destName);
                    // Binlog upload is diagnostic-only; copy failures will not fail the test.
                    FileUtility.TryCopyFile(binlogFile, destPath, Log);
                }
            }

            return result;
        }

        public static void LogCommandResult(ITestOutputHelper log, CommandResult result)
        {
            log.WriteLine($"> {result.StartInfo.FileName} {result.StartInfo.Arguments}");
            log.WriteLine(result.StdOut ?? string.Empty);

            if (!string.IsNullOrEmpty(result.StdErr))
            {
                log.WriteLine("");
                log.WriteLine("StdErr:");
                log.WriteLine(result.StdErr);
            }

            if (result.ExitCode != 0)
            {
                log.WriteLine($"Exit Code: {result.ExitCode}");
            }
        }
    }
}
