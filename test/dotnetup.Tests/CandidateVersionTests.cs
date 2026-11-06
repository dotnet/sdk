// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class CandidateVersionTests : SdkTest
{
    [TestMethod]
    [DataRow("dotnetup", "0")]
    [DataRow("dotnetup", "1")]
    [DataRow("dotnetup", "2")]
    [DataRow("dotnetup", "10")]
    [DataRow("dotnetup.Library", "0")]
    [DataRow("dotnetup.Library", "1")]
    [DataRow("dotnetup.Library", "2")]
    [DataRow("dotnetup.Library", "10")]
    [DataRow("Microsoft.Dotnet.Installation", "0")]
    [DataRow("Microsoft.Dotnet.Installation", "1")]
    [DataRow("Microsoft.Dotnet.Installation", "2")]
    [DataRow("Microsoft.Dotnet.Installation", "10")]
    public void CandidatesUseTheActualQualitySpecificVersionProperties(string projectName, string previewIteration)
    {
        string? repoRoot = SdkTestContext.Current.ToolsetUnderTest.RepoRoot
            ?? SdkTestContext.GetRepoRoot();
        if (repoRoot is null || !File.Exists(Path.Combine(repoRoot, "src", "Installer", "Directory.Build.props")))
        {
            Assert.Inconclusive("Candidate version evaluation requires the source checkout, which is not deployed to Helix.");
            return;
        }

        string projectPath = Path.Combine(repoRoot, "src", "Installer", projectName, projectName + ".csproj");
        foreach (string quality in new[] { "daily", "preview" })
        {
            var command = new DotnetCommand(Log,
                "msbuild", projectPath,
                "/t:GetAssemblyVersion,AddSourceRevisionToInformationalVersion",
                "/p:OfficialBuildId=20261008.2",
                $"/p:DotnetupPreReleaseVersionLabel={quality}",
                $"/p:DotnetupPreReleaseVersionIteration={previewIteration}",
                "-getProperty:VersionPrefix,Version,PackageVersion,InformationalVersion,PreReleaseVersionIteration",
                BinLogArgument([projectName, quality, previewIteration]))
            {
                WorkingDirectory = repoRoot
            };

            var result = command.Execute();
            Assert.AreEqual(0, result.ExitCode, result.StdOut + result.StdErr);
            Assert.IsNotNull(result.StdOut);
            using JsonDocument document = JsonDocument.Parse(result.StdOut);
            JsonElement properties = document.RootElement.GetProperty("Properties");
            string iteration = quality == "daily" ? "" : previewIteration + ".";
            string expectedVersion = $"{properties.GetProperty("VersionPrefix").GetString()}-{quality}.{iteration}26508.2";
            Assert.AreEqual(expectedVersion, properties.GetProperty("Version").GetString());
            Assert.AreEqual(expectedVersion, properties.GetProperty("PackageVersion").GetString());
            Assert.StartsWith(expectedVersion + "+", properties.GetProperty("InformationalVersion").GetString());
            Assert.AreEqual(quality == "daily" ? "" : previewIteration,
                properties.GetProperty("PreReleaseVersionIteration").GetString());
        }
    }
}
