// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using NuGet.RuntimeModel;

namespace Microsoft.DotNet.Cli.Build
{
    public class GenerateSdkRuntimeIdentifierChain : Task
    {
        [Required]
        public string RuntimeIdentifier { get; set; }

        [Required]
        public string RuntimeIdentifierGraphPath { get; set; }

        [Required]
        public string RuntimeIdentifierChainOutputPath { get; set; }

        public override bool Execute()
        {
            var runtimeIdentifierGraph = JsonRuntimeFormat.ReadRuntimeGraph(RuntimeIdentifierGraphPath);

            var chainContents = string.Join("\n", runtimeIdentifierGraph.ExpandRuntime(RuntimeIdentifier));
            File.WriteAllText(RuntimeIdentifierChainOutputPath, chainContents);

            return true;
        }
    }
}
