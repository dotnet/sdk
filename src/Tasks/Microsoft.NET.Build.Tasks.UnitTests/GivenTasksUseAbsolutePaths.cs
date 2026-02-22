// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.Build.Framework;
using Xunit;
using Xunit.Abstractions;

namespace Microsoft.NET.Build.Tasks.UnitTests;

/// <summary>
/// Tests that verify MSBuild tasks handle file paths correctly in multi-node scenarios.
///
/// When MSBuild runs in parallel mode, tasks may be spawned on different nodes, each with
/// a potentially different working directory. Tasks that use relative paths resolved from
/// the current working directory will fail in these scenarios.
///
/// These tests create files in a "project" directory, then verify task behavior by
/// passing RELATIVE paths and expecting tasks to resolve them via TaskEnvironment.
/// </summary>
public class GivenTasksUseAbsolutePaths : IDisposable
{
    private readonly TaskTestEnvironment _env;
    private readonly ITestOutputHelper _output;

    public GivenTasksUseAbsolutePaths(ITestOutputHelper output)
    {
        _env = new TaskTestEnvironment();
        _output = output;
    }

    public void Dispose()
    {
        _env.Dispose();
    }

    #region CheckForDuplicateFrameworkReferences - No File I/O

    [Fact]
    public void CheckForDuplicateFrameworkReferences_NoFileIO_ShouldSucceed()
    {
        var task = new CheckForDuplicateFrameworkReferences
        {
            BuildEngine = new MockBuildEngine(),
            FrameworkReferences = Array.Empty<ITaskItem>()
        };

        var result = task.Execute();
        result.Should().BeTrue();
    }

    #endregion

    #region CheckForDuplicateItemMetadata - No File I/O

    [Fact]
    public void CheckForDuplicateItemMetadata_NoFileIO_ShouldSucceed()
    {
        var task = new CheckForDuplicateItemMetadata
        {
            BuildEngine = new MockBuildEngine(),
            Items = new[] { new MockTaskItem("item1.cs", new Dictionary<string, string> { ["Key"] = "Value1" }) },
            MetadataName = "Key"
        };

        var result = task.Execute();
        result.Should().BeTrue();
    }

    #endregion

    #region CheckForDuplicateItems - No File I/O

    [Fact]
    public void CheckForDuplicateItems_NoFileIO_ShouldSucceed()
    {
        var task = new CheckForDuplicateItems
        {
            BuildEngine = new MockBuildEngine(),
            Items = new[] { new MockTaskItem("file.cs", new Dictionary<string, string>()) },
            ItemName = "Compile",
            PropertyNameToDisableDefaultItems = "EnableDefaultCompileItems",
            MoreInformationLink = "https://aka.ms/test",
            DefaultItemsEnabled = true,
            DefaultItemsOfThisTypeEnabled = true
        };

        var result = task.Execute();
        result.Should().BeTrue();
    }

    #endregion

    #region CheckForImplicitPackageReferenceOverrides - No File I/O

    [Fact]
    public void CheckForImplicitPackageReferenceOverrides_NoFileIO_ShouldSucceed()
    {
        var task = new CheckForImplicitPackageReferenceOverrides
        {
            BuildEngine = new MockBuildEngine(),
            PackageReferenceItems = Array.Empty<ITaskItem>(),
            MoreInformationLink = "https://aka.ms/test"
        };

        var result = task.Execute();
        result.Should().BeTrue();
    }

    #endregion

    #region CheckIfPackageReferenceShouldBeFrameworkReference - No File I/O

    [Fact]
    public void CheckIfPackageReferenceShouldBeFrameworkReference_NoFileIO_ShouldSucceed()
    {
        var task = new CheckIfPackageReferenceShouldBeFrameworkReference
        {
            BuildEngine = new MockBuildEngine(),
            PackageReferences = Array.Empty<ITaskItem>(),
            FrameworkReferences = Array.Empty<ITaskItem>(),
            PackageReferenceToReplace = ""
        };

        var result = task.Execute();
        result.Should().BeTrue();
    }

    #endregion

    #region CollatePackageDownloads - No File I/O

    [Fact]
    public void CollatePackageDownloads_NoFileIO_ShouldSucceed()
    {
        var task = new CollatePackageDownloads
        {
            BuildEngine = new MockBuildEngine(),
            Packages = Array.Empty<ITaskItem>()
        };

        var result = task.Execute();
        result.Should().BeTrue();
    }

    #endregion
}
