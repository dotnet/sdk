// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

class Program
{
    static void Main(string[] args)
    {
        // Print arguments
        Console.WriteLine($"Arguments: [{string.Join(", ", args)}]");
        
        // Print specific environment variables
        var testVar = Environment.GetEnvironmentVariable("TEST_ENV_VAR");
        if (testVar != null)
        {
            Console.WriteLine($"TEST_ENV_VAR={testVar}");
        }
        
        var customVar = Environment.GetEnvironmentVariable("CUSTOM_VAR");
        if (customVar != null)
        {
            Console.WriteLine($"CUSTOM_VAR={customVar}");
        }
        
        // Print working directory
        Console.WriteLine($"WorkingDirectory={Environment.CurrentDirectory}");
    }
}
