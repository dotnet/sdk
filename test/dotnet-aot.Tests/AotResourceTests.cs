// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Commands;

namespace Microsoft.DotNet.Cli.Tests;

[TestClass]
public class AotResourceTests
{
    [TestMethod]
    public void NativeClosureDoesNotEmbedTaskDiagnostics()
    {
        foreach (string name in typeof(NativeEntryPoint).Assembly.GetManifestResourceNames())
        {
            Assert.IsFalse(name.StartsWith("Microsoft.NET.Build.Tasks.Strings", StringComparison.Ordinal), name);
        }
    }

    [TestMethod]
    [DataRow("en")]
    [DataRow("fr")]
    [DataRow("ja")]
    [DataRow("zh-Hans")]
    public void FilteredResourcesPreserveCultureFallback(string culture)
    {
        string? message = CliCommandStrings.ResourceManager.GetString(
            "RunCommandExceptionNoProjects", CultureInfo.GetCultureInfo(culture));
        Assert.IsFalse(string.IsNullOrEmpty(message));
    }
}
