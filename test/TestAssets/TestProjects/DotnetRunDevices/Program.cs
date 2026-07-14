// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace DotNetRunDevices
{
    class Program
    {
        static void Main(string[] args)
        {
            Console.WriteLine("Hello from multi-targeted app!");
            Console.WriteLine($"Target Framework: {AppContext.TargetFrameworkName}");
            Console.WriteLine($"Runtime: {System.Runtime.InteropServices.RuntimeInformation.FrameworkDescription}");

            // DeviceInfo class is generated at build time when Device property is set
            Console.WriteLine($"Device: {DeviceInfo.Device}");
            Console.WriteLine($"RuntimeIdentifier: {DeviceInfo.RuntimeIdentifier}");
        }
    }
}
