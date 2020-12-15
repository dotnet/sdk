using System;
using System.IO;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests.Utils
{
    internal static class TestHelper
    {
        internal static string CreateTemporaryFolder(string name = "")
        {
            string workingDir = Path.Combine(Path.GetTempPath(), "IDE.IntegrationTests", Guid.NewGuid().ToString(), name);
            Directory.CreateDirectory(workingDir);
            return workingDir;
        }

    }
}
