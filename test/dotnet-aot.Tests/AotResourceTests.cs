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
    [DataRow("cs")]
    [DataRow("de")]
    [DataRow("es")]
    [DataRow("fr")]
    [DataRow("it")]
    [DataRow("ja")]
    [DataRow("ko")]
    [DataRow("pl")]
    [DataRow("pt-BR")]
    [DataRow("ru")]
    [DataRow("tr")]
    [DataRow("zh-Hans")]
    [DataRow("zh-Hant")]
    public void StaticSubsetsPreserveTranslationsAndExcludeUnusedKeys(string culture)
    {
        var resources = CliCommandStrings.ResourceManager.GetResourceSet(
            CultureInfo.GetCultureInfo(culture), createIfNotExists: true, tryParents: false);
        Assert.IsNotNull(resources);
        string? translated = resources.GetString("RunCommandExceptionNoProjects");
        Assert.IsFalse(string.IsNullOrEmpty(translated));
        Assert.AreNotEqual(CliCommandStrings.ResourceManager.GetString(
            "RunCommandExceptionNoProjects", CultureInfo.InvariantCulture), translated);
        Assert.IsNull(resources.GetString("CmdTestCaseFilterExpression"));
        Assert.IsNull(CliCommandStrings.ResourceManager.GetString(
            "CmdTestCaseFilterExpression", CultureInfo.InvariantCulture));
    }
}
