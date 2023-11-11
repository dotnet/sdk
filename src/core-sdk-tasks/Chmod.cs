// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Cli.Build
{
    public class Chmod : ToolTask
    {
        [Required]
        public string Glob { get; set; }

        [Required]
        public string Mode { get; set; }

        public bool Recursive { get; set; }

        protected override string ToolName => "chmod";

        protected override MessageImportance StandardOutputLoggingImportance => MessageImportance.High;

        protected override string GenerateFullPathToTool() => "chmod";

        protected override string GenerateCommandLineCommands() => $"{GetRecursive()} {GetMode()} {GetGlob()}";

        private string GetGlob() => Glob;

        private string GetMode() => Mode;

        private string GetRecursive() => Recursive ? "--recursive" : null;
    }
}
