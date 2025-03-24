// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TestUtilities;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.Tests;

public class SourceBuiltArtifactsTests : SdkTests
{
    public static bool IncludeSourceBuiltArtifactsTests => !string.IsNullOrWhiteSpace(Config.SourceBuiltArtifactsPath);

    public SourceBuiltArtifactsTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

    [ConditionalFact(typeof(SourceBuiltArtifactsTests), nameof(IncludeSourceBuiltArtifactsTests))]
    public void VerifyVersionFile()
    {
        Assert.NotNull(Config.SourceBuiltArtifactsPath);

        string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "sourcebuilt-artifacts");
        Directory.CreateDirectory(outputDir);
        try
        {
            // Extract the .version file
            Utilities.ExtractTarball(Config.SourceBuiltArtifactsPath, outputDir, ".version");

            string[] versionLines = File.ReadAllLines(Path.Combine(outputDir, ".version"));
            Assert.Equal(2, versionLines.Length);

            // Verify the commit SHA

            string commitSha = versionLines[0];
            OutputHelper.WriteLine($"Commit SHA: {commitSha}");
            Assert.Equal(40, commitSha.Length);
            Assert.True(commitSha.All(c => char.IsLetterOrDigit(c)));

            // When running in CI, we should ensure that the commit SHA is not all zeros, which is the default
            // value when no commit SHA is available. In a dev environment this will likely be all zeros but it's
            // possible that it could be a valid commit SHA depending on the environment's configuration, so we
            // only verify this in CI.
            if (Config.RunningInCI)
            {
                Assert.False(commitSha.All(c => c == '0'));
            }

            // Verify the SDK version

            string sdkVersion = versionLines[1];

            // Find the expected SDK version by getting it from the source built SDK
            DirectoryInfo sdkDir = new DirectoryInfo(Path.Combine(Config.DotNetDirectory, "sdk"));
            string sdkVersionPath = sdkDir.GetFiles(".version", SearchOption.AllDirectories).Single().FullName;
            string[] sdkVersionLines = File.ReadAllLines(Path.Combine(outputDir, sdkVersionPath));
            string expectedSdkVersion = sdkVersionLines[3];  // Get the unique, non-stable, SDK version

            Assert.Equal(expectedSdkVersion, sdkVersion);
        }
        finally
        {
            Directory.Delete(outputDir, recursive: true);
        }
    }
}
