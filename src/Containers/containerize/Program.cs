// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.CommandLine.Parsing;

namespace containerize;

internal class Program
{
    private static Task<int> Main(string[] args)
    {
        return new ContainerizeCommand().InvokeAsync(args);
    }
}
