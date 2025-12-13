// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Transactions;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.NuGetPackageDownloader;
using Microsoft.DotNet.Cli.ShellShim;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.Cli.Utils.Extensions;
using Microsoft.DotNet.Tools.Tests.ComponentMocks;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Frameworks;

namespace Microsoft.DotNet.ShellShim.Tests
{
    public class ShellShimRepositoryTests : SdkTest
    {
        public ShellShimRepositoryTests(ITestOutputHelper output) : base(output)
        {
        }

        [Fact]
        public void GivenAnExecutablePathItCanGenerateShimFile()
        {
            var outputDll = MakeHelloWorldExecutableDll();
            var pathToShim = GetNewCleanFolderUnderTempRoot();
            ShellShimRepository shellShimRepository = ConfigBasicTestDependencyShellShimRepository(pathToShim);
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();

            var command = new ToolCommand(new ToolCommandName(shellCommandName), "dotnet", outputDll);

            shellShimRepository.CreateShim(command);

            var stdOut = ExecuteInShell(shellCommandName, pathToShim);

            stdOut.Should().Contain("Hello World");
        }

        // Reproduce https://github.com/dotnet/cli/issues/9319
        [Fact]
        public void GivenAnExecutableAndRelativePathToShimPathItCanGenerateShimFile()
        {
            var outputDll = MakeHelloWorldExecutableDll();
            // To reproduce the bug, dll need to be nested under the shim
            var parentPathAsShimPath = outputDll.GetDirectoryPath().GetParentPath().GetParentPath().Value;
            var relativePathToShim = Path.GetRelativePath(
                Directory.GetCurrentDirectory(),
                parentPathAsShimPath);

            ShellShimRepository shellShimRepository = ConfigBasicTestDependencyShellShimRepository(relativePathToShim);
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();

            var command = new ToolCommand(new ToolCommandName(shellCommandName), "dotnet", outputDll);
            shellShimRepository.CreateShim(command);

            var stdOut = ExecuteInShell(shellCommandName, relativePathToShim);

            stdOut.Should().Contain("Hello World");
        }

        private static ShellShimRepository ConfigBasicTestDependencyShellShimRepository(string pathToShim)
        {
            string stage2AppHostTemplateDirectory = GetAppHostTemplateFromStage2();

            return new ShellShimRepository(new DirectoryPath(pathToShim), stage2AppHostTemplateDirectory);
        }

        [Fact]
        public void GivenAnExecutablePathItCanGenerateShimFileInTransaction()
        {
            var outputDll = MakeHelloWorldExecutableDll();
            var pathToShim = GetNewCleanFolderUnderTempRoot();
            var shellShimRepository = ConfigBasicTestDependencyShellShimRepository(pathToShim);
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();

            var command = new ToolCommand(new ToolCommandName(shellCommandName), "dotnet", outputDll);

            using (var transactionScope = new TransactionScope(
                TransactionScopeOption.Required,
                TimeSpan.Zero))
            {
                shellShimRepository.CreateShim(command);
                transactionScope.Complete();
            }

            var stdOut = ExecuteInShell(shellCommandName, pathToShim);

            stdOut.Should().Contain("Hello World");
        }

        [Fact]
        public void GivenAnExecutablePathDirectoryThatDoesNotExistItCanGenerateShimFile()
        {
            var outputDll = MakeHelloWorldExecutableDll();
            var testFolder = _testAssetsManager.CreateTestDirectory().Path;
            var extraNonExistDirectory = Path.GetRandomFileName();
            var shellShimRepository = new ShellShimRepository(new DirectoryPath(Path.Combine(testFolder, extraNonExistDirectory)), GetAppHostTemplateFromStage2());
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();
            var command = new ToolCommand(new ToolCommandName(shellCommandName), "dotnet", outputDll);

            Action a = () => shellShimRepository.CreateShim(command);

            a.Should().NotThrow<DirectoryNotFoundException>();
        }

