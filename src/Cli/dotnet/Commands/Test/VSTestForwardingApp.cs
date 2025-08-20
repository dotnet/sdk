// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Test;

public class VSTestForwardingApp : ForwardingApp
{
    private const string VstestAppName = "vstest.console.dll";

    public VSTestForwardingApp(IEnumerable<string> argsToForward)
        : base(GetVSTestExePath(), argsToForward)
    {
        Dictionary<string, string> variables = GetVSTestRootVariables();
        foreach (var (rootVariableName, rootValue) in variables)
        {
            WithEnvironmentVariable(rootVariableName, rootValue);
            VSTestTrace.SafeWriteTrace(() => $"Root variable set {rootVariableName}:{rootValue}");
        }
        
        VSTestTrace.SafeWriteTrace(() => $"Forwarding to '{GetVSTestExePath()}' with args \"{argsToForward?.Aggregate((a, b) => $"{a} | {b}")}\"");
    }

    private static string GetVSTestExePath()
    {
        // Provide custom path to vstest.console.dll or exe to be able to test it against any version of 
        // vstest.console. This is useful especially for our integration tests.
        // This is equivalent to specifying -p:VSTestConsolePath when using dotnet test with csproj.
        string vsTestConsolePath = Environment.GetEnvironmentVariable("VSTEST_CONSOLE_PATH");
        if (!string.IsNullOrWhiteSpace(vsTestConsolePath))
        {
            return vsTestConsolePath;
        }

        return Path.Combine(AppContext.BaseDirectory, VstestAppName);
    }

    internal static Dictionary<string, string> GetVSTestRootVariables()
    {
        // Gather the current .NET SDK dotnet.exe location and forward it to vstest.console.dll so it can use it
        // to setup DOTNET_ROOT for testhost.exe, to find the same installation of NET SDK that is running `dotnet test`.
        // This way if we have private installation of .NET SDK, the testhost.exe will be able to use the same private installation.
        // The way to set the environment is complicated and depends on the version of testhost, so we leave that implementation to vstest console,
        // we just tell it where the current .net SDK is located, and what is the architecture of it. We don't have more information than that here.
        return new()
        {
            ["VSTEST_DOTNET_ROOT_PATH"] = Path.GetDirectoryName(new Muxer().MuxerPath),
            ["VSTEST_DOTNET_ROOT_ARCHITECTURE"] = RuntimeInformation.ProcessArchitecture.ToString()
        };
    }
}
