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

            // Global 
            var toolListCommand = new ToolListGlobalOrToolPathCommand(_parseResult);
            var toolList = toolListCommand.GetPackages(null, null);

            // local
            var toolListLocalCommand = new ToolListLocalCommand(_parseResult);
            var toolListLocal = toolListLocalCommand.GetPackages(null);

            // For each global tool, call the update command
            foreach (var tool in toolList)
            {
                var toolUpdateCommand = new ToolUpdateCommand(_parseResult);
                toolUpdateCommand.Execute();
            }

            // For each local tool, call the update command
            foreach (var tool in toolListLocal)
            {
                var toolUpdateCommand = new ToolUpdateCommand(_parseResult);
                toolUpdateCommand.Execute();
            }   
            return 0;
        }
    }
}
