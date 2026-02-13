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

        public string? WorkingDirectory { get; set; }

        public List<string> Arguments { get; set; } = new List<string>();

        public List<string> EnvironmentToRemove { get; } = new List<string>();

        public bool RedirectStandardInput { get; set; }

        public bool DisableOutputAndErrorRedirection { get; set; }

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

            if (!spec.DisableOutputAndErrorRedirection)
            {
                command
                    .CaptureStdOut()
                    .CaptureStdErr()
                    .OnOutputLine(line =>
                     {
                         Log.WriteLine($"》{line}");
                         CommandOutputHandler?.Invoke(line);
                     })
                    .OnErrorLine(line =>
                    {
                        Log.WriteLine($"❌{line}");
                    });

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

            if (Environment.GetEnvironmentVariable("HELIX_WORKITEM_UPLOAD_ROOT") is string uploadRoot)
            {
                var binlogFiles = Directory.GetFiles(spec.WorkingDirectory ?? Environment.CurrentDirectory, "*.binlog");
                foreach (string binlogFile in binlogFiles)
                {
                    File.Copy(binlogFile, Path.Combine(uploadRoot, Path.GetFileName(binlogFile)), true);
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
