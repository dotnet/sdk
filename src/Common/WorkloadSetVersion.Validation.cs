// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;
using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.Cli;

internal static partial class WorkloadSetVersion
{
    private static string[] SeparateCoreComponents(string workloadSetVersion, out string[] sections)
    {
        sections = workloadSetVersion.Split(['-', '+'], 2);
        if (sections.Length < 1)
        {
            return [];
        }

        return sections[0].Split('.');
    }

    public static bool IsWorkloadSetPackageVersion(string workloadSetVersion)
    {
        int coreComponentsLength = SeparateCoreComponents(workloadSetVersion, out _).Length;
        return coreComponentsLength >= 3 && coreComponentsLength <= 4;
    }
}
