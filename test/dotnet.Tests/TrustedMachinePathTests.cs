// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.Versioning;
using System.Security.AccessControl;
using System.Security.Principal;
using Microsoft.DotNet;
using Microsoft.DotNet.Cli.Commands.Workload;
using Microsoft.DotNet.Cli.Installer.Windows;

namespace Microsoft.DotNet.Tests;

[SupportedOSPlatform("windows5.1.2600")]
[TestClass]
public class TrustedMachinePathTests
{
    private const string TestPath = @"C:\ProgramData\dotnet";

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(@"C:\ProgramData\dotnet\workloads\pack", @"C:\ProgramData\dotnet", true)]
    [DataRow(@"C:\ProgramData\dotnet", @"C:\ProgramData\dotnet", true)]
    [DataRow(@"C:\ProgramData\dotnet", @"C:\ProgramData\dotnet\", true)]
    [DataRow(@"C:\ProgramData\dotnetEvil\evil", @"C:\ProgramData\dotnet", false)]
    [DataRow(@"C:\ProgramData\dotnet\..\evil", @"C:\ProgramData\dotnet", false)]
    public void IsUnderRoot_ReturnsExpectedResult(string path, string root, bool expected)
    {
        Assert.AreEqual(expected, TrustedMachinePath.IsUnderRoot(path, root));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Validate_AcceptsTrustedDescriptor()
    {
        TrustedMachinePath.Validate(CreateTrustedDescriptor(), TestPath);
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
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

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Validate_RejectsUntrustedOwner()
    {
        DirectorySecurity security = CreateTrustedDescriptor();
        security.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null));

        Assert.ThrowsExactly<InvalidDataException>(() => TrustedMachinePath.Validate(security, TestPath));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow(FileSystemRights.WriteData)]
    [DataRow(FileSystemRights.AppendData)]
    [DataRow(FileSystemRights.Delete)]
    [DataRow(FileSystemRights.ChangePermissions)]
    [DataRow(FileSystemRights.TakeOwnership)]
    // FILE_DELETE_CHILD removes a child whatever the child's own descriptor says.
    [DataRow(FileSystemRights.DeleteSubdirectoriesAndFiles)]
    public void Validate_RejectsUntrustedModificationRights(FileSystemRights rights)
    {
        DirectorySecurity security = CreateTrustedDescriptor();
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            rights,
            AccessControlType.Allow));

        Assert.ThrowsExactly<InvalidDataException>(() => TrustedMachinePath.Validate(security, TestPath));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Validate_RejectsInheritOnlyModificationRights()
    {
        // An inherit-only rule grants nothing on the root itself but becomes effective on every
        // descendant, which is what root-anchored trust relies on.
        DirectorySecurity security = CreateTrustedDescriptor();
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.WriteData,
            InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit,
            PropagationFlags.InheritOnly,
            AccessControlType.Allow));

        Assert.ThrowsExactly<InvalidDataException>(() => TrustedMachinePath.Validate(security, TestPath));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Validate_RejectsMissingTrustedFullControl()
    {
        DirectorySecurity security = new();
        SecurityIdentifier administrators = new(WellKnownSidType.BuiltinAdministratorsSid, null);
        security.SetOwner(administrators);
        security.AddAccessRule(new FileSystemAccessRule(administrators, FileSystemRights.FullControl, AccessControlType.Allow));

        // SYSTEM has no rule, so the descriptor cannot be trusted.
        Assert.ThrowsExactly<InvalidDataException>(() => TrustedMachinePath.Validate(security, TestPath));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void ValidateDirectory_RejectsCallerWritableDirectory()
    {
        string directory = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);

        try
        {
            Assert.ThrowsExactly<InvalidDataException>(() => TrustedMachinePath.ValidateDirectory(directory));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    [DataRow("C:")]
    [DataRow(".")]
    [DataRow("trailingdot.")]
    [DataRow("trailingspace ")]
    public void ValidatePathComponent_RejectsWindowsPathAliases(string input)
    {
        Assert.IsFalse(PathComponentUtilities.IsValidPathComponent(input));
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void InstallStateContents_RejectsReadUnderUntrustedMachineRoot()
    {
        string testRoot = Path.Combine(SdkTestContext.Current.TestExecutionDirectory, Guid.NewGuid().ToString("N"));
        string installStatePath = Path.Combine(testRoot, "workloads", "x64", "10.0.100", "InstallState", "default.json");
        Directory.CreateDirectory(Path.GetDirectoryName(installStatePath)!);
        File.WriteAllText(installStatePath, "{}");

        try
        {
            Assert.ThrowsExactly<InvalidDataException>(
                () => InstallStateContents.FromPath(installStatePath, testRoot));
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void InstallStateContents_AllowsReadOutsideMachineRoot()
    {
        string testRoot = Path.Combine(SdkTestContext.Current.TestExecutionDirectory, Guid.NewGuid().ToString("N"));
        string machineRoot = Path.Combine(testRoot, "machine");
        string installStatePath = Path.Combine(testRoot, "file-based", "default.json");
        Directory.CreateDirectory(Path.GetDirectoryName(installStatePath)!);
        File.WriteAllText(installStatePath, """{"useWorkloadSets":false}""");

        try
        {
            Assert.IsFalse(InstallStateContents.FromPath(installStatePath, machineRoot).UseWorkloadSets);
        }
        finally
        {
            Directory.Delete(testRoot, recursive: true);
        }
    }

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Validate_AcceptsDescriptorWrittenByShippedSdks()
    {
        // SecurityUtils.SetDirectoryAccessRules has produced this exact shape since 2023, so every
        // %ProgramData%\dotnet provisioned by a shipped SDK must still be accepted.
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

    [TestMethod]
    [OSCondition(OperatingSystems.Windows)]
    public void Validate_RejectsProgramDataInheritedUserWriteGrant()
    {
        // A root created without an explicit descriptor inherits C:\ProgramData's
        // BUILTIN\Users:(CI)(WD,AD,WEA,WA), which really does leave it user-writable.
        DirectorySecurity security = CreateTrustedDescriptor();
        security.AddAccessRule(new FileSystemAccessRule(
            new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid, null),
            FileSystemRights.Write,
            InheritanceFlags.ContainerInherit,
            PropagationFlags.None,
            AccessControlType.Allow));

        Assert.ThrowsExactly<InvalidDataException>(() => TrustedMachinePath.Validate(security, TestPath));
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
}
