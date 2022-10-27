// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.CommandUtils
{
    internal class DotnetNewCommand : DotnetCommand
    {
        private bool _hiveSet;

        internal DotnetNewCommand(ILogger log, params string[] args) : base(log, "new", args)
        {
        }

        internal DotnetNewCommand(ITestOutputHelper log, params string[] args) : base(log, "new", args)
        {
        }

        internal DotnetNewCommand WithVirtualHive()
        {
            Arguments.Add("--debug:ephemeral-hive");
            _hiveSet = true;
            return this;
        }

        internal DotnetNewCommand WithCustomHive(string path)
        {
            Arguments.Add("--debug:custom-hive");
            Arguments.Add(path);
            _hiveSet = true;
            return this;
        }

        internal DotnetNewCommand WithoutCustomHive()
        {
            _hiveSet = true;
            return this;
        }

        internal DotnetNewCommand WithoutBuiltInTemplates()
        {
            Arguments.Add("--debug:disable-sdk-templates");
            return this;
        }

        internal DotnetNewCommand WithDebug()
        {
            WithEnvironmentVariable("DOTNET_CLI_CONTEXT_VERBOSE", "true");
            return this;
        }

        private protected override SdkCommandSpec CreateCommand(IEnumerable<string> args)
        {
            if (!_hiveSet)
            {
                throw new Exception($"\"--debug:custom-hive\" is not set, call {nameof(WithCustomHive)} to set it or {nameof(WithoutCustomHive)} if it is intentional.");
            }

            return base.CreateCommand(args);
        }
    }
}
