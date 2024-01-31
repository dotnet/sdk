// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.DotNet.Tools.Tool.List;

namespace Microsoft.DotNet.Tools.Tool.Update
{
    internal class ToolUpdateAllCommand : CommandBase
    {
        public ToolUpdateAllCommand(ParseResult parseResult)
            : base(parseResult)
        {

        }

        public override int Execute()
        {
            // Get the list of tools
            var toolListCommand = new ToolListCommand(_parseResult);
            var toolList = toolListCommand.Execute();

            // Parse result



            // For each tool, call the update command
            return 0;
        }
    }
}
