using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

public class SourceBuiltArtifactsTests : SmokeTests
{
    public SourceBuiltArtifactsTests(ITestOutputHelper outputHelper) : base(outputHelper) { }

    [Fact]
    public void VerifyVersionFile()
    {
        string outputDir = Path.Combine(Directory.GetCurrentDirectory(), "sourcebuilt-artifacts");
        Directory.CreateDirectory(outputDir);
        try
        {
            // Extract the .version file
            ExtractFileFromTarball(Config.SourceBuiltArtifactsPath, ".version", outputDir);

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

            // Find the expected SDK version by getting it from the SDK tarball
            ExtractFileFromTarball(Config.SdkTarballPath, "./sdk/*/.version", outputDir);
            DirectoryInfo sdkDir = new DirectoryInfo(Path.Combine(outputDir, "sdk"));
            string sdkVersionPath = sdkDir.GetFiles(".version", SearchOption.AllDirectories).Single().FullName;
            string[] sdkVersionLines = File.ReadAllLines(Path.Combine(outputDir, sdkVersionPath));
            string expectedSdkVersion = sdkVersionLines[1];

            Assert.Equal(expectedSdkVersion, sdkVersion);
        }
        finally
        {
            Directory.Delete(outputDir, recursive: true);
        }
    }

    private void ExtractFileFromTarball(string tarballPath, string filePath, string outputDir)
    {
        ExecuteHelper.ExecuteProcessValidateExitCode("tar", $"--wildcards -xzf {tarballPath} -C {outputDir} {filePath}", OutputHelper);
    }
}
