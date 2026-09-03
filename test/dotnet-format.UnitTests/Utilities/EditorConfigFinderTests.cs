// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Tools.Utilities;

namespace Microsoft.CodeAnalysis.Tools.Tests.Utilities
{
    [TestClass]
    public class EditorConfigFinderTests
    {
        private static string CreateTempDirectoryTree()
        {
            // temp/
            //   .editorconfig
            //   src/
            //     .editorconfig
            //     Project/
            //       File.cs
            //   sibling/
            //     .editorconfig      <- must NOT be returned for src/Project/File.cs
            var root = Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString());
            Directory.CreateDirectory(Path.Combine(root, "src", "Project"));
            Directory.CreateDirectory(Path.Combine(root, "sibling"));
            File.WriteAllText(Path.Combine(root, ".editorconfig"), "root = true");
            File.WriteAllText(Path.Combine(root, "src", ".editorconfig"), "");
            File.WriteAllText(Path.Combine(root, "sibling", ".editorconfig"), "");
            File.WriteAllText(Path.Combine(root, "src", "Project", "File.cs"), "class C { }");
            return root;
        }

        [TestMethod]
        public void GetEditorConfigPathsForFiles_ReturnsAncestorConfigs()
        {
            var root = CreateTempDirectoryTree();
            try
            {
                var filePath = Path.Combine(root, "src", "Project", "File.cs");

                var paths = EditorConfigFinder.GetEditorConfigPathsForFiles(ImmutableArray.Create(filePath));

                Assert.Contains(Path.Combine(root, ".editorconfig"), paths);
                Assert.Contains(Path.Combine(root, "src", ".editorconfig"), paths);
                Assert.DoesNotContain(Path.Combine(root, "sibling", ".editorconfig"), paths);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }

        [TestMethod]
        public void GetEditorConfigPathsForFiles_MultipleFiles_NoDuplicates()
        {
            var root = CreateTempDirectoryTree();
            try
            {
                var fileA = Path.Combine(root, "src", "Project", "File.cs");
                var fileB = Path.Combine(root, "src", "Project", "Other.cs");
                File.WriteAllText(fileB, "class D { }");

                var paths = EditorConfigFinder.GetEditorConfigPathsForFiles(ImmutableArray.Create(fileA, fileB));

                var totalCount = paths.Length;
                var distinctCount = paths.Distinct().Count();
                Assert.AreEqual(totalCount, distinctCount);
                Assert.Contains(Path.Combine(root, ".editorconfig"), paths);
            }
            finally
            {
                Directory.Delete(root, recursive: true);
            }
        }
    }
}
