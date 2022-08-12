// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    /// <summary>
    /// Test class for testing file manipulation aspects of template creation without installing the templates.
    /// This does not deal with parameters or variables, it's beyond the scope of this class.
    /// </summary>
    internal class TestTemplateSetup
    {
        private readonly string _configFile;

        private IEngineEnvironmentSettings _environmentSettings;

        private IDictionary<string, string> _sourceFiles;
        private readonly TemplateConfigModel _configModel;
        private string _sourceBaseDir;

        private IMountPoint _sourceMountPoint;

        /// <summary>
        /// Setup a template at the given mount point defined by the file names and contents in the sourceFiles.
        /// </summary>
        /// <param name="environment">The environment.</param>
        /// <param name="sourceBaseDir">
        ///     The directory the files will be created relative to.
        ///     It is strongly recommended that this directory is virtualized.
        /// </param>
        /// <param name="sourceFiles">
        ///     Keys are file paths / names relative to the mount point
        ///     Values are file contents. If null, an empty file is created.
        /// </param>
        public TestTemplateSetup(IEngineEnvironmentSettings environment, string sourceBaseDir, IDictionary<string, string> sourceFiles)
        {
            _environmentSettings = environment;
            _sourceFiles = sourceFiles;
            _sourceBaseDir = sourceBaseDir;
            _configFile = TestFileSystemHelper.DefaultConfigRelativePath;
        }

        public TestTemplateSetup(IEngineEnvironmentSettings environment, string sourceBaseDir, IDictionary<string, string> sourceFiles, TemplateConfigModel configModel)
        {
            _environmentSettings = environment;
            _sourceFiles = sourceFiles;
            _configModel = configModel;
            _sourceBaseDir = sourceBaseDir;
            _configFile = TestFileSystemHelper.DefaultConfigRelativePath;
        }

        private IMountPoint SourceMountPoint
        {
            get
            {
                if (_sourceMountPoint == null)
                {
                    _sourceMountPoint = TestFileSystemHelper.CreateMountPoint(_environmentSettings, _sourceBaseDir);
                }

                return _sourceMountPoint;
            }
        }

        public IFileSystemInfo InfoForSourceFile(string filePath)
        {
            return SourceMountPoint.FileSystemInfo(filePath);
        }

        public IFile FileInfoForSourceFile(string filePath)
        {
            return SourceMountPoint.FileInfo(filePath);
        }

        public void WriteSource()
        {
            TestFileSystemHelper.WriteTemplateSource(_environmentSettings, _sourceBaseDir, _sourceFiles);
        }

        public void InstantiateTemplate(string targetBaseDir, IVariableCollection variables = null)
        {
            if (variables == null)
            {
                variables = new VariableCollection();
            }

            IRunnableProjectConfig runnableConfig = GetConfig();

            runnableConfig.Evaluate(variables);

            MockGlobalRunSpec runSpec = new MockGlobalRunSpec();
            runSpec.RootVariableCollection = variables;
            IDirectory sourceDir = SourceMountPoint.DirectoryInfo("/");

            IOrchestrator basicOrchestrator = new Core.Util.Orchestrator(_environmentSettings.Host.Logger, _environmentSettings.Host.FileSystem);
            RunnableProjectOrchestrator orchestrator = new RunnableProjectOrchestrator(basicOrchestrator);

            foreach (FileSourceMatchInfo source in runnableConfig.Sources)
            {
                TemplateConfigTestHelpers.SetupFileSourceMatchersOnGlobalRunSpec(runSpec, source);
                string targetDirForSource = Path.Combine(targetBaseDir, source.Target);
                orchestrator.Run(runSpec, sourceDir, targetDirForSource);
            }
        }

        public IReadOnlyDictionary<string, IReadOnlyList<IFileChange2>> GetFileChanges(string targetBaseDir, IVariableCollection variables = null)
        {
            if (variables == null)
            {
                variables = new VariableCollection();
            }

            IRunnableProjectConfig runnableConfig = GetConfig();
            runnableConfig.Evaluate(variables);

            MockGlobalRunSpec runSpec = new MockGlobalRunSpec();
            IDirectory sourceDir = SourceMountPoint.DirectoryInfo("/");

            IOrchestrator basicOrchestrator = new Core.Util.Orchestrator(_environmentSettings.Host.Logger, _environmentSettings.Host.FileSystem);
            RunnableProjectOrchestrator orchestrator = new RunnableProjectOrchestrator(basicOrchestrator);

            Dictionary<string, IReadOnlyList<IFileChange2>> changesByTarget = new Dictionary<string, IReadOnlyList<IFileChange2>>();

            foreach (FileSourceMatchInfo source in runnableConfig.Sources)
            {
                TemplateConfigTestHelpers.SetupFileSourceMatchersOnGlobalRunSpec(runSpec, source);
                string targetDirForSource = Path.Combine(targetBaseDir, source.Target);
                IReadOnlyList<IFileChange2> changes = orchestrator.GetFileChanges(runSpec, sourceDir, targetDirForSource);
                changesByTarget[source.Target] = changes;
            }

            return changesByTarget;
        }

        public IReadOnlyDictionary<string, string> GetRenames(string sourceDir, string targetBaseDir, IVariableCollection variables, IReadOnlyList<IReplacementTokens> symbolBasedRenames)
        {
            IFileSystemInfo configFileInfo = SourceMountPoint.FileInfo(_configFile ?? TestFileSystemHelper.DefaultConfigRelativePath);
            object resolvedNameValue = variables["name"];
            return FileRenameGenerator.AugmentFileRenames(_environmentSettings, _sourceBaseDir, configFileInfo, sourceDir, ref targetBaseDir, resolvedNameValue, variables, new Dictionary<string, string>(), symbolBasedRenames);
        }

        public void AddFile(string filename, string content = null)
        {
            _sourceFiles.Add(filename, content ?? string.Empty);
        }

        public void AddFileMapping(IReadOnlyDictionary<string, string> fileMap)
        {
            foreach (KeyValuePair<string, string> fileInfo in fileMap)
            {
                AddFile(fileInfo.Key, fileInfo.Value);
            }
        }

        private IRunnableProjectConfig GetConfig()
        {
            string configPath = _configFile ?? TestFileSystemHelper.DefaultConfigRelativePath;

            return _configModel == null
                ? new RunnableProjectConfig(_environmentSettings, A.Fake<IGenerator>(), SourceMountPoint.FileInfo(configPath))
                : new RunnableProjectConfig(_environmentSettings, A.Fake<IGenerator>(), _configModel, SourceMountPoint.FileInfo(configPath));
        }
    }
}
