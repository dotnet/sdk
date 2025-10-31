// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Linq;

namespace Microsoft.DotNet.Tools.Bootstrapper;

// Copy of DebugHelper.cs in the SDK - port eventually.
internal static class DnupDebugHelper
{
    [Conditional("DEBUG")]
    public static void HandleDebugSwitch(ref string[] args)
    {
        if (args.Length > 0 && string.Equals("--debug", args[0], StringComparison.OrdinalIgnoreCase))
        {
            args = [.. args.Skip(1)];
            WaitForDebugger();
        }
    }

    public static void WaitForDebugger()
    {
        int processId = Environment.ProcessId;

        Console.WriteLine("Waiting for debugger to attach. Press ENTER to continue");
        Console.WriteLine($"Process ID: {processId}");
        Console.ReadLine();
    }
}
