using System.IO;
using Microsoft.TemplateEngine.Abstractions.PhysicalFileSystem;
using Microsoft.TemplateEngine.Mocks;
using Xunit;

namespace Microsoft.TemplateEngine.Utils.UnitTests
{
    public class InMemoryFileSystemTests
    {
        [Fact(DisplayName = nameof(VerifyMultipleVirtualizationsAreHandled))]
        public void VerifyMultipleVirtualizationsAreHandled()
        {
            IPhysicalFileSystem mockFileSystem = new MockFileSystem();
            IPhysicalFileSystem virtualized1 = new InMemoryFileSystem(Directory.GetCurrentDirectory().CombinePaths("test1"), mockFileSystem);
            IPhysicalFileSystem virtualized2 = new InMemoryFileSystem(Directory.GetCurrentDirectory().CombinePaths("test2"), virtualized1);

            string testFilePath = Directory.GetCurrentDirectory().CombinePaths("test1", "test.txt");
            virtualized2.CreateFile(testFilePath).Dispose();
            Assert.False(mockFileSystem.FileExists(testFilePath));
            Assert.True(virtualized1.FileExists(testFilePath));
            Assert.True(virtualized2.FileExists(testFilePath));
        }
    }
}
