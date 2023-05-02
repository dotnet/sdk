﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli;
using System.CommandLine;
using System.CommandLine.Parsing;

namespace Microsoft.DotNet.Tools.Fsi
{
    public class FsiCommand
    {
        public static int Run(string[] args)
        {
            DebugHelper.HandleDebugSwitch(ref args);
            return new FsiForwardingApp(args).Execute();
        }
    }
}
