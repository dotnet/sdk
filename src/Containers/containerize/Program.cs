// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

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
