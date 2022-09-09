// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#if NET
using System.Collections.Generic;
using System.Linq;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.TestHelper.Commands
{
    public class DotnetCommand : TestCommand
    {
        public DotnetCommand(ITestOutputHelper log, string commandName, params string[] args) : base(log)
        {
            Arguments.Add(commandName);
            Arguments.AddRange(args);
        }

        public DotnetCommand WithCustomHive(string? path = null)
        {
            path ??= TestUtils.CreateTemporaryFolder();
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
    }
}
#endif
