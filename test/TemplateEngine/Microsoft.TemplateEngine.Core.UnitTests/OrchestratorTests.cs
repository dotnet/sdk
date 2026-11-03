// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    [TestClass]
    public class OrchestratorTests : TestBase
    {
        private static EnvironmentSettingsHelper s_environmentSettingsHelper = null!;
        private readonly IEngineEnvironmentSettings _engineEnvironmentSettings;
        private readonly ILogger _logger;

        [ClassInitialize]
        public static void ClassInitialize(TestContext _)
            => s_environmentSettingsHelper = new EnvironmentSettingsHelper();

        [ClassCleanup]
        public static void ClassCleanup() => s_environmentSettingsHelper?.Dispose();

        public OrchestratorTests()
        {
            _engineEnvironmentSettings = CreateEnvironment(s_environmentSettingsHelper, GetType().Name);
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

        [TestMethod]
        public void TryGetBufferSize_KnownLengthFile_UsesLengthBelowDefault()
        {
            TestOrchestrator orchestrator = new(_logger, new MockFileSystem());

            bool found = orchestrator.TryGetBufferSize(new KnownLengthFile(32), out int bufferSize);

            Assert.IsTrue(found);
            Assert.AreEqual(32, bufferSize);
        }

        [TestMethod]
        public void TryGetBufferSize_KnownLengthFile_DoesNotIncreaseDefault()
        {
            TestOrchestrator orchestrator = new(_logger, new MockFileSystem());

            bool found = orchestrator.TryGetBufferSize(new KnownLengthFile(Util.Processor.DefaultBufferSize), out int bufferSize);

            Assert.IsFalse(found);
            Assert.AreEqual(-1, bufferSize);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(3)]
        public void TryGetBufferSize_KnownLengthFile_UsesMinimumBufferSize(long length)
        {
            TestOrchestrator orchestrator = new(_logger, new MockFileSystem());

            bool found = orchestrator.TryGetBufferSize(new KnownLengthFile(length), out int bufferSize);

            Assert.IsTrue(found);
            Assert.AreEqual(4, bufferSize);
        }

        [TestMethod]
        [DataRow(-1)]
        [DataRow(Util.Processor.DefaultBufferSize + 1L)]
        public void TryGetBufferSize_KnownLengthFile_InvalidLengthUsesDefault(long length)
        {
            TestOrchestrator orchestrator = new(_logger, new MockFileSystem());

            bool found = orchestrator.TryGetBufferSize(new KnownLengthFile(length), out int bufferSize);

            Assert.IsFalse(found);
            Assert.AreEqual(-1, bufferSize);
        }

        [TestMethod]
        public void TryGetBufferSize_KnownLengthFile_ThrowingLengthUsesDefault()
        {
            TestOrchestrator orchestrator = new(_logger, new MockFileSystem());

            bool found = orchestrator.TryGetBufferSize(new KnownLengthFile(throwOnAccess: true), out int bufferSize);

            Assert.IsFalse(found);
            Assert.AreEqual(-1, bufferSize);
        }

        private sealed class TestOrchestrator(ILogger logger, MockFileSystem fileSystem) : Util.Orchestrator(logger, fileSystem)
        {
            public new bool TryGetBufferSize(IFile sourceFile, out int bufferSize)
                => base.TryGetBufferSize(sourceFile, out bufferSize);
        }

        private sealed class KnownLengthFile(long length = 0, bool throwOnAccess = false) : IKnownLengthFile
        {
            public bool Exists => true;

            public string FullPath => "/test";

            public FileSystemInfoKind Kind => FileSystemInfoKind.File;

            public IDirectory? Parent => null;

            public string Name => "test";

            public IMountPoint MountPoint => null!;

            public long Length => throwOnAccess ? throw new IOException() : length;

            public Stream OpenRead() => Stream.Null;
        }
    }
}
