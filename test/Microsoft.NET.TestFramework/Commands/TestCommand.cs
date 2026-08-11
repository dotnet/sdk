// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using Microsoft.DotNet.Cli.Utils;
using static Microsoft.DotNet.Cli.Utils.ExponentialRetry;

namespace Microsoft.NET.TestFramework.Commands
{
    public abstract class TestCommand
    {
        private Dictionary<string, string> _environment = new();
        private bool _doNotEscapeArguments;

        public ITestOutputHelper Log { get; }

        public string WorkingDirectory { get; set; }

        public List<string> Arguments { get; set; } = new List<string>();

        public List<string> EnvironmentToRemove { get; } = new List<string>();

        /// <summary>
        /// When true, the full command output is echoed to <see cref="Log"/> instead of being capped by a
        /// <see cref="TruncatingTestOutputHelper"/>. Leave false (the default) so a single very verbose
        /// command (e.g. <c>dotnet test -v diag</c>) can't flood xUnit's IPC channel and trigger a
        /// spurious hang-dump timeout. Set it (or call <see cref="WithoutTruncatedOutputLogging"/>) for
        /// tests that need the complete output in the test log.
        /// </summary>
        public bool DisableTruncatedOutputLogging { get; set; }

        //  These only work via Execute(), not when using GetProcessStartInfo()
        public Action<string> CommandOutputHandler { get; set; }
        public Action<Process> ProcessStartedHandler { get; set; }

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

        /// <summary>
        /// Opts this command out of bounded output logging so the complete command output is echoed to
        /// <see cref="Log"/>. See <see cref="DisableTruncatedOutputLogging"/>.
        /// </summary>
        public TestCommand WithoutTruncatedOutputLogging()
        {
            DisableTruncatedOutputLogging = true;
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
            var command = CreateCommandSpec(args)
                .ToCommand(_doNotEscapeArguments)
                .CaptureStdOut()
                .CaptureStdErr();

            if (CommandOutputHandler != null)
            {
                command.OnOutputLine(CommandOutputHandler);
            }

            var result = ((Command)command).Execute(ProcessStartedHandler);

            // By default, cap how much of the (potentially enormous) command output is echoed to the test
            // log so a single very verbose command (e.g. `dotnet test -v diag`) can't flush hundreds of
            // megabytes over the test host's IPC channel and trigger a spurious hang-dump timeout. The full
            // StdOut/StdErr remain on the returned CommandResult; only the log echo is bounded. Tests that
            // need the complete output in the log can opt out with WithoutTruncatedOutputLogging().
            if (DisableTruncatedOutputLogging)
            {
                LogCommandResult(Log, result);
            }
            else
            {
                using var truncatingLog = new TruncatingTestOutputHelper(Log);
                LogCommandResult(truncatingLog, result);
            }

            return result;
        }

        public static void LogCommandResult(ITestOutputHelper log, CommandResult result)
        {
            log.WriteLine($"> {result.StartInfo.FileName} {result.StartInfo.Arguments}");
            log.WriteLine(result.StdOut);

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
