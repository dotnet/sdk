// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.TemplateEngine.Authoring.TemplateVerifier;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.TestHelper.Commands;
using Microsoft.TemplateEngine.Tests;
using Xunit.Abstractions;

namespace Microsoft.TemplateEngine.Authoring.CLI.IntegrationTests
{
    [UsesVerify]
    public class VerifyCommandTests : TestBase
    {
        private readonly ITestOutputHelper _log;

        public VerifyCommandTests(ITestOutputHelper log)
        {
            _log = log;
        }

        [Fact]
        public void VerifyCommandFullDevLoop()
        {
            // dots issue https://github.com/VerifyTests/Verify/issues/658
            string workingDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName().Replace(".", string.Empty));
            string expectationsDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName().Replace(".", string.Empty));
            string templateOutputDir = "path with spaces";

            var cmd = new BasicCommand(
                _log,
                "dotnet",
                Path.GetFullPath("Microsoft.TemplateEngine.Authoring.CLI.dll"),
                "verify",
                "console",
                "--template-args",
                "--use-program-main -o \"" + templateOutputDir + "\"  --no-restore",
                "--verify-std",
                "-o",
                workingDir,
                "--expectations-directory",
                expectationsDir,
                "--disable-diff-tool",
                "--unique-for",
                "architecture",
                "--unique-for",
                "RuntimeAndVersion");

            cmd.Execute()
                .Should()
                .ExitWith((int)TemplateVerificationErrorCode.VerificationFailed)
                .And.HaveStdOutContaining("Verification Failed.");

            // Assert template created
            Directory.Exists(Path.Combine(workingDir, templateOutputDir)).Should().BeTrue();
            File.Exists(Path.Combine(workingDir, templateOutputDir, "console.csproj")).Should().BeTrue();
            File.Exists(Path.Combine(workingDir, templateOutputDir, "Program.cs")).Should().BeTrue();

            // Assert verification files created
            Directory.Exists(expectationsDir).Should().BeTrue();
            Directory.GetDirectories(expectationsDir).Length.Should().Be(2);
            //for simplicity move to the created dir
            expectationsDir = Directory.GetDirectories(expectationsDir).Single(d => d.EndsWith(".received", StringComparison.Ordinal));
            File.Exists(Path.Combine(expectationsDir, templateOutputDir, "console.csproj")).Should().BeTrue();
            File.Exists(Path.Combine(expectationsDir, templateOutputDir, "Program.cs")).Should().BeTrue();
            File.Exists(Path.Combine(expectationsDir, "std-streams", "stdout.txt")).Should().BeTrue();
            File.Exists(Path.Combine(expectationsDir, "std-streams", "stderr.txt")).Should().BeTrue();
            Directory.GetFiles(expectationsDir, "*", SearchOption.AllDirectories).Length.Should().Be(4);
            // .verified files are only created when diff tool is used - that is however turned off in CI
            //File.Exists(Path.Combine(expectationsDir, "console.console.csproj.verified.csproj")).Should().BeTrue();
            //File.Exists(Path.Combine(expectationsDir, "console.Program.cs.verified.cs")).Should().BeTrue();
            //File.Exists(Path.Combine(expectationsDir, "console.StdOut.verified.txt")).Should().BeTrue();
            //File.Exists(Path.Combine(expectationsDir, "console.StdErr.verified.txt")).Should().BeTrue();

            // .verified files are only created when diff tool is used - that is however turned off in CI
            //File.ReadAllText(Path.Combine(expectationsDir, "console.console.csproj.verified.csproj")).Should().BeEmpty();
            //File.ReadAllText(Path.Combine(expectationsDir, "console.Program.cs.verified.cs")).Should().BeEmpty();
            //File.ReadAllText(Path.Combine(expectationsDir, "console.StdOut.verified.txt")).Should().BeEmpty();
            //File.ReadAllText(Path.Combine(expectationsDir, "console.StdErr.verified.txt")).Should().BeEmpty();
            File.ReadAllText(Path.Combine(expectationsDir, templateOutputDir, "console.csproj").UnixifyLineBreaks()).Should()
                .BeEquivalentTo(File.ReadAllText(Path.Combine(workingDir, templateOutputDir, "console.csproj")).UnixifyLineBreaks());
            File.ReadAllText(Path.Combine(expectationsDir, templateOutputDir, "Program.cs").UnixifyLineBreaks()).Should()
                .BeEquivalentTo(File.ReadAllText(Path.Combine(workingDir, templateOutputDir, "Program.cs")).UnixifyLineBreaks());

            // Accept changes
            string verifiedDir = expectationsDir.Replace(".received", ".verified", StringComparison.Ordinal);
            Directory.Delete(verifiedDir, false);
            Directory.Move(expectationsDir, verifiedDir);

