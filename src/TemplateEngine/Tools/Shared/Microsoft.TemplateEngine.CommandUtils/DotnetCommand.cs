// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.CommandUtils
{
    internal class DotnetCommand : TestCommand
    {
        internal DotnetCommand(ILogger log, string subcommand, params string[] args) : base(log)
        {
            Arguments.Add(subcommand);
            Arguments.AddRange(args);
            //opt out from telemetry
            WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "true");
        }

        internal DotnetCommand(ITestOutputHelper log, string subcommand, params string[] args) : base(log)
        {
            Arguments.Add(subcommand);
            Arguments.AddRange(args);
            //opt out from telemetry
            WithEnvironmentVariable("DOTNET_CLI_TELEMETRY_OPTOUT", "true");
        }

        private protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
        {
            var sdkCommandSpec = new SdkCommandSpec()
            {
                FileName = "dotnet",
                Arguments = args.ToList(),
                WorkingDirectory = WorkingDirectory
            };
            return sdkCommandSpec;
        }
    }
}
