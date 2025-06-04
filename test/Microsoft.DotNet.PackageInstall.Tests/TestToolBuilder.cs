// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.DotNet.PackageInstall.Tests
{
    [CollectionDefinition(nameof(TestToolBuilderCollection))]
    public class TestToolBuilderCollection : ICollectionFixture<TestToolBuilder>
    {
        // This class is intentionally left empty.
    }

    public class TestToolBuilder
    {

        public string CreateTestTool(ITestOutputHelper log)
        {
            


            var targetDirectory = Path.Combine(TestContext.Current.TestExecutionDirectory, "TestTool");
            Directory.CreateDirectory(targetDirectory);

            var testProject = new TestProject("TestTool")
            {
                TargetFrameworks = ToolsetInfo.CurrentTargetFramework,
                IsExe = true,
            };
            testProject.AdditionalProperties["PackaAsTool"] = "true";
            testProject.AdditionalProperties["ToolCommandName"] = "TestTool";

            testProject.SourceFiles.Add("Program.cs", "Console.WriteLine(\"Hello Tool!\");");

            var testAssetManager = new TestAssetsManager(log);
            var testAsset = testAssetManager.CreateTestProject(testProject);






            throw new NotImplementedException(testAsset.Path);
        }

    }
}
