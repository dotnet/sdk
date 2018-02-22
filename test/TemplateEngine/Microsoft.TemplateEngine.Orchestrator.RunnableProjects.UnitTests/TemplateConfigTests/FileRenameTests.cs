using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.UnitTests.TemplateConfigTests
{
    public class FileRenameTests
    {
        [Fact(DisplayName = nameof(SourceRenameIsCaseSensitive))]
        public void SourceRenameIsCaseSensitive()
        {
            IEngineEnvironmentSettings environment = TemplateConfigTestHelpers.GetTestEnvironment();
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(environment);

            TestTemplateSetup setup = SetupSourceRenameIsCaseSensitveTestTemplate(environment, sourceBasePath);
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(environment);
            IReadOnlyDictionary<string, IReadOnlyList<IFileChange>> allChanges = setup.GetFileChanges(targetDir);

            Assert.Equal(1, allChanges.Count);  // one source had changes
            Assert.True(allChanges.TryGetValue("./", out IReadOnlyList<IFileChange> changes), "No changes for source './'");

            Assert.Equal(2, changes.Count);
            Assert.Equal(1, changes.Count(x => string.Equals(x.TargetRelativePath, "YesNewName.txt", StringComparison.Ordinal)));
            Assert.Equal(1, changes.Count(x => string.Equals(x.TargetRelativePath, "dontrenameme.txt", StringComparison.Ordinal)));
            Assert.Equal(0, changes.Count(x => string.Equals(x.TargetRelativePath, "NoNewName.txt", StringComparison.Ordinal)));
        }

        private static TestTemplateSetup SetupSourceRenameIsCaseSensitveTestTemplate(IEngineEnvironmentSettings environment, string basePath)
        {
            string sourceConfig = @"
{
  ""sources"": [
    {
      ""rename"": {
        ""RenameMe.txt"": ""YesNewName.txt"",
        ""DontRenameMe.txt"": ""NoNewName.txt""
      },
    }
  ]
}";

            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();
            // template.json
            templateSourceFiles.Add(TemplateConfigTestHelpers.DefaultConfigRelativePath, sourceConfig);
            // content
            templateSourceFiles.Add("RenameMe.txt", null);
            templateSourceFiles.Add("dontrenameme.txt", null);
            TestTemplateSetup setup = new TestTemplateSetup(environment, basePath, templateSourceFiles);
            setup.WriteSource();
            return setup;
        }

        [Fact(DisplayName = nameof(SourceModifierRenameIsCaseSensitive))]
        public void SourceModifierRenameIsCaseSensitive()
        {
            IEngineEnvironmentSettings environment = TemplateConfigTestHelpers.GetTestEnvironment();
            string sourceBasePath = FileSystemHelpers.GetNewVirtualizedPath(environment);

            TestTemplateSetup setup = SetupSourceModifierRenameIsCaseSensitiveTestTemplate(environment, sourceBasePath);
            string targetDir = FileSystemHelpers.GetNewVirtualizedPath(environment);
            IReadOnlyDictionary<string, IReadOnlyList<IFileChange>> allChanges = setup.GetFileChanges(targetDir);

            Assert.Equal(1, allChanges.Count);  // one source had changes
            Assert.True(allChanges.TryGetValue("./", out IReadOnlyList<IFileChange> changes), "No changes for source './'");

            Assert.Equal(2, changes.Count);
            Assert.Equal(1, changes.Count(x => string.Equals(x.TargetRelativePath, "YesNewName.txt", StringComparison.Ordinal)));
            Assert.Equal(1, changes.Count(x => string.Equals(x.TargetRelativePath, "dontrenameme.txt", StringComparison.Ordinal)));
            Assert.Equal(0, changes.Count(x => string.Equals(x.TargetRelativePath, "NoNewName.txt", StringComparison.Ordinal)));
        }

        private static TestTemplateSetup SetupSourceModifierRenameIsCaseSensitiveTestTemplate(IEngineEnvironmentSettings environment, string basePath)
        {
            string sourceConfig = @"
{
  ""sources"": [
    {
      ""modifiers"": [
        {
          ""rename"": {
            ""RenameMe.txt"": ""YesNewName.txt"",
            ""DontRenameMe.txt"": ""NoNewName.txt"",
          }
        }
      ]
    }
  ]
}
";

            IDictionary<string, string> templateSourceFiles = new Dictionary<string, string>();
            // template.json
            templateSourceFiles.Add(TemplateConfigTestHelpers.DefaultConfigRelativePath, sourceConfig);
            // content
            templateSourceFiles.Add("RenameMe.txt", null);
            templateSourceFiles.Add("dontrenameme.txt", null);
            TestTemplateSetup setup = new TestTemplateSetup(environment, basePath, templateSourceFiles);
            setup.WriteSource();
            return setup;
        }
    }
}
