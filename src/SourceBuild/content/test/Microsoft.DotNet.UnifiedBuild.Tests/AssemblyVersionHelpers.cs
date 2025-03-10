// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Reflection;

namespace Microsoft.DotNet.UnifiedBuild.Tests;

public static class AssemblyVersionHelpers
{
    public static void WriteAssemblyVersionsToFile(Dictionary<string, Version?> assemblyVersions, string outputPath)
    {
        string[] lines = assemblyVersions
            .Select(kvp => $"{kvp.Key} - {kvp.Value}")
            .Order()
            .ToArray();
        File.WriteAllLines(outputPath, lines);
    }

    // It's known that assembly versions can be different between builds in their revision field. Disregard that difference
    // by excluding that field in the output.
    public static Version? GetVersion(AssemblyName assemblyName)
    {
        if (assemblyName.Version is not null)
        {
            return new Version(assemblyName.Version.ToString(3));
        }

        return null;
    }
}
