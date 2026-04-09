// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace ConsoleAppDoesNothing;

public class Program
{
    /// <summary>
    /// A console application that does "nothing" - just exits without any handshake or testing platform interaction.
    /// This is used to test dotnet test behavior with non-MTP console applications.
    /// </summary>
    public static void Main(string[] args)
    {
        // Intentionally does nothing - no handshake, no test framework, just exits
        // This simulates a regular console app that doesn't implement MTP protocol
    }
}