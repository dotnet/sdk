// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Linq;

namespace MSBuildTestApp
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine($"DOTNET_LAUNCH_PROFILE=<<<{Environment.GetEnvironmentVariable("DOTNET_LAUNCH_PROFILE")}>>>");
            Console.WriteLine($"TEST_VAR1=<<<{Environment.GetEnvironmentVariable("TEST_VAR1")}>>>");
            Console.WriteLine($"TEST_VAR2=<<<{Environment.GetEnvironmentVariable("TEST_VAR2")}>>>");
            Console.WriteLine($"TEST_VAR3=<<<{Environment.GetEnvironmentVariable("TEST_VAR3")}>>>");
            Console.WriteLine($"ARGS={string.Join(",", args)}");
        }
    }
}
