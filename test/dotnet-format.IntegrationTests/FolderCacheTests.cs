// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Format.IntegrationTests;

[TestClass]
public class FolderCacheTests : SdkTest
{
    [TestMethod]
    public void ColdAndWarmCacheMatchUncachedFormattingAfterFileAndConfigurationEdits()
    {
        var root = TestAssetsManager.CreateTestDirectory().Path;
        var folder = Directory.CreateDirectory(Path.Combine(root, "workspace")).FullName;
        var reportPath = Path.Combine(root, "report.json");
        var configPath = Path.Combine(folder, ".editorconfig");
        var firstPath = Path.Combine(folder, "First.cs");
        var secondPath = Path.Combine(folder, "Second.cs");
        var generatedPath = Path.Combine(folder, "Generated.g.cs");
        var excludedPath = Path.Combine(folder, "Excluded.cs");
        var original = "class C\r\n{\r\n int x=1;\r\n}";
        File.WriteAllText(configPath, "root = true\n[*.cs]\nindent_style = space\nindent_size = 4\nend_of_line = lf\ninsert_final_newline = true\ncharset = utf-8\n");
        foreach (var path in new[] { firstPath, secondPath, generatedPath, excludedPath })
        {
            File.WriteAllText(path, original, new UTF8Encoding(false));
        }

        var workspaceHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(folder)));
        var cachePath = Path.Combine(Path.GetTempPath(), "dotnet-format", workspaceHash + ".json");
        File.Delete(cachePath);
        try
        {
            VerifyMatchesUncached(expectedExitCode: 2);
            Assert.IsFalse(File.Exists(cachePath), "Verification must not populate the cache.");
            SaveMatchesUncached();
            Assert.AreEqual("class C\n{\n    int x = 1;\n}\n", File.ReadAllText(firstPath));
            Assert.IsTrue(File.Exists(cachePath), "Write mode must populate the folder cache.");
            var cacheBytes = File.ReadAllBytes(cachePath);
            VerifyMatchesUncached(expectedExitCode: 0);
            File.ReadAllBytes(cachePath).Should().Equal(cacheBytes);

            var unchangedSecond = File.ReadAllBytes(secondPath);
            File.WriteAllText(firstPath, original.Replace("x=1", "x=3"), new UTF8Encoding(false));
            VerifyMatchesUncached(expectedExitCode: 2);
            File.ReadAllBytes(cachePath).Should().Equal(cacheBytes);
            SaveMatchesUncached();
            File.ReadAllBytes(secondPath).Should().Equal(unchangedSecond);
            Assert.Contains("x = 3;", File.ReadAllText(firstPath));

            File.WriteAllText(configPath, "root = true\n[*.cs]\nindent_style = space\nindent_size = 2\nend_of_line = crlf\ninsert_final_newline = true\ncharset = utf-8-bom\n");
            VerifyMatchesUncached(expectedExitCode: 2);
            SaveMatchesUncached();
            Assert.AreEqual("class C\r\n{\r\n  int x = 3;\r\n}\r\n", File.ReadAllText(firstPath));
            Assert.IsTrue(File.ReadAllBytes(firstPath).AsSpan().StartsWith(Encoding.UTF8.GetPreamble()));
            VerifyMatchesUncached(expectedExitCode: 0);

            Assert.AreEqual(original, File.ReadAllText(generatedPath));
            Assert.AreEqual(original, File.ReadAllText(excludedPath));
        }
        finally
        {
            File.Delete(cachePath);
        }

        (CommandResult Result, string Report, string Diagnostics) Run(bool noCache, bool verify)
        {
            var arguments = new List<string>
            {
                "format", "whitespace", folder, "--folder", "--exclude", "Excluded.cs",
                "--report", reportPath, "--verbosity", "diagnostic",
            };
            if (noCache)
            {
                arguments.Add("--no-cache");
            }
            if (verify)
            {
                arguments.Add("--verify-no-changes");
            }

            File.Delete(reportPath);
            var result = new DotnetCommand(Log, arguments.ToArray()).WithWorkingDirectory(folder).Execute();
            Assert.IsTrue(File.Exists(reportPath), result.StdOut + result.StdErr);
            var report = JsonNode.Parse(File.ReadAllText(reportPath))!.AsArray();
            foreach (var file in report)
            {
                file!.AsObject().Remove("DocumentId");
            }

            var diagnostics = string.Join("\n", (result.StdOut + "\n" + result.StdErr)
                .Split('\n').Where(line => Regex.IsMatch(line, @"\(\d+,\d+\):")));
            return (result, report.ToJsonString(), diagnostics);
        }

        void VerifyMatchesUncached(int expectedExitCode)
        {
            var before = new[] { File.ReadAllBytes(firstPath), File.ReadAllBytes(secondPath) };
            var cached = Run(noCache: false, verify: true);
            var uncached = Run(noCache: true, verify: true);
            Assert.AreEqual(expectedExitCode, cached.Result.ExitCode, cached.Result.StdOut + cached.Result.StdErr);
            Assert.AreEqual(expectedExitCode, uncached.Result.ExitCode, uncached.Result.StdOut + uncached.Result.StdErr);
            Assert.AreEqual(uncached.Report, cached.Report);
            Assert.AreEqual(uncached.Diagnostics, cached.Diagnostics);
            File.ReadAllBytes(firstPath).Should().Equal(before[0]);
            File.ReadAllBytes(secondPath).Should().Equal(before[1]);
        }

        void SaveMatchesUncached()
        {
            var paths = new[] { firstPath, secondPath };
            var before = paths.Select(File.ReadAllBytes).ToArray();
            var cached = Run(noCache: false, verify: false);
            Assert.AreEqual(0, cached.Result.ExitCode, cached.Result.StdOut + cached.Result.StdErr);
            var expected = paths.Select(File.ReadAllBytes).ToArray();
            for (var i = 0; i < paths.Length; i++)
            {
                File.WriteAllBytes(paths[i], before[i]);
            }

            var uncached = Run(noCache: true, verify: false);
            Assert.AreEqual(0, uncached.Result.ExitCode, uncached.Result.StdOut + uncached.Result.StdErr);
            Assert.AreEqual(uncached.Report, cached.Report);
            Assert.AreEqual(uncached.Diagnostics, cached.Diagnostics);
            for (var i = 0; i < paths.Length; i++)
            {
                File.ReadAllBytes(paths[i]).Should().Equal(expected[i]);
            }
        }
    }
}
