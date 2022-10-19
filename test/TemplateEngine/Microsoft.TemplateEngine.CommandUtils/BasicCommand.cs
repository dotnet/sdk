// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.CommandUtils
{
    public class BasicCommand : TestCommand
    {
        private readonly string _processName;

        public BasicCommand(ITestOutputHelper log, string processName, params string[] args) : base(log)
        {
            _processName = processName;
            Arguments.AddRange(args.Where(a => !string.IsNullOrWhiteSpace(a)));
        }

        public BasicCommand(ILogger log, string processName, params string[] args) : base(log)
        {
            _processName = processName;
            Arguments.AddRange(args.Where(a => !string.IsNullOrWhiteSpace(a)));
        }

        private protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
        {
            var sdkCommandSpec = new SdkCommandSpec()
            {
                FileName = _processName,
                Arguments = args.ToList(),
                WorkingDirectory = WorkingDirectory
            };
            return sdkCommandSpec;
        }
    }
}