        [Theory]
        [InlineData("arg1 arg2", new[] { "arg1", "arg2" })]
        [InlineData(" \"arg1 with space\" arg2", new[] { "arg1 with space", "arg2" })]
        [InlineData(" \"arg with ' quote\" ", new[] { "arg with ' quote" })]
        public void GivenAShimItPassesThroughArguments(string arguments, string[] expectedPassThru)
        {
            var outputDll = MakeHelloWorldExecutableDll(identifier: arguments);
            var pathToShim = GetNewCleanFolderUnderTempRoot();
            var shellShimRepository = ConfigBasicTestDependencyShellShimRepository(pathToShim);
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();
            var command = new ToolCommand(new ToolCommandName(shellCommandName), "dotnet", outputDll);

            shellShimRepository.CreateShim(command);

            var stdOut = ExecuteInShell(shellCommandName, pathToShim, arguments);

            for (int i = 0; i < expectedPassThru.Length; i++)
            {
                stdOut.Should().Contain($"{i} = {expectedPassThru[i]}");
            }
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAShimConflictItWillRollback(bool testMockBehaviorIsInSync)
        {
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();
            var pathToShim = GetNewCleanFolderUnderTempRoot();
            MakeNameConflictingCommand(pathToShim, shellCommandName);

            IShellShimRepository shellShimRepository;
            if (testMockBehaviorIsInSync)
            {
                shellShimRepository = GetShellShimRepositoryWithMockMaker(pathToShim);
            }
            else
            {
                shellShimRepository = ConfigBasicTestDependencyShellShimRepository(pathToShim);
            }

            var command = new ToolCommand(new ToolCommandName(shellCommandName), "dotnet", new FilePath("dummy.dll"));

            Action a = () =>
            {
                using (var scope = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero))
                {
                    shellShimRepository.CreateShim(command);

                    scope.Complete();
                }
            };

            a.Should().Throw<ShellShimException>().Where(
                ex => ex.Message ==
                    string.Format(
                        CliStrings.ShellShimConflict,
                        shellCommandName));

            Directory
                .EnumerateFileSystemEntries(pathToShim)
                .Should()
                .HaveCount(1, "should only be the original conflicting command");
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnExceptionItWillRollback(bool testMockBehaviorIsInSync)
        {
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();
            var pathToShim = GetNewCleanFolderUnderTempRoot();

            IShellShimRepository shellShimRepository;
            if (testMockBehaviorIsInSync)
            {
                shellShimRepository = GetShellShimRepositoryWithMockMaker(pathToShim);
            }
            else
            {
                shellShimRepository = ConfigBasicTestDependencyShellShimRepository(pathToShim);
            }



            Action intendedError = () => throw new ToolPackageException("simulated error");



            Action a = () =>
            {
                using (var scope = new TransactionScope(
                    TransactionScopeOption.Required,
                    TimeSpan.Zero))
                {
                    FilePath targetExecutablePath = MakeHelloWorldExecutableDll(identifier: testMockBehaviorIsInSync.ToString());
                    var command = new ToolCommand(new ToolCommandName(shellCommandName), "dotnet", targetExecutablePath);
                    shellShimRepository.CreateShim(command);

                    intendedError();
                    scope.Complete();
                }
            };
            a.Should().Throw<ToolPackageException>().WithMessage("simulated error");

            Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenANonexistentShimRemoveDoesNotThrow(bool testMockBehaviorIsInSync)
        {
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();
            var pathToShim = GetNewCleanFolderUnderTempRoot();

            IShellShimRepository shellShimRepository;
            if (testMockBehaviorIsInSync)
            {
                shellShimRepository = GetShellShimRepositoryWithMockMaker(pathToShim);
            }
            else
            {
                shellShimRepository = ConfigBasicTestDependencyShellShimRepository(pathToShim);
            }

            Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();

            var command = new ToolCommand(new ToolCommandName(shellCommandName), "dotnet", new FilePath("dummyExe"));

            shellShimRepository.RemoveShim(command);

            Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnInstalledShimRemoveDeletesTheShimFiles(bool testMockBehaviorIsInSync)
        {
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();
            var pathToShim = GetNewCleanFolderUnderTempRoot(identifier: testMockBehaviorIsInSync.ToString());

            IShellShimRepository shellShimRepository;
            if (testMockBehaviorIsInSync)
            {
                shellShimRepository = GetShellShimRepositoryWithMockMaker(pathToShim);
            }
            else
            {
                shellShimRepository = ConfigBasicTestDependencyShellShimRepository(pathToShim);
            }

            Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();

            FilePath targetExecutablePath = MakeHelloWorldExecutableDll(identifier: testMockBehaviorIsInSync.ToString());
            var command = new ToolCommand(new ToolCommandName(shellCommandName), "dotnet", targetExecutablePath);
            shellShimRepository.CreateShim(command);

            Directory.EnumerateFileSystemEntries(pathToShim).Should().NotBeEmpty();

            shellShimRepository.RemoveShim(command);

            Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnInstalledShimRemoveRollsbackIfTransactionIsAborted(bool testMockBehaviorIsInSync)
        {
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();
            var pathToShim = GetNewCleanFolderUnderTempRoot();

            IShellShimRepository shellShimRepository;
            if (testMockBehaviorIsInSync)
            {
                shellShimRepository = GetShellShimRepositoryWithMockMaker(pathToShim);
            }
            else
            {
                shellShimRepository = ConfigBasicTestDependencyShellShimRepository(pathToShim);
            }

            Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();

            FilePath targetExecutablePath = MakeHelloWorldExecutableDll(identifier: testMockBehaviorIsInSync.ToString());
            var command = new ToolCommand(new ToolCommandName(shellCommandName), "dotnet", targetExecutablePath);
            shellShimRepository.CreateShim(command);

            Directory.EnumerateFileSystemEntries(pathToShim).Should().NotBeEmpty();

            using (var scope = new TransactionScope(
                TransactionScopeOption.Required,
                TimeSpan.Zero))
            {
                shellShimRepository.RemoveShim(command);

                Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();
            }

            Directory.EnumerateFileSystemEntries(pathToShim).Should().NotBeEmpty();
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public void GivenAnInstalledShimRemoveCommitsIfTransactionIsCompleted(bool testMockBehaviorIsInSync)
        {
            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();
            var pathToShim = GetNewCleanFolderUnderTempRoot(identifier: testMockBehaviorIsInSync.ToString());

            IShellShimRepository shellShimRepository;
            if (testMockBehaviorIsInSync)
            {
                shellShimRepository = GetShellShimRepositoryWithMockMaker(pathToShim);
            }
            else
            {
                shellShimRepository = ConfigBasicTestDependencyShellShimRepository(pathToShim);
            }

            Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();

            FilePath targetExecutablePath = MakeHelloWorldExecutableDll(identifier: testMockBehaviorIsInSync.ToString());
            var command = new ToolCommand(new ToolCommandName(shellCommandName), "dotnet", targetExecutablePath);
            shellShimRepository.CreateShim(command);

            Directory.EnumerateFileSystemEntries(pathToShim).Should().NotBeEmpty();

            using (var scope = new TransactionScope(
                TransactionScopeOption.Required,
                TimeSpan.Zero))
            {
                shellShimRepository.RemoveShim(command);

                Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();

                scope.Complete();
            }

            Directory.EnumerateFileSystemEntries(pathToShim).Should().BeEmpty();
        }

        [Fact]
        public void WhenPackagedShimProvidedItCopies()
        {
            const string tokenToIdentifyCopiedShim = "packagedShim";

            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();
            var pathToShim = GetNewCleanFolderUnderTempRoot();
            var packagedShimFolder = GetNewCleanFolderUnderTempRoot();
            var dummyShimPath = Path.Combine(packagedShimFolder, shellCommandName);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                dummyShimPath = dummyShimPath + ".exe";
            }

            File.WriteAllText(dummyShimPath, tokenToIdentifyCopiedShim);

            ShellShimRepository shellShimRepository = GetShellShimRepositoryWithMockMaker(pathToShim);

            var command = new ToolCommand(new ToolCommandName(shellCommandName), "dotnet", new FilePath("dummy.dll"));

            shellShimRepository.CreateShim(
                command,
                new[] { new FilePath(dummyShimPath) });

            var createdShim = Directory.EnumerateFileSystemEntries(pathToShim).Single();
            File.ReadAllText(createdShim).Should().Contain(tokenToIdentifyCopiedShim);
        }

        [Fact]
        public void WhenMultipleSameNamePackagedShimProvidedItThrows()
        {
            const string tokenToIdentifyCopiedShim = "packagedShim";

            var shellCommandName = nameof(ShellShimRepositoryTests) + Path.GetRandomFileName();
            var pathToShim = GetNewCleanFolderUnderTempRoot();
            var packagedShimFolder = GetNewCleanFolderUnderTempRoot();
            var dummyShimPath = Path.Combine(packagedShimFolder, shellCommandName);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                dummyShimPath = dummyShimPath + ".exe";
            }

            File.WriteAllText(dummyShimPath, tokenToIdentifyCopiedShim);
            ShellShimRepository shellShimRepository = GetShellShimRepositoryWithMockMaker(pathToShim);

            FilePath[] filePaths = new[] { new FilePath(dummyShimPath), new FilePath("path" + dummyShimPath) };

            var command = new ToolCommand(new ToolCommandName(shellCommandName), "dotnet", new FilePath("dummy.dll"));

            Action a = () => shellShimRepository.CreateShim(
                command,
                new[] { new FilePath(dummyShimPath), new FilePath("path" + dummyShimPath) });

            a.Should().Throw<ShellShimException>()
                .And.Message
                .Should().Contain(
                    string.Format(
                           CliStrings.MoreThanOnePackagedShimAvailable,
                           string.Join(';', filePaths)));
        }

        [WindowsOnlyTheory]
        [InlineData("net5.0")]
        [InlineData("netcoreapp3.1")]
        public void WhenRidNotSupportedOnWindowsItIsImplicit(string tfm)
        {
            var tempDir = _testAssetsManager.CreateTestDirectory(identifier: tfm).Path;
            var templateFinder = new ShellShimTemplateFinder(new MockNuGetPackageDownloader(), new DirectoryPath(tempDir), null);
            var path = templateFinder.ResolveAppHostSourceDirectoryAsync(null, NuGetFramework.Parse(tfm), Architecture.Arm64).Result;
            path.Should().Contain(tfm.Equals("net5.0") ? "AppHostTemplate" : "win-x64");
        }

        private static void MakeNameConflictingCommand(string pathToPlaceShim, string shellCommandName)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                shellCommandName = shellCommandName + ".exe";
            }

            File.WriteAllText(Path.Combine(pathToPlaceShim, shellCommandName), string.Empty);
        }

        private string ExecuteInShell(string shellCommandName, string cleanFolderUnderTempRoot, string arguments = "")
        {
            ProcessStartInfo processStartInfo;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                var file = Path.Combine(cleanFolderUnderTempRoot, shellCommandName + ".exe");
                processStartInfo = new ProcessStartInfo
                {
                    FileName = file,
                    UseShellExecute = false,
                    Arguments = arguments,
                };
            }
            else
            {
                var file = Path.Combine(cleanFolderUnderTempRoot, shellCommandName);
                processStartInfo = new ProcessStartInfo
                {
                    FileName = file,
                    Arguments = arguments,
                    UseShellExecute = false
                };
            }

            Log.WriteLine($"Launching '{processStartInfo.FileName} {processStartInfo.Arguments}'");
            processStartInfo.WorkingDirectory = cleanFolderUnderTempRoot;

            var environmentProvider = new EnvironmentProvider();
            processStartInfo.EnvironmentVariables["PATH"] = environmentProvider.GetEnvironmentVariable("PATH");
            if (Environment.Is64BitProcess)
            {
                processStartInfo.EnvironmentVariables["DOTNET_ROOT"] =
                    TestContext.Current.ToolsetUnderTest.DotNetRoot;
            }
            else
            {
                processStartInfo.EnvironmentVariables["DOTNET_ROOT(x86)"] =
                    TestContext.Current.ToolsetUnderTest.DotNetRoot;
            }

            processStartInfo.ExecuteAndCaptureOutput(out var stdOut, out var stdErr);

            stdErr.Should().BeEmpty();

            return stdOut ?? "";
        }

