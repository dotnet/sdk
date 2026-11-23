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
            // Ask the host BCL before anchoring: Framework's two path modes and .NET reject
            // different whitespace. Discard the result so accepted names remain project-relative.
            try
            {
                _ = Path.GetFullPath(path);
            }
            catch (ArgumentException)
            {
                // Preserve the caller's exception and side-effect ordering.
                return path;
            }
        }

        return environment.GetAbsolutePath(path);
    }
}
