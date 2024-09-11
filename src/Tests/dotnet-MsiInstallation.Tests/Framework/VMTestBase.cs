// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.WorkloadManifestReader;

namespace Microsoft.DotNet.MsiInstallerTests.Framework
{
    public class VMTestBase : SdkTest, IDisposable
    {
        internal VirtualMachine VM { get; }

        public VMTestBase(ITestOutputHelper log) : base(log)
        {
            VM = new VirtualMachine(Log);
        }

        public virtual void Dispose()
        {
            VM.Dispose();
        }

        protected string SdkInstallerVersion
        {
            get
            {
                if (!string.IsNullOrEmpty(VM.VMTestSettings.SdkInstallerVersion))
                {
                    return VM.VMTestSettings.SdkInstallerVersion;
                }
                else
                {
                    return "8.0.203";
                }
            }
        }

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
                .Execute()
                .Should()
                .Pass();

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

            var installedSdkFolder = $@"c:\Program Files\dotnet\sdk\{SdkInstallerVersion}";

            Log.WriteLine($"Deploying SDK from {TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest} to {installedSdkFolder} on VM.");

            //  TODO: It would be nice if the description included the date/time of the SDK build, to distinguish different snapshots
            VM.CreateActionGroup("Deploy Stage 2 SDK",
                    VM.CopyFolder(TestContext.Current.ToolsetUnderTest.SdkFolderUnderTest, installedSdkFolder),
                    ChangeVersionFileContents(SdkInstallerVersion))
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

        protected WorkloadSet GetRollback()
        {
            var result = VM.CreateRunCommand("dotnet", "workload", "update", "--print-rollback")
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
    }
}
