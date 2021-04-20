// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

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
            mnt.MockRoot.AddDirectory("subdir").AddFile("test.file", System.Array.Empty<byte>());
            Util.Orchestrator orchestrator = new Util.Orchestrator();
            orchestrator.Run(new MockGlobalRunSpec(), mnt.Root, @"c:\temp");
        }
    }
}
