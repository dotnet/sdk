// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Telemetry;

internal static class TelemetryTestHooks
{
    public static void TryWriteFile(string environmentVariableName, string content)
    {
        var path = Environment.GetEnvironmentVariable(environmentVariableName);
        if (string.IsNullOrWhiteSpace(path))
        {
            return;
        }

        try
        {
            using var writer = new StreamWriter(new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
            writer.Write(content);
        }
        catch
        {
        }
    }
}