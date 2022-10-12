// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;

namespace Microsoft.TemplateEngine.Authoring.TemplateVerifier.Commands
{
    internal class DotnetCommand : TestCommand
    {
        private readonly string _commandName;

        public DotnetCommand(ILogger log, string commandName, params string[] args) : base(log)
        {
            Arguments.Add(commandName);
            Arguments.AddRange(args);
            _commandName = commandName;
        }

        public DotnetCommand WithCustomHive(string? path = null)
        {
            path ??= CreateTemporaryFolder();
            Arguments.Add("--debug:custom-hive");
            Arguments.Add(path);
            return this;
        }

        protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
        {
            var sdkCommandSpec = new SdkCommandSpec()
            {
                FileName = "dotnet",
                Arguments = args.ToList(),
                WorkingDirectory = WorkingDirectory
            };
            return sdkCommandSpec;
        }

        private static string CreateTemporaryFolder(string name = "")
        {
            string workingDir = Path.Combine(Path.GetTempPath(), "TemplateEngine.Tests", Guid.NewGuid().ToString(), name);
            Directory.CreateDirectory(workingDir);
            return workingDir;
        }
    }
}
