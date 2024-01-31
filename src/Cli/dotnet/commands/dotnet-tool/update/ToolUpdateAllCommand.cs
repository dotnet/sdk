// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Tools.Tool.Install;

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
            return 0;
        }
    }
}
