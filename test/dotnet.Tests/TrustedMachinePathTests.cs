// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.DotNet;
using Microsoft.DotNet.Cli.Commands.Workload;
using Microsoft.DotNet.Cli.Installer.Windows;

namespace Microsoft.DotNet.Tests
{
    [SupportedOSPlatform("windows5.1.2600")]
    public class TrustedMachinePathTests
    {
        private const string TestPath = @"C:\ProgramData\dotnet";

        [WindowsOnlyTheory]
        [InlineData(@"C:\ProgramData\dotnet\workloads\pack", @"C:\ProgramData\dotnet", true)]
        [InlineData(@"C:\ProgramData\dotnet", @"C:\ProgramData\dotnet", true)]
        [InlineData(@"C:\ProgramData\dotnet", @"C:\ProgramData\dotnet\", true)]
        [InlineData(@"C:\ProgramData\dotnetEvil\evil", @"C:\ProgramData\dotnet", false)]
        [InlineData(@"C:\ProgramData\dotnet\..\evil", @"C:\ProgramData\dotnet", false)]
        public void IsUnderRoot_ReturnsExpectedResult(string path, string root, bool expected)
        {
            Assert.Equal(expected, TrustedMachinePath.IsUnderRoot(path, root));
        }

        [WindowsOnlyFact]
        public void Validate_AcceptsTrustedDescriptor()
        {
            TrustedMachinePath.Validate(CreateTrustedDescriptor(), TestPath);
        }

        [WindowsOnlyFact]
        public void Validate_AllowsAdditionalReadOnlyAndDenyRules()
        {
            DirectorySecurity security = CreateTrustedDescriptor();
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                FileSystemRights.ReadAndExecute,
                AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.WorldSid, null),
                FileSystemRights.FullControl,
                AccessControlType.Deny));

            TrustedMachinePath.Validate(security, TestPath);
        }

        [WindowsOnlyFact]
        public void Validate_RejectsUntrustedOwner()
        {
            DirectorySecurity security = CreateTrustedDescriptor();
            security.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null));

            Assert.Throws<InvalidDataException>(() => TrustedMachinePath.Validate(security, TestPath));
        }

        [WindowsOnlyTheory]
        [InlineData(FileSystemRights.WriteData)]
        [InlineData(FileSystemRights.AppendData)]
        [InlineData(FileSystemRights.Delete)]
        [InlineData(FileSystemRights.ChangePermissions)]
        [InlineData(FileSystemRights.TakeOwnership)]
        [InlineData(FileSystemRights.DeleteSubdirectoriesAndFiles)]
        public void Validate_RejectsUntrustedModificationRights(FileSystemRights rights)
        {
            DirectorySecurity security = CreateTrustedDescriptor();
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                rights,
                AccessControlType.Allow));

            Assert.Throws<InvalidDataException>(() => TrustedMachinePath.Validate(security, TestPath));
        }

        [WindowsOnlyFact]
        public void Validate_RejectsInheritOnlyModificationRights()
        {
            DirectorySecurity security = CreateTrustedDescriptor();
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                FileSystemRights.WriteData,
                InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
                PropagationFlags.InheritOnly,
                AccessControlType.Allow));

            Assert.Throws<InvalidDataException>(() => TrustedMachinePath.Validate(security, TestPath));
        }

        [WindowsOnlyFact]
        public void Validate_RejectsMissingTrustedFullControl()
        {
            DirectorySecurity security = new();
            SecurityIdentifier administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
            security.SetOwner(administrators);
            security.AddAccessRule(new FileSystemAccessRule(administrators, FileSystemRights.FullControl, AccessControlType.Allow));

            Assert.Throws<InvalidDataException>(() => TrustedMachinePath.Validate(security, TestPath));
        }

        [WindowsOnlyFact]
        public void ValidateDirectory_RejectsCallerWritableDirectory()
        {
            string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);

            try
            {
                Assert.Throws<InvalidDataException>(() => TrustedMachinePath.ValidateDirectory(directory));
            }
            finally
            {
                Directory.Delete(directory, recursive: true);
            }
        }

        [WindowsOnlyTheory]
        [InlineData("C:")]
        [InlineData(".")]
        [InlineData("trailingdot.")]
        [InlineData("trailingspace ")]
        public void ValidatePathComponent_RejectsWindowsPathAliases(string input)
        {
            Assert.False(PathComponentUtilities.IsValidPathComponent(input));
        }

        [WindowsOnlyFact]
        public void InstallStateContents_RejectsReadUnderUntrustedMachineRoot()
        {
            string testRoot = CreateTemporaryDirectory();
            string installStatePath = Path.Combine(testRoot, "workloads", "x64", "10.0.100", "InstallState", "default.json");
            Directory.CreateDirectory(Path.GetDirectoryName(installStatePath)!);
            File.WriteAllText(installStatePath, "{}");

            try
            {
                Assert.Throws<InvalidDataException>(() => InstallStateContents.FromPath(installStatePath, testRoot));
            }
            finally
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }

        [WindowsOnlyFact]
        public void InstallStateContents_AllowsReadOutsideMachineRoot()
        {
            string testRoot = CreateTemporaryDirectory();
            string machineRoot = Path.Combine(testRoot, "machine");
            string installStatePath = Path.Combine(testRoot, "file-based", "default.json");
            Directory.CreateDirectory(Path.GetDirectoryName(installStatePath)!);
            File.WriteAllText(installStatePath, "{\"useWorkloadSets\":false}");

            try
            {
                Assert.Equal(false, InstallStateContents.FromPath(installStatePath, machineRoot).UseWorkloadSets);
            }
            finally
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }

        [WindowsOnlyFact]
        public void Validate_AcceptsDescriptorWrittenByShippedSdks()
        {
            SecurityIdentifier administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
            InheritanceFlags inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
            DirectorySecurity security = new();
            security.SetOwner(administrators);
            security.SetGroup(administrators);
            security.SetAccessRule(new FileSystemAccessRule(administrators, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
            security.SetAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null), FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
            security.SetAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null), FileSystemRights.ReadAndExecute, inheritance, PropagationFlags.None, AccessControlType.Allow));
            security.SetAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.WorldSid, null), FileSystemRights.ReadAndExecute, inheritance, PropagationFlags.None, AccessControlType.Allow));

            TrustedMachinePath.Validate(security, TestPath);
        }

        [WindowsOnlyFact]
        public void Validate_RejectsProgramDataInheritedUserWriteGrant()
        {
            DirectorySecurity security = CreateTrustedDescriptor();
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
                FileSystemRights.Write,
                InheritanceFlags.ContainerInherit,
                PropagationFlags.None,
                AccessControlType.Allow));

            Assert.Throws<InvalidDataException>(() => TrustedMachinePath.Validate(security, TestPath));
        }

        private static DirectorySecurity CreateTrustedDescriptor()
        {
            SecurityIdentifier administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
            DirectorySecurity security = new();
            security.SetOwner(administrators);
            security.AddAccessRule(new FileSystemAccessRule(administrators, FileSystemRights.FullControl, AccessControlType.Allow));
            security.AddAccessRule(new FileSystemAccessRule(
                new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null),
                FileSystemRights.FullControl,
                AccessControlType.Allow));

            return security;
        }

        private static string CreateTemporaryDirectory()
        {
            string path = Path.Combine(Path.GetTempPath(), nameof(TrustedMachinePathTests), Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(path);
            return path;
        }
    }
}
