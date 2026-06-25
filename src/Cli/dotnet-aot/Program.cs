// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli;

/// <summary>
///  Managed entry point for the NativeAOT <c>dotnet</c> CLI. This mirrors the managed CLI's
///  <see cref="Program"/> in <c>src/Cli/dotnet/Program.cs</c>, but builds and invokes the shared
///  <see cref="Parser"/> directly: the heavy first-run/telemetry/signal setup that the managed
///  entry point performs in its static constructor is instead driven from the native bridge
///  (<see cref="NativeEntryPoint"/>) so the same work happens regardless of how the binary is hosted.
///  Keeping this <c>Main</c> in its own type lets <c>dotnet-aot.csproj</c> compile without linking
///  the managed <c>Program.cs</c>, so the two entry points stay structurally parallel but decoupled.
/// </summary>
public class Program
{
    public static int Main(string[] args)
    {
        var parseResult = Parser.Parse(args);
        return Parser.Invoke(parseResult);
    }
}
