// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using NuGet.Versioning;

namespace Microsoft.DotNet.MsiInstallerTests.Framework
{
    [Collection("VM Tests")]
    public class VMTestBase : SdkTest, IDisposable
    {
        internal VirtualMachine VM { get; }

        public VMTestBase(ITestOutputHelper log) : base(log)
        {
            VM = new VirtualMachine(Log);
            _sdkInstallerVersion = new Lazy<string>(() =>
            {
                if (!string.IsNullOrEmpty(VM.VMTestSettings.SdkInstallerVersion))
                {
                    return VM.VMTestSettings.SdkInstallerVersion;
                }
                else
                {
                    var sdkTestingDir = VM.GetRemoteDirectory(@"c:\SdkTesting");

                    string installerPrefix = "dotnet-sdk-";
                    string installerSuffix = "-win-x64.exe";

                    List<string> sdkInstallerVersions = new List<string>();
                    foreach (var file in sdkTestingDir.Files.Select(f => Path.GetFileName(f)))
                    {
                        if (file.StartsWith(installerPrefix) && file.EndsWith(installerSuffix))
                        {
                            sdkInstallerVersions.Add(file.Substring(installerPrefix.Length, file.Length - installerPrefix.Length - installerSuffix.Length));
                        }
                    }

                    if (sdkInstallerVersions.Count == 0)
                    {
                        throw new Exception("No SDK installer found on VM");
                    }

                    return sdkInstallerVersions.MaxBy(v => new NuGetVersion(v));
                }
            });
        }

        public virtual void Dispose()
        {
            VM.Dispose();
        }

        Lazy<string> _sdkInstallerVersion;

        protected string SdkInstallerVersion => _sdkInstallerVersion.Value;

        protected string SdkInstallerFileName => $"dotnet-sdk-{SdkInstallerVersion}-win-x64.exe";

        protected void InstallSdk(bool deployStage2 = true)
        {
            VM.CreateRunCommand("setx", "DOTNET_NOLOGO", "true")
                .WithDescription("Disable .NET SDK first run message")
                .Execute()
                .Should()
                .Pass();

            VM.CreateRunCommand($@"c:\SdkTesting\{SdkInstallerFileName}", "/quiet")
                .WithDescription($"Install SDK {SdkInstallerVersion}")
                .Execute().Should().Pass();

            

            if (deployStage2)
            {
                DeployStage2Sdk();
            }
        }

        protected void UninstallSdk()
        {
            VM.CreateRunCommand($@"c:\SdkTesting\{SdkInstallerFileName}", "/quiet", "/uninstall")
                .WithDescription($"Uninstall SDK {SdkInstallerVersion}")
                .Execute()
                .Should()
                .Pass();
        }

        protected void DeployStage2Sdk()
        {
            if (!VM.VMTestSettings.ShouldTestStage2)
            {
                return;
            }


            //  Install any runtimes that are in the c:\SdkTesting directory, to support using older baseline SDK versions with a newer stage 2
            var sdkTestingDir = VM.GetRemoteDirectory(@"c:\SdkTesting");
            List<string> runtimeInstallers = new List<string>();
            string installerPrefix = "dotnet-runtime-";
            string installerSuffix = "-win-x64.exe";
            foreach (var file in sdkTestingDir.Files.Select(Path.GetFileName))
            {
                if (file.StartsWith(installerPrefix) && file.EndsWith(installerSuffix))
                {
                    runtimeInstallers.Add(file);
                }
            }

            if (runtimeInstallers.Any())
            {
                VM.CreateActionGroup($"Install .NET runtime(s)",
                        runtimeInstallers.Select(i => VM.CreateRunCommand($@"c:\SdkTesting\{i}", "/quiet")).ToArray())
                    .Execute().Should().Pass();
            }

            var result = VM.CreateRunCommand("dotnet", "--version")
                .WithIsReadOnly(true)
                .Execute();

            result.Should().Pass();

            string existingVersionToOverwrite = result.StdOut;

            var installedSdkFolder = $@"c:\Program Files\dotnet\sdk\{existingVersionToOverwrite}";

            Log.WriteLine($"Deploying SDK from {TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest} to {installedSdkFolder} on VM.");

            //  TODO: It would be nice if the description included the date/time of the SDK build, to distinguish different snapshots
            VM.CreateActionGroup("Deploy Stage 2 SDK",
                    VM.CopyFolder(TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, installedSdkFolder),
                    ChangeVersionFileContents(existingVersionToOverwrite))
                .Execute()
                .Should()
                .Pass();
        }

        protected void ChangeSdkVersion(string oldVersion, string newVersion)
        {
            var oldSdkFolder = $@"c:\Program Files\dotnet\sdk\{oldVersion}";
            var newSdkFolder = $@"c:\Program Files\dotnet\sdk\{newVersion}";

            new VMMoveFolderAction(VM)
            {
               SourcePath = oldSdkFolder,
               TargetPath = newSdkFolder
            }
                .WithDescription($"Change SDK version to {newVersion}")
                .Execute().Should().Pass();

            ChangeVersionFileContents(newVersion)
                .WithDescription("Update .version file")
                .Execute()
                .Should()
                .Pass();

        }

        private VMWriteFileAction ChangeVersionFileContents(string sdkVersion)
        {
            var installedSdkFolder = $@"c:\Program Files\dotnet\sdk\{sdkVersion}";
            var vmVersionFilePath = Path.Combine(installedSdkFolder, ".version");

            var newVersionFileContents = File.ReadAllLines(Path.Combine(TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, ".version"));
            newVersionFileContents[1] = sdkVersion;

            return VM.WriteFile(vmVersionFilePath, string.Join(Environment.NewLine, newVersionFileContents));

        }

        protected string GetInstalledSdkVersion()
        {
            var command = VM.CreateRunCommand("dotnet", "--version");
            command.IsReadOnly = true;
            var result = command.Execute();
            result.Should().Pass();
            return result.StdOut;
        }

        protected CommandResult InstallWorkload(string workloadName, bool skipManifestUpdate)
        {
            string [] args = { "dotnet", "workload", "install", workloadName};
            if (skipManifestUpdate)
            {
                args = [.. args, "--skip-manifest-update"];
            }

            var result = VM.CreateRunCommand(args)
                    .WithDescription($"Install {workloadName} workload")
                    .Execute();

            result.Should().Pass();

            return result;
        }

        protected WorkloadSet GetRollback(string directory = null)
        {
            var result = VM.CreateRunCommand("dotnet", "workload", "update", "--print-rollback")
                .WithWorkingDirectory(directory)
                .WithIsReadOnly(true)
                .Execute();

            result.Should().Pass();

            return ParseRollbackOutput(result.StdOut);
        }

        protected WorkloadSet ParseRollbackOutput(string output)
        {
            var filteredOutput = string.Join(Environment.NewLine,
                output.Split(Environment.NewLine)
                .Except(["==workloadRollbackDefinitionJsonOutputStart==", "==workloadRollbackDefinitionJsonOutputEnd=="]));

            return WorkloadSet.FromJson(filteredOutput, defaultFeatureBand: new SdkFeatureBand(SdkInstallerVersion));
        }

        protected string GetWorkloadVersion()
        {
            var result = VM.CreateRunCommand("dotnet", "workload", "--version")
                .WithIsReadOnly(true)
                .Execute();

            result.Should().Pass();

            return result.StdOut;
        }

        protected void AddNuGetSource(string source)
        {
            VM.CreateRunCommand("dotnet", "nuget", "add", "source", source)
                .WithDescription($"Add {source} to NuGet.config")
                .Execute()
                .Should()
                .Pass();
        }
    }
}
