// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    [TestClass]
    public class OrchestratorTests
    {
        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;
        private readonly ILogger _logger;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper(NullMessageSink.Instance);

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        public OrchestratorTests()
        {
            _engineEnvironmentSettings = s_environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
            _logger = _engineEnvironmentSettings.Host.Logger;
        }

        [TestMethod]
        public void VerifyRun()
        {
            Util.Orchestrator orchestrator = new Util.Orchestrator(_logger, new MockFileSystem());
            MockMountPoint mnt = new MockMountPoint(_engineEnvironmentSettings);
            mnt.MockRoot.AddDirectory("subdir").AddFile("test.file", []);
            orchestrator.Run(new MockGlobalRunSpec(), mnt.Root, @"c:\temp");
        }
    }
}
