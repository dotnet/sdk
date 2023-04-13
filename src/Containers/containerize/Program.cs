﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace containerize;

internal class Program
{
    private static Task<int> Main(string[] args)
    {
        try
        {
            return new ContainerizeCommand().InvokeAsync(args);
        }
        catch (Exception e)
        {
            string message = !e.Message.StartsWith("CONTAINER", StringComparison.OrdinalIgnoreCase) ? $"CONTAINER9000: " + e.ToString() : e.ToString();
            Console.WriteLine($"Containerize: error {message}");

            return Task.FromResult(1);
        }
    }
}
