// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Mocks;

namespace Microsoft.TemplateEngine.Utils.UnitTests
{
    [TestClass]
    public class InMemoryFileSystemTests
    {
        [TestMethod]
        public void VerifyMultipleVirtualizationsAreHandled()
        {
            IPhysicalFileSystem mockFileSystem = new MockFileSystem();
            IPhysicalFileSystem virtualized1 = new InMemoryFileSystem(Directory.GetCurrentDirectory().CombinePaths("test1"), mockFileSystem);
            IPhysicalFileSystem virtualized2 = new InMemoryFileSystem(Directory.GetCurrentDirectory().CombinePaths("test2"), virtualized1);

            string testFilePath = Directory.GetCurrentDirectory().CombinePaths("test1", "test.txt");
            virtualized2.CreateFile(testFilePath).Dispose();
            Assert.IsFalse(mockFileSystem.FileExists(testFilePath));
            Assert.IsTrue(virtualized1.FileExists(testFilePath));
            Assert.IsTrue(virtualized2.FileExists(testFilePath));
        }
    }
}
