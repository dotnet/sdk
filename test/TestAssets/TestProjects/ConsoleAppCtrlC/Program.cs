// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Diagnostics;
using System.Threading;

namespace ConsoleAppCtrlC;

class Program
{
    static void Main(string[] args)
    {
        Console.CancelKeyPress += (sender, e) =>
        {
            e.Cancel = true;
            Environment.Exit(42);
        };

        Console.WriteLine(Process.GetCurrentProcess().Id);
        Console.WriteLine("Started");

        Thread.Sleep(Timeout.Infinite);
    }
}
