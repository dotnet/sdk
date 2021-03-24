using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Utils;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class OrchestratorTests
    {
        [Fact(DisplayName = nameof(VerifyRun))]
        public void VerifyRun()
        {
            TestHost host = new TestHost
            {
                HostIdentifier = "TestRunner",
                Version = "1.0.0.0",
            };

            host.FileSystem = new MockFileSystem();
            var environmentSettings = new EngineEnvironmentSettings(host, x => null);
            MockMountPoint mnt = new MockMountPoint(environmentSettings);
            mnt.MockRoot.AddDirectory("subdir").AddFile("test.file", new byte[0]);
            Util.Orchestrator orchestrator = new Util.Orchestrator();
            orchestrator.Run(new MockGlobalRunSpec(), mnt.Root, @"c:\temp");
        }
    }
}
