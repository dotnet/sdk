// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using Microsoft.NET.TestFramework;
using Xunit;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

internal static class ToolsetUtils
{
    /// <summary>
    /// Gets path to RuntimeIdentifierGraph.json file.
    /// </summary>
    /// <returns></returns>
    internal static string GetRuntimeGraphFilePath()
    {
        string dotnetRoot = TestContext.Current.ToolsetUnderTest.DotNetRoot;

        DirectoryInfo sdksDir = new(Path.Combine(dotnetRoot, "sdk"));

        var lastWrittenSdk = sdksDir.EnumerateDirectories().OrderByDescending(di => di.LastWriteTime).First();

        return lastWrittenSdk.GetFiles("RuntimeIdentifierGraph.json").Single().FullName;
    }
}
