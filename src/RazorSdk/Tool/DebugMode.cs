﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace Microsoft.NET.Sdk.Razor.Tool
{
    internal static class DebugMode
    {
        public static void HandleDebugSwitch(ref string[] args)
        {
            if (args.Length > 0 && string.Equals("--debug", args[0], StringComparison.OrdinalIgnoreCase))
            {
                args = args.Skip(1).ToArray();

                Console.WriteLine("Waiting for debugger in pid: {0}", Environment.ProcessId);
                while (!Debugger.IsAttached)
                {
                    Thread.Sleep(TimeSpan.FromSeconds(3));
                }
            }
        }
    }
}
