using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
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
