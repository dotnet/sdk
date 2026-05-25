// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.


using System.Runtime.CompilerServices;
using NuGet.Packaging.Signing;

namespace Microsoft.NET.Publish.Tests;

public class PublishNoRestoreTests : SdkTest
{
    public PublishNoRestoreTests(ITestOutputHelper log) : base(log)
    {
    }

    [Theory]
    [CombinatorialData]
    public void PublishTrimmed(bool specifyRuntimeIdentifier)
    {
        TestNoRestore("PublishTrimmed", specifyRuntimeIdentifier);
    }

    [Theory]
    [CombinatorialData]
    public void PublishSingleFile(bool specifyRuntimeIdentifier)
    {
        TestNoRestore("PublishSingleFile", specifyRuntimeIdentifier);
    }

    [Theory]
    [CombinatorialData]
    public void PublishAot(bool specifyRuntimeIdentifier)
    {
        TestNoRestore("PublishAot", specifyRuntimeIdentifier);
    }

    void TestNoRestore(string propertyToSet, bool specifyRuntimeIdentifier)
    {
        List<string> runtimeIdentifiers = new List<string>();
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            runtimeIdentifiers.Add("win-x64");
            runtimeIdentifiers.Add("win-x86");
        }
        else
        {
            runtimeIdentifiers.Add(RuntimeInformation.RuntimeIdentifier);
        }

        var testProject = new TestProject()
        {
            Name = propertyToSet,
            TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
            IsExe = true,
        };

        testProject.AdditionalProperties[propertyToSet] = "true";

        if (specifyRuntimeIdentifier)
        {
            testProject.AdditionalProperties["RuntimeIdentifiers"] = string.Join(';', runtimeIdentifiers);
        }

        var testProjectInstance = TestAssetsManager.CreateTestProject(testProject, testProject.Name, specifyRuntimeIdentifier.ToString());
        new RestoreCommand(testProjectInstance).Execute().Should().Pass();
        var publishCommand = new PublishCommand(testProjectInstance)
        {
            ShouldRestore = false
        };
        if (specifyRuntimeIdentifier)
        {
            foreach (var runtimeIdentifier in runtimeIdentifiers)
            {
                publishCommand.Execute($"/p:RuntimeIdentifier={runtimeIdentifier}").Should().Pass();
            }
        }
        else
        {
            publishCommand.Execute().Should().Pass();
        }
    }
}
