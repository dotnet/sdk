// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class OrchestratorTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private IEngineEnvironmentSettings _engineEnvironmentSettings;
        private ILogger _logger;

        public OrchestratorTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _engineEnvironmentSettings = environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
            _logger = _engineEnvironmentSettings.Host.Logger;
        }

        [Fact(DisplayName = nameof(VerifyRun))]
        public void VerifyRun()
        {
            MockFileSystem fileSystem = new MockFileSystem();
            Util.Orchestrator orchestrator = new Util.Orchestrator(_logger, new MockFileSystem());
            MockMountPoint mnt = new MockMountPoint(_engineEnvironmentSettings);
            mnt.MockRoot.AddDirectory("subdir").AddFile("test.file", System.Array.Empty<byte>());
            orchestrator.Run(new MockGlobalRunSpec(), mnt.Root, @"c:\temp");
        }
    }
}
