// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;
using System.Text.Json;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;
using Microsoft.NET.TestFramework;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>Exercises the metadata host tool through the product's restore and build graph.</summary>
[TestClass]
public class DotnetupMetadataBuildTests : SdkTest
{
    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task RestoredProductBuildsMetadataWithoutNestedRestore(bool crossRid)
    {
        var repoRoot = SdkTestContext.GetRepoRoot();
        Assert.IsNotNull(repoRoot);
        var directory = Directory.CreateDirectory(Path.Combine(repoRoot, "artifacts", "tmp", "metadata-" + Guid.NewGuid().ToString("N")[..8]));
        var logs = Path.Combine(repoRoot, "artifacts", "log", "test-runs", nameof(DotnetupMetadataBuildTests), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(logs);
        try
        {
            var guard = Path.Combine(directory.FullName, "RejectRestore.targets");
            File.WriteAllText(guard, """
                <Project>
                  <Target Name="RejectUnexpectedRestore" BeforeTargets="Restore">
                    <Error Text="A no-restore metadata build must not invoke Restore." />
                  </Target>
                </Project>
                """);
            var project = Path.Combine(repoRoot, "src", "Installer", "Microsoft.Dotnet.Installation", "Microsoft.Dotnet.Installation.csproj");
            var properties = new List<string>
            {
                "-p:ArtifactsDir=" + directory.FullName + Path.DirectorySeparatorChar,
                "-p:Configuration=Debug",
            };
            if (crossRid)
            {
                properties.AddRange([
                    "-p:RuntimeIdentifier=linux-arm64",
                    "-p:PublishAot=true",
                    "-p:SelfContained=true",
                    "-p:_IsPublishing=true",
                    "-p:DotnetupMetadataRid=linux-arm64",
                ]);
            }

            await RunDotnet(repoRoot, logs, "restore", ["restore", project, .. properties]);

            var toolAssets = Path.Combine(directory.FullName, "obj", "Dotnetup.VersionMetadata", "project.assets.json");
            Assert.IsTrue(File.Exists(toolAssets), "The product restore must include the metadata tool.");
            using (var assets = JsonDocument.Parse(File.ReadAllText(toolAssets)))
            {
                Assert.DoesNotContain(target => target.Name.Contains('/'), assets.RootElement.GetProperty("targets").EnumerateObject(),
                    "The metadata tool must be restored as a host library, not for the product RID.");
            }

            await RunDotnet(repoRoot, logs, "build", [
                "build", project, "--no-restore", .. properties,
                "-p:CustomAfterMicrosoftCommonTargets=" + guard,
            ]);

            // Publishing also invokes this target directly when the product has already been built.
            await RunDotnet(repoRoot, logs, "no-build-validation-tool", [
                "msbuild", project, "-t:BuildDotnetupMetadataTool", .. properties,
                "-p:NoBuild=true", "-p:CustomAfterMicrosoftCommonTargets=" + guard,
            ]);

            Assert.IsNotEmpty(Directory.EnumerateFiles(Path.Combine(directory.FullName, "bin", "Dotnetup.VersionMetadata"),
                "Dotnetup.VersionMetadata.dll", SearchOption.AllDirectories));
            Assert.IsEmpty(Directory.EnumerateFiles(Path.Combine(directory.FullName, "bin", "Microsoft.Dotnet.Installation"),
                "Dotnetup.VersionMetadata.dll", SearchOption.AllDirectories),
                "The host task assembly must not be copied into the product output.");
        }
        finally
        {
            TestContext.WriteLine($"Metadata build diagnostics: {logs}");
            directory.Delete(recursive: true);
        }
    }

    private async Task RunDotnet(string repoRoot, string logs, string name, string[] arguments)
    {
        var start = new ProcessStartInfo(SelfUpdateTestFiles.DotnetHostPath)
        {
            WorkingDirectory = repoRoot,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        foreach (var argument in arguments)
        {
            start.ArgumentList.Add(argument);
        }
        start.ArgumentList.Add("-nodeReuse:false");
        start.ArgumentList.Add("-bl:" + Path.Combine(logs, name + ".binlog"));
        start.Environment["MSBUILDDISABLENODEREUSE"] = "1";
        using var process = Process.Start(start);
        Assert.IsNotNull(process);
        var stdout = process.StandardOutput.ReadToEndAsync();
        var stderr = process.StandardError.ReadToEndAsync();
        try
        {
            await process.WaitForExitAsync(TestContext.CancellationToken).WaitAsync(TimeSpan.FromMinutes(3), TestContext.CancellationToken);
            var output = await stdout + await stderr;
            TestContext.WriteLine(output);
            Assert.AreEqual(0, process.ExitCode, output);
        }
        finally
        {
            if (!process.HasExited)
            {
                process.Kill(entireProcessTree: true);
                Assert.IsTrue(process.WaitForExit(10_000), "Metadata build process did not terminate.");
            }
        }
    }
}
