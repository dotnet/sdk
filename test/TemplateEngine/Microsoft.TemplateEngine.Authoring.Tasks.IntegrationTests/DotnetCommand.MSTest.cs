// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.NET.TestFramework;

namespace Microsoft.TemplateEngine.CommandUtils
{
    internal class DotnetCommand : TestCommand
    {
        private string _executableFilePath = SdkTestContext.Current.ToolsetUnderTest.DotNetHostPath;

        internal DotnetCommand(ILogger log, string subcommand, params string[] args) : base(log)
        {
            Arguments.Add(subcommand);
            Arguments.AddRange(args);
        }

        internal DotnetCommand(ITestOutputHelper log, string subcommand, params string[] args) : base(log)
        {
            Arguments.Add(subcommand);
            Arguments.AddRange(args);
        }

        internal DotnetCommand WithoutTelemetry()
        {
            WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "true");
            return this;
        }

        internal DotnetCommand WithCustomExecutablePath(string? executableFilePath)
        {
            if (!string.IsNullOrEmpty(executableFilePath))
            {
                _executableFilePath = executableFilePath;
            }
            return this;
        }

        private protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
        {
            var commandSpec = new SdkCommandSpec()
            {
                FileName = _executableFilePath,
                Arguments = args.ToList(),
                WorkingDirectory = WorkingDirectory
            };

            var environment = new Dictionary<string, string?>();
            SdkTestContext.Current.AddTestEnvironmentVariables(environment);
            foreach ((string name, string? value) in environment)
            {
                if (value is null)
                {
                    commandSpec.EnvironmentToRemove.Add(name);
                }
                else
                {
                    commandSpec.Environment[name] = value;
                }
            }
            return commandSpec;
        }
    }
}
