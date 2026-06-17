// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;
using static Microsoft.DotNet.Cli.Utils.ExponentialRetry;

namespace Microsoft.NET.TestFramework.Commands
{
    public abstract class TestCommand
    {
        private readonly Dictionary<string, string> _environment = [];
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
        /// When true, command output is written to a file on disk instead of the test log
        /// when the command fails. The file is placed in the Helix upload directory (if
        /// available) or a temp directory. Use this for tests that intentionally produce
        /// very large output (e.g., diagnostic verbosity) to avoid bloating CI logs.
        /// The output is still captured in <see cref="CommandResult.StdOut"/>/<see cref="CommandResult.StdErr"/>
        /// for assertions.
        /// </summary>
        public bool SuppressOutputOnFailure { get; set; }

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
                    shouldStopRetry: SuccessOrNotTransientRestoreError,
                    maxRetryCount: 3,
                    timer: () => Timer(Intervals),
                    taskDescription: "Run command while retry transient restore error")
                .ConfigureAwait(false).GetAwaiter().GetResult();
        }

        private static bool SuccessOrNotTransientRestoreError(CommandResult result)
        {
            if (result.ExitCode == 0)
            {
                return true;
            }

            return !NuGetTransientErrorDetector.IsTransientError(result.StdOut);
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

                if (StandardOutputEncoding is not null)
                {
                    command.StandardOutputEncoding(StandardOutputEncoding);
                }
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
                if (SuppressOutputOnFailure)
                {
                    // Write output to a file instead of the test log to avoid bloating CI logs.
                    var outputDir = Environment.GetEnvironmentVariable("HELIX_WORKITEM_UPLOAD_ROOT")
                        ?? Path.GetTempPath();
                    var fileName = $"cmd-output-{Guid.NewGuid():N}.log";
                    var outputPath = Path.Combine(outputDir, fileName);
                    File.WriteAllText(outputPath,
                        $"> {result.StartInfo.FileName} {result.StartInfo.Arguments}{Environment.NewLine}" +
                        $"Exit code: {result.ExitCode}{Environment.NewLine}{Environment.NewLine}" +
                        $"=== STDOUT ==={Environment.NewLine}{result.StdOut ?? ""}{Environment.NewLine}{Environment.NewLine}" +
                        $"=== STDERR ==={Environment.NewLine}{result.StdErr ?? ""}");
                    int stdOutLines = string.IsNullOrEmpty(result.StdOut) ? 0 : result.StdOut.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Length;
                    int stdErrLines = string.IsNullOrEmpty(result.StdErr) ? 0 : result.StdErr.Split(new[] { Environment.NewLine }, StringSplitOptions.None).Length;
                    Log.WriteLine($"  ⚠️ Command failed — output ({stdOutLines} stdout + {stdErrLines} stderr lines) written to: {outputPath}");
                }
                else
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
                    File.Copy(binlogFile, Path.Combine(uploadRoot, destName), true);
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
