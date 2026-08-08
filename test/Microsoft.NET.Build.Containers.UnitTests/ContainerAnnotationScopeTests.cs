// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Microsoft.NET.Build.Containers.Tasks;
using Moq;

namespace Microsoft.NET.Build.Containers.UnitTests;

[TestClass]
public class ContainerAnnotationScopeTests
{
    [TestMethod]
    [DataRow(null, true, true)]
    [DataRow("", true, true)]
    [DataRow("Manifest", true, false)]
    [DataRow(" index ", false, true)]
    [DataRow(" INDEX, manifest ", true, true)]
    public void FiltersScopes(string? scope, bool appliesToManifest, bool appliesToIndex)
    {
        TaskItem annotation = new("example.annotation");
        if (scope is not null)
        {
            annotation.SetMetadata("Scope", scope);
        }
        var task = new TestTask { BuildEngine = new Mock<IBuildEngine>().Object };

        Assert.IsTrue(ContainerAnnotationScopes.TryFilter([annotation], ContainerAnnotationScope.Manifest, task.Log, out ITaskItem[] manifests));
        Assert.HasCount(appliesToManifest ? 1 : 0, manifests);
        Assert.IsTrue(ContainerAnnotationScopes.TryFilter([annotation], ContainerAnnotationScope.Index, task.Log, out ITaskItem[] indexes));
        Assert.HasCount(appliesToIndex ? 1 : 0, indexes);
    }

    [TestMethod]
    public void RejectsInvalidScope()
    {
        TaskItem annotation = new("example.annotation");
        annotation.SetMetadata("Scope", "Manifest,Descriptor");
        var task = new TestTask { BuildEngine = new Mock<IBuildEngine>().Object };

        Assert.IsFalse(ContainerAnnotationScopes.TryFilter([annotation], ContainerAnnotationScope.Manifest, task.Log, out _));
        Assert.IsTrue(task.Log.HasLoggedErrors);
    }

    private sealed class TestTask : Microsoft.Build.Utilities.Task
    {
        public override bool Execute() => true;
    }
}
