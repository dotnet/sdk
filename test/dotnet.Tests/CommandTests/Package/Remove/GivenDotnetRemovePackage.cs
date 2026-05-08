// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Remove.Package.Tests
{
    public class GivenDotnetRemovePackage : SdkTest
    {
        private Func<string, string> HelpText = (defaultVal) => $@"Description:
  Remove a NuGet package reference from the project.

Usage:
  dotnet remove [<PROJECT | FILE>] package <PACKAGE_NAME>... [options]

Arguments:
  <PROJECT | FILE>  The project file or C# file-based app to operate on. If a file is not specified, the command will search the current directory for a project file. [default: {PathUtility.EnsureTrailingSlash(defaultVal)}]
  <PACKAGE_NAME>    The package reference to remove.

Options:
  --interactive     Allows the command to stop and wait for user input or action (for example to complete authentication). [default: False]
  -?, -h, --help    Show command line help.";

        private Func<string, string> RemoveCommandHelpText = (defaultVal) => $@"Description:
  .NET Remove Command

Usage:
  dotnet remove <PROJECT | FILE> [command] [options]

Arguments:
  <PROJECT | FILE>  The project file or C# file-based app to operate on. If a file is not specified, the command will search the current directory for a project file. [default: {PathUtility.EnsureTrailingSlash(defaultVal)}]

Options:
  -?, -h, --help    Show command line help.

Commands:
  package <PACKAGE_NAME>      Remove a NuGet package reference from the project.
  reference <PROJECT_PATH>    Remove a project-to-project reference from the project";

        public GivenDotnetRemovePackage(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("--help")]
        [InlineData("-h")]
        public void WhenHelpOptionIsPassedItPrintsUsage(string helpArg)
        {
            var cmd = new DotnetCommand(Log).Execute($"remove", "package", helpArg);
            cmd.Should().Pass();
            cmd.StdOut.Should().BeVisuallyEquivalentToIfNotLocalized(HelpText(Directory.GetCurrentDirectory()));
        }

        [Theory]
        [InlineData("")]
        [InlineData("unknownCommandName")]
        public void WhenNoCommandIsPassedItPrintsError(string commandName)
        {
            var cmd = new DotnetCommand(Log)
                .Execute("remove", commandName);
            cmd.Should().Fail();
            cmd.StdErr.Should().Be(CliStrings.RequiredCommandNotPassed);
        }

        [Fact]
        public void WhenReferencedPackageIsPassedItGetsRemoved()
        {
            var projectDirectory = _testAssetsManager
                .CopyTestAsset("TestAppSimple")
                .WithSource().Path;

            var packageName = "Newtonsoft.Json";
            var add = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute("add", "package", packageName);
            add.Should().Pass();


            var remove = new DotnetCommand(Log)
                .WithWorkingDirectory(projectDirectory)
                .Execute($"remove", "package", packageName);

            remove.Should().Pass();
            remove.StdOut.Should().Contain($"Removing PackageReference for package '{packageName}' from project '{projectDirectory + Path.DirectorySeparatorChar}TestAppSimple.csproj'.");
            remove.StdErr.Should().BeEmpty();
        }

        [Fact]
        public void FileBasedApp()
        {
            var testInstance = _testAssetsManager.CreateTestDirectory();
            var file = Path.Join(testInstance.Path, "Program.cs");
            File.WriteAllText(file, """
                #:package Humanizer@2.14.1

                Console.WriteLine();
                """);

            new DotnetCommand(Log, "package", "remove", "Humanizer", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOut(string.Format(CliCommandStrings.DirectivesRemoved, "#:package", 1, "Humanizer", file));

            File.ReadAllText(file).Should().Be("""
                Console.WriteLine();
                """);
        }

        [Fact]
        public void FileBasedApp_Multiple()
        {
            var testInstance = _testAssetsManager.CreateTestDirectory();
            var file = Path.Join(testInstance.Path, "Program.cs");
            File.WriteAllText(file, """
                #:package Humanizer@2.14.1
                #:package Another@1.0.0
                #:property X=Y
                #:package Humanizer@2.9.9

                Console.WriteLine();
                """);

            new DotnetCommand(Log, "package", "remove", "Humanizer", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Pass()
                .And.HaveStdOut(string.Format(CliCommandStrings.DirectivesRemoved, "#:package", 2, "Humanizer", file));

            File.ReadAllText(file).Should().Be("""
                #:package Another@1.0.0
                #:property X=Y

                Console.WriteLine();
                """);
        }

        [Fact]
        public void FileBasedApp_None()
        {
            var testInstance = _testAssetsManager.CreateTestDirectory();
            var file = Path.Join(testInstance.Path, "Program.cs");
            File.WriteAllText(file, """
                Console.WriteLine();
                """);

            new DotnetCommand(Log, "package", "remove", "Humanizer", "--file", "Program.cs")
                .WithWorkingDirectory(testInstance.Path)
                .Execute()
                .Should().Fail()
                .And.HaveStdOut(string.Format(CliCommandStrings.DirectivesRemoved, "#:package", 0, "Humanizer", file));

            File.ReadAllText(file).Should().Be("""
                Console.WriteLine();
                """);
        }
    }
}
