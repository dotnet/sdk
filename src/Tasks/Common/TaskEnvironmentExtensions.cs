// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.InteropServices;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Tasks;

internal static class TaskEnvironmentExtensions
{
    public static string? GetAbsolutePathPreservingInvalidArguments(this TaskEnvironment environment, string? path)
    {
        if (string.IsNullOrEmpty(path))
        {
            return path;
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && string.IsNullOrWhiteSpace(path))
        {
            // A Framework task assembly can run on .NET; use the host's BCL behavior.
            bool frameworkModernHandling =
                RuntimeInformation.FrameworkDescription.StartsWith(".NET Framework", StringComparison.Ordinal) &&
                (!AppContext.TryGetSwitch("Switch.System.IO.UseLegacyPathHandling", out bool useLegacy) || !useLegacy);
            if (!frameworkModernHandling)
            {
                return path;
            }
        }

        return environment.GetAbsolutePath(path);
    }
}