        private static string GetAppHostTemplateFromStage2()
        {
            var stage2AppHostTemplateDirectory =
                Path.Combine(TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, "AppHostTemplate");
            return stage2AppHostTemplateDirectory;
        }

        private FilePath MakeHelloWorldExecutableDll([CallerMemberName] string callingMethod = "", string identifier = null)
        {
            const string testAppName = "TestAppSimple";

            var testInstance = _testAssetsManager.CopyTestAsset(testAppName, callingMethod: callingMethod, identifier: identifier)
                .WithSource();

            new BuildCommand(testInstance)
                .Execute()
                .Should()
                .Pass();

            var configuration = Environment.GetEnvironmentVariable("CONFIGURATION") ?? "Debug";

            var outputDirectory = new DirectoryInfo(OutputPathCalculator.FromProject(testInstance.Path, testInstance).GetOutputDirectory(configuration: configuration));

            return new FilePath(Path.Combine(outputDirectory.FullName, $"{testAppName}.dll"));
        }

        private string GetNewCleanFolderUnderTempRoot([CallerMemberName] string callingMethod = null, string identifier = "")
        {
            return _testAssetsManager.CreateTestDirectory(testName: callingMethod, identifier: "cleanfolder" + identifier + Path.GetRandomFileName()).Path;
        }

        private ShellShimRepository GetShellShimRepositoryWithMockMaker(string pathToShim)
        {
            return new ShellShimRepository(
                    new DirectoryPath(pathToShim),
                    string.Empty,
                    appHostShellShimMaker: new AppHostShellShimMakerMock());
        }
    }
}
