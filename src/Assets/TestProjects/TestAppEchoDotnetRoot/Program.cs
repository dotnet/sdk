﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;

namespace ConsoleApplication
{
    public class Program
    {
        public static void Main(string[] args)
        {
            Console.WriteLine($"DOTNET_ROOT='{Environment.GetEnvironmentVariable("DOTNET_ROOT", EnvironmentVariableTarget.Process)}';" +
                $"DOTNET_ROOT(x86)='{Environment.GetEnvironmentVariable("DOTNET_ROOT(x86)", EnvironmentVariableTarget.Process)}'");
        }
    }
}