            //reset the expectations dir to where it was before previous run
            expectationsDir = Path.GetDirectoryName(expectationsDir)!;

            // And run again same scenario - verification should succeed now
            string workingDir2 = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var cmd2 = new BasicCommand(
                _log,
                "dotnet",
                Path.GetFullPath("Microsoft.TemplateEngine.Authoring.CLI.dll"),
                "verify",
                "console",
                "--template-args",
                "--use-program-main -o \"" + templateOutputDir + "\"  --no-restore",
                "--verify-std",
                "-o",
                workingDir2,
                "--expectations-directory",
                expectationsDir,
                "--unique-for",
                "architecture",
                "--unique-for",
                "RuntimeAndVersion");

            cmd2.Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining("Running the verification of console.")
                .And.NotHaveStdErr();

            Directory.Delete(workingDir, true);
            Directory.Delete(workingDir2, true);
            Directory.Delete(expectationsDir, true);
        }

        [Fact]
        public void VerifyCommandFullDevLoopWithNotInstalledTemplate()
        {
            // dots issue https://github.com/VerifyTests/Verify/issues/658
            string workingDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName().Replace(".", string.Empty));
            string expectationsDir = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName().Replace(".", string.Empty));
            string templateShortName = "TestAssets.SampleTestTemplate";
            string templateOutputDir = templateShortName;

            //get the template location
            string executingAssemblyPath = this.GetType().Assembly.Location;
            string templateLocation = Path.Combine(TestTemplatesLocation, "TestTemplate");

            var cmd = new BasicCommand(
                _log,
                "dotnet",
                Path.GetFullPath("Microsoft.TemplateEngine.Authoring.CLI.dll"),
                "verify",
                templateShortName,
                "--template-path",
                templateLocation,
                "--template-args",
                "--paramB true",
                "--verify-std",
                "-o",
                workingDir,
                "--expectations-directory",
                expectationsDir,
                "--disable-diff-tool");

            cmd.Execute()
                .Should()
                .ExitWith((int)TemplateVerificationErrorCode.VerificationFailed)
                .And.HaveStdOutContaining("Verification Failed.");

            // Assert template created
            Directory.Exists(Path.Combine(workingDir, templateOutputDir)).Should().BeTrue();
            File.Exists(Path.Combine(workingDir, templateOutputDir, "Test.cs")).Should().BeTrue();

            // Assert verification files created
            Directory.Exists(expectationsDir).Should().BeTrue();
            Directory.GetDirectories(expectationsDir).Length.Should().Be(2);
            //for simplicity move to the created dir
            expectationsDir = Directory.GetDirectories(expectationsDir).Single(d => d.EndsWith(".received", StringComparison.Ordinal));
            File.Exists(Path.Combine(expectationsDir, templateOutputDir, "Test.cs")).Should().BeTrue();
            File.Exists(Path.Combine(expectationsDir, "std-streams", "stdout.txt")).Should().BeTrue();
            File.Exists(Path.Combine(expectationsDir, "std-streams", "stderr.txt")).Should().BeTrue();
            Directory.GetFiles(expectationsDir, "*", SearchOption.AllDirectories).Length.Should().Be(3);

            File.ReadAllText(Path.Combine(expectationsDir, templateOutputDir, "Test.cs").UnixifyLineBreaks()).Should()
                .BeEquivalentTo(File.ReadAllText(Path.Combine(workingDir, templateOutputDir, "Test.cs")).UnixifyLineBreaks());

            // Accept changes
            string verifiedDir = expectationsDir.Replace(".received", ".verified", StringComparison.Ordinal);
            Directory.Delete(verifiedDir, false);
            Directory.Move(expectationsDir, verifiedDir);

            //reset the expectations dir to where it was before previous run
            expectationsDir = Path.GetDirectoryName(expectationsDir)!;

            // And run again same scenario - verification should succeed now
            string workingDir2 = Path.Combine(Path.GetTempPath(), Path.GetRandomFileName());
            var cmd2 = new BasicCommand(
                _log,
                "dotnet",
                Path.GetFullPath("Microsoft.TemplateEngine.Authoring.CLI.dll"),
                "verify",
                templateShortName,
                "--template-path",
                templateLocation,
                "--template-args",
                "--paramB true",
                "--verify-std",
                "-o",
                workingDir2,
                "--expectations-directory",
                expectationsDir);

            cmd2.Execute()
                .Should()
                .Pass()
                .And.HaveStdOutContaining(string.Format("Running the verification of {0}.", templateShortName))
                .And.NotHaveStdErr();

            Directory.Delete(workingDir, true);
            Directory.Delete(workingDir2, true);
            Directory.Delete(expectationsDir, true);
        }
    }
}
