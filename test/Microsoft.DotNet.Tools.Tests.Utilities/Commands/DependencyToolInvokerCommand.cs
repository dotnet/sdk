// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public class DependencyToolInvokerCommand : DotnetCommand
    {
        public DependencyToolInvokerCommand() : base()
        {
        }

        public DependencyToolInvokerCommand(string dotnetUnderTest) : base(dotnetUnderTest)
        {
        }

        public CommandResult Execute(string commandName, string framework, string additionalArgs)
        {
            var args = $"dependency-tool-invoker {commandName} --framework {framework} {additionalArgs}";
            return base.Execute(args);
        }

        public CommandResult ExecuteWithCapturedOutput(string commandName, string framework, string additionalArgs)
        {
            var args = $"dependency-tool-invoker {commandName} --framework {framework} {additionalArgs}";
            return base.ExecuteWithCapturedOutput(args);
        }
    }
}
