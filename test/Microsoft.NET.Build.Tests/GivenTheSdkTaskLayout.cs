// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using NuGet.Frameworks;

namespace Microsoft.NET.Build.Tests;

[TestClass]
public sealed class GivenTheSdkTaskLayout : SdkTest
{
    [TestMethod]
    [DataRow("Microsoft.NET.Sdk", "tools", "Microsoft.NET.Build.Tasks.dll")]
    [DataRow("Microsoft.NET.Sdk.Razor", "tasks", "Microsoft.NET.Sdk.Razor.Tasks.dll")]
    [DataRow("Microsoft.NET.Sdk.StaticWebAssets", "tasks", "Microsoft.NET.Sdk.StaticWebAssets.Tasks.dll")]
    [DataRow("Microsoft.NET.Sdk.StaticWebAssets", "tools", "Microsoft.NET.Sdk.StaticWebAssets.Tool.dll")]
    [DataRow("Microsoft.NET.Sdk.BlazorWebAssembly", "tools", "Microsoft.NET.Sdk.BlazorWebAssembly.Tasks.dll")]
    [DataRow("Microsoft.NET.Sdk.BlazorWebAssembly", "tools", "Microsoft.NET.Sdk.BlazorWebAssembly.Tool.dll")]
    [DataRow("Microsoft.NET.Sdk.Publish", "tools", "Microsoft.NET.Sdk.Publish.Tasks.dll")]
    public void TaskAssembliesMatchTheSdkTargetFramework(string sdk, string folder, string assembly)
    {
        var assemblyPath = Path.Combine(
            SdkTestContext.Current.ToolsetUnderTest.SdksPath,
            sdk, folder, ToolsetInfo.SdkTargetFramework, assembly);

        var expectedFramework = NuGetFramework.Parse(ToolsetInfo.SdkTargetFramework).DotNetFrameworkName;
        AssemblyInfo.Get(assemblyPath).Should().Contain(("TargetFrameworkAttribute", expectedFramework));
    }

    [TestMethod]
    public void ApiCompatIsDeployedAlongsideTheSdkTasks()
    {
        var assemblyPath = Path.Combine(
            SdkTestContext.Current.ToolsetUnderTest.SdksPath,
            "Microsoft.NET.Sdk", "tools", ToolsetInfo.SdkTargetFramework, "Microsoft.DotNet.ApiCompat.Task.dll");

        File.Exists(assemblyPath).Should().BeTrue();
    }
}
