// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;
using System.Collections.Generic;
using Xunit.Abstractions;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.NET.TestFramework.Commands
{
    public abstract class TestCommand
    {
        private Dictionary<string, string> _environment = new Dictionary<string, string>();

        public ITestOutputHelper Log { get; }

        public string WorkingDirectory { get; set; }

        public List<string> Arguments { get; set; } = new List<string>();

        protected TestCommand(ITestOutputHelper log)
        {
            Log = log;
        }

        protected abstract SdkCommandSpec CreateCommand(string[] additionalArgs);

        public TestCommand WithEnvironmentVariable(string name, string value)
        {
            _environment[name] = value;
            return this;
        }

        private SdkCommandSpec CreateCommandSpec(string [] additionalArgs)
        {
            var commandSpec = CreateCommand(additionalArgs);
            foreach (var kvp in _environment)
            {
                commandSpec.Environment[kvp.Key] = kvp.Value;
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

        public ProcessStartInfo GetProcessStartInfo(params string[] additionalArgs)
        {
            var commandSpec = CreateCommandSpec(additionalArgs);

            return commandSpec.ToProcessStartInfo();
        }

        public CommandResult Execute(params string[] additionalArgs)
        {
            var command = CreateCommandSpec(additionalArgs)
                .ToCommand()
                .CaptureStdOut()
                .CaptureStdErr();

            var result = command.Execute();

            Log.WriteLine($"> {result.StartInfo.FileName} {result.StartInfo.Arguments}");
            Log.WriteLine(result.StdOut);

            if (!string.IsNullOrEmpty(result.StdErr))
            {
                Log.WriteLine("");
                Log.WriteLine("StdErr:");
                Log.WriteLine(result.StdErr);
            }

            if (result.ExitCode != 0)
            {
                Log.WriteLine($"Exit Code: {result.ExitCode}");
            }

            return result;
        }
    }
}
