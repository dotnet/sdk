// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Tools.Test.Utilities
{
    public sealed class ToolCommand : DotnetCommand
    {
        public override CommandResult Execute(string args = "") => base.Execute($"tool {args}");

        public override CommandResult ExecuteWithCapturedOutput(string args = "") => base.ExecuteWithCapturedOutput($"tool {args}");
    }
}
