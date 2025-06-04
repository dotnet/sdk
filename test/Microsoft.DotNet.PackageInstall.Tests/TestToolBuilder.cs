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

        /// <summary>
        /// Compares the files in two directories (non-recursively) and returns true if they have the same files with identical text contents.
        /// </summary>
        /// <param name="dir1">First directory path</param>
        /// <param name="dir2">Second directory path</param>
        /// <returns>True if the directories have the same files with the same text contents, false otherwise.</returns>
        public static bool AreDirectoriesEqual(string dir1, string dir2)
        {
            if (!Directory.Exists(dir1) || !Directory.Exists(dir2))
                return false;

            var files1 = Directory.GetFiles(dir1);
            var files2 = Directory.GetFiles(dir2);

            if (files1.Length != files2.Length)
                return false;

            var fileNames1 = new HashSet<string>(Array.ConvertAll(files1, f => Path.GetFileName(f) ?? string.Empty), StringComparer.OrdinalIgnoreCase);
            var fileNames2 = new HashSet<string>(Array.ConvertAll(files2, f => Path.GetFileName(f) ?? string.Empty), StringComparer.OrdinalIgnoreCase);

            if (!fileNames1.SetEquals(fileNames2))
                return false;

            foreach (var fileName in fileNames1)
            {
                var filePath1 = Path.Combine(dir1, fileName);
                var filePath2 = Path.Combine(dir2, fileName);
                if (!File.Exists(filePath1) || !File.Exists(filePath2))
                    return false;
                var text1 = File.ReadAllText(filePath1);
                var text2 = File.ReadAllText(filePath2);
                if (!string.Equals(text1, text2, StringComparison.Ordinal))
                    return false;
            }
            return true;
        }
    }
}
