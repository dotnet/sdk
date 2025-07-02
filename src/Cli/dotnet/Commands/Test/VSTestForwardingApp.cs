// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Reflection;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Commands.Test;

public class VSTestForwardingApp : ForwardingApp
{
    private const string VstestAppName = "vstest.console.dll";

    public VSTestForwardingApp(IEnumerable<string> argsToForward, string targetArchitecture)
        : base(GetVSTestExePath(), argsToForward)
    {
        (bool setRootVariable, string rootVariableName, string rootValue) = GetRootVariable(targetArchitecture);
        if (!setRootVariable)
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

    internal static (bool setRootVariable, string rootVariableName, string rootValue) GetRootVariable(string targetArchitecture)
    {
        string[] rootVariables = [];
        var processArchitecture = RuntimeInformation.ProcessArchitecture;
        if (string.IsNullOrWhiteSpace(targetArchitecture) || processArchitecture != Enum.Parse<Architecture>(targetArchitecture, ignoreCase: true))
        {
            // User specified the --arch parameter but it is different from current process architecture, so we won't set anything
            // to not break child processes by setting DOTNET_ROOT on them;
            return (setRootVariable: false, null, null);
        }

        // Get variables to pick up from the current environment in the order in which they are inspected by the process.
        rootVariables = GetRootVariablesToInspect(processArchitecture);

        string rootPath = null;
        foreach (var variable in rootVariables)
        {
            rootPath = Environment.GetEnvironmentVariable(variable);
            if (!string.IsNullOrWhiteSpace(rootPath))
            {
                break;
            }
        }

        if (!string.IsNullOrWhiteSpace(rootPath))
        {
            // Root path for this process was already set externally don't do anything.
            return (setRootVariable: false, null, null);
        }

        // VSTest only accepts the two variants below, and the apphost does fallback to DOTNET_ROOT in all architectures, so we pass the
        // architecture non-specific env variable.
        string rootVariableName = Environment.Is64BitProcess ? "VSTEST_WINAPPHOST_DOTNET_ROOT" : "VSTEST_WINAPPHOST_DOTNET_ROOT(x86)";

        // We rename env variable to support --arch switch that relies on DOTNET_ROOT/DOTNET_ROOT(x86)
        // We provide VSTEST_WINAPPHOST_ only in case of testhost*.exe removing VSTEST_WINAPPHOST_ prefix and passing as env vars.
        return (setRootVariable: true, rootVariableName, rootPath);

        static string[] GetRootVariablesToInspect(Architecture processArchitecture)
        {
            switch (processArchitecture)
            {
                case Architecture.X86:
                    return ["DOTNET_ROOT_X86", "DOTNET_ROOT(x86)"];

                case Architecture.X64:
                    return ["DOTNET_ROOT_X64", "DOTNET_ROOT"];

                default:
                    return [$"DOTNET_ROOT_{processArchitecture.ToString().ToUpperInvariant()}"];
            }
        }
    }
}
