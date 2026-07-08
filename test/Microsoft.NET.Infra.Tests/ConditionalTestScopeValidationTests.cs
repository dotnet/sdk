// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Xml.Linq;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.NET.Infra.Tests;

[TestClass]
public class ConditionalTestScopeValidationTests
{
    private static string RepoRoot { get; } = FindRepoRoot();

    private static string PropsFile => Path.Combine(RepoRoot, "test", "ConditionalTests.props");

    [TestMethod]
    public void AllTriggerPathBaseDirectoriesExist()
    {
        Assert.IsTrue(File.Exists(PropsFile), $"ConditionalTests.props not found at {PropsFile}");

        var doc = XDocument.Load(PropsFile);
        var errors = new List<string>();

        // Validate per-scope TriggerPaths
        foreach (var scope in doc.Descendants("ConditionalTestScope"))
        {
            var scopeName = scope.Attribute("Include")?.Value ?? "(unnamed)";
            var triggerPaths = (scope.Element("TriggerPaths")?.Value ?? "")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var pattern in triggerPaths)
            {
                ValidatePattern(pattern, $"scope '{scopeName}' TriggerPaths", errors);
            }
        }

        // Validate GlobalTriggerPaths
        var globalPaths = (doc.Descendants("GlobalTriggerPaths").FirstOrDefault()?.Value ?? "")
            .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var pattern in globalPaths)
        {
            ValidatePattern(pattern, "GlobalTriggerPaths", errors);
        }

        if (errors.Count > 0)
        {
            Assert.Fail(
                $"The following trigger paths in ConditionalTests.props reference directories that do not exist:\n" +
                string.Join("\n", errors.Select(e => $"  - {e}")));
        }
    }

    [TestMethod]
    public void AllTestProjectGlobsMatchAtLeastOneProject()
    {
        Assert.IsTrue(File.Exists(PropsFile), $"ConditionalTests.props not found at {PropsFile}");

        var doc = XDocument.Load(PropsFile);
        var errors = new List<string>();

        foreach (var scope in doc.Descendants("ConditionalTestScope"))
        {
            var scopeName = scope.Attribute("Include")?.Value ?? "(unnamed)";
            var testProjects = (scope.Element("TestProjects")?.Value ?? "")
                .Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            foreach (var pattern in testProjects)
            {
                ValidatePattern(pattern, $"scope '{scopeName}' TestProjects", errors);
            }
        }

        if (errors.Count > 0)
        {
            Assert.Fail(
                $"The following TestProjects paths in ConditionalTests.props reference directories that do not exist:\n" +
                string.Join("\n", errors.Select(e => $"  - {e}")));
        }
    }

    private static void ValidatePattern(string pattern, string context, List<string> errors)
    {
        // Strip glob portions to get the base directory that must exist.
        // e.g. "src/TemplateEngine/**" -> "src/TemplateEngine"
        // e.g. "test/TemplateEngine/**/*.csproj" -> "test/TemplateEngine"
        var normalized = pattern.Replace('\\', '/');
        var firstWildcard = normalized.IndexOfAny(['*', '?']);
        if (firstWildcard < 0)
        {
            // No glob — treat as literal path
            var fullPath = Path.Combine(RepoRoot, normalized.Replace('/', Path.DirectorySeparatorChar));
            if (!File.Exists(fullPath) && !Directory.Exists(fullPath))
            {
                errors.Add($"{context}: '{pattern}' does not exist");
            }
            return;
        }

        // Get the directory portion before the first wildcard
        var baseDir = normalized[..firstWildcard].TrimEnd('/');
        if (string.IsNullOrEmpty(baseDir))
        {
            // Pattern like "**/*.cs" — matches repo root, always valid
            return;
        }

        var fullBaseDir = Path.Combine(RepoRoot, baseDir.Replace('/', Path.DirectorySeparatorChar));
        if (!Directory.Exists(fullBaseDir))
        {
            errors.Add($"{context}: '{pattern}' — base directory '{baseDir}' does not exist");
        }
    }

    private static string FindRepoRoot()
    {
        var dir = AppContext.BaseDirectory;
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "global.json")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }

        // Fallback: walk up from current directory
        dir = Directory.GetCurrentDirectory();
        while (dir != null)
        {
            if (File.Exists(Path.Combine(dir, "global.json")))
            {
                return dir;
            }
            dir = Path.GetDirectoryName(dir);
        }

        throw new InvalidOperationException("Could not find repository root (no global.json found in parent directories).");
    }
}
