// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateEngine.Tests;

namespace Microsoft.TemplateEngine.Authoring.CLI.IntegrationTests
{
    public class ExportCommandTests : TestBase, IDisposable
    {
        private static readonly JsonDocumentOptions DocOptions = new() { AllowTrailingCommas = true, CommentHandling = JsonCommentHandling.Skip };
        private readonly string _workingDirectory;

        public ExportCommandTests()
        {
            _workingDirectory = Path.Combine(Path.GetTempPath(), "Microsoft.TemplateEngine.Authoring.CLI.IntegrationTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_workingDirectory);
        }

        public void Dispose()
        {
            Directory.Delete(_workingDirectory, true);
        }

        [Fact]
        public async Task LocFilesAreExported()
        {
            string[] exportedFiles = await RunTemplateLocalizer(
                GetTestTemplateJsonContent(),
                _workingDirectory,
                args: new string[] { "localize", "export", _workingDirectory });

            Assert.True(exportedFiles.Length > 0);
            Assert.All(exportedFiles, p =>
            {
                Assert.StartsWith("templatestrings.", Path.GetFileName(p));
                Assert.EndsWith(".json", p);
            });
        }

        [Fact]
        public async Task LocFilesAreExportedFirstTime()
        {
            string testTemplate = GetTestTemplateInTempDir("TemplateWithSourceName");
            int runResult = await Program.Main(new[] { "localize", "export", testTemplate });
            Assert.Equal(0, runResult);
            string[] exportedFiles;
            string expectedExportDirectory = Path.Combine(testTemplate, ".template.config", "localize");
            try
            {
                exportedFiles = Directory.GetFiles(expectedExportDirectory);
            }
            catch (DirectoryNotFoundException)
            {
                // Since no templates were created, it is normal that no directory was created.
                exportedFiles = [];
            }
            Assert.True(exportedFiles.Length > 0);
            Assert.All(exportedFiles, p =>
            {
                Assert.StartsWith("templatestrings.", Path.GetFileName(p));
                Assert.EndsWith(".json", p);
            });
        }

        [Fact]
        public async Task EnglishLocFilesAreOverwritten()
        {
            string testTemplate = GetTestTemplateInTempDir("TemplateWithSourceName");
            int runResult = await Program.Main(new[] { "localize", "export", testTemplate });
            Assert.Equal(0, runResult);
            string expectedExportDirectory = Path.Combine(testTemplate, ".template.config", "localize");
            string enLocFile = Path.Combine(expectedExportDirectory, "templatestrings.en.json");
            string deLocFile = Path.Combine(expectedExportDirectory, "templatestrings.de.json");
            Assert.True(File.Exists(enLocFile));
            Assert.True(File.Exists(deLocFile));
            var engJsonContent = JsonNode.Parse(File.ReadAllText(enLocFile), documentOptions: DocOptions)!.AsObject();
            var deJsonContent = JsonNode.Parse(File.ReadAllText(deLocFile), documentOptions: DocOptions)!.AsObject();
            Assert.Equal("Test Asset", engJsonContent["author"]?.ToString());
            Assert.Equal("Test Asset", deJsonContent["author"]?.ToString());

            //modify author property
            string baseConfig = Path.Combine(testTemplate, ".template.config", "template.json");
            var templateJsonContent = JsonNode.Parse(File.ReadAllText(baseConfig), documentOptions: DocOptions)!.AsObject();
            Assert.NotNull(templateJsonContent["author"]);
            templateJsonContent["author"] = "New Author";
            File.WriteAllText(baseConfig, templateJsonContent.ToJsonString());

            runResult = await Program.Main(new[] { "localize", "export", testTemplate });
            Assert.Equal(0, runResult);
            Assert.True(File.Exists(enLocFile));
            Assert.True(File.Exists(deLocFile));
            engJsonContent = JsonNode.Parse(File.ReadAllText(enLocFile), documentOptions: DocOptions)!.AsObject();
            deJsonContent = JsonNode.Parse(File.ReadAllText(deLocFile), documentOptions: DocOptions)!.AsObject();
            Assert.Equal("New Author", engJsonContent["author"]?.ToString());
            Assert.Equal("Test Asset", deJsonContent["author"]?.ToString());
        }

        [Fact]
        public async Task TemplateLanguageLocFilesAreOverwritten()
        {
            string testTemplate = GetTestTemplateInTempDir("TemplateWithSourceName");
            string baseConfig = Path.Combine(testTemplate, ".template.config", "template.json");
            var templateJsonContent = JsonNode.Parse(File.ReadAllText(baseConfig), documentOptions: DocOptions)!.AsObject();
            templateJsonContent.Insert(0, "authoringLanguage", "de");
            File.WriteAllText(baseConfig, templateJsonContent.ToJsonString());

            int runResult = await Program.Main(new[] { "localize", "export", testTemplate });
            Assert.Equal(0, runResult);
            string expectedExportDirectory = Path.Combine(testTemplate, ".template.config", "localize");
            string enLocFile = Path.Combine(expectedExportDirectory, "templatestrings.en.json");
            string deLocFile = Path.Combine(expectedExportDirectory, "templatestrings.de.json");
            Assert.True(File.Exists(enLocFile));
            Assert.True(File.Exists(deLocFile));
            var engJsonContent = JsonNode.Parse(File.ReadAllText(enLocFile), documentOptions: DocOptions)!.AsObject();
            var deJsonContent = JsonNode.Parse(File.ReadAllText(deLocFile), documentOptions: DocOptions)!.AsObject();
            Assert.Equal("Test Asset", engJsonContent["author"]?.ToString());
            Assert.Equal("Test Asset", deJsonContent["author"]?.ToString());

            //modify author property
            templateJsonContent = JsonNode.Parse(File.ReadAllText(baseConfig), documentOptions: DocOptions)!.AsObject();
            Assert.NotNull(templateJsonContent["author"]);
            templateJsonContent["author"] = "New Author";
            File.WriteAllText(baseConfig, templateJsonContent.ToJsonString());

            runResult = await Program.Main(new[] { "localize", "export", testTemplate });
            Assert.Equal(0, runResult);
            Assert.True(File.Exists(enLocFile));
            Assert.True(File.Exists(deLocFile));
            engJsonContent = JsonNode.Parse(File.ReadAllText(enLocFile), documentOptions: DocOptions)!.AsObject();
            deJsonContent = JsonNode.Parse(File.ReadAllText(deLocFile), documentOptions: DocOptions)!.AsObject();
            Assert.Equal("New Author", deJsonContent["author"]?.ToString());
            Assert.Equal("Test Asset", engJsonContent["author"]?.ToString());
        }

        [Fact]
        public async Task LocFilesAreNotExportedWithDryRun()
        {
            string[] exportedFiles = await RunTemplateLocalizer(
                GetTestTemplateJsonContent(),
                _workingDirectory,
                args: new string[] { "localize", "export", _workingDirectory, "--dry-run" });

            Assert.Empty(exportedFiles);
        }

        [Fact]
        public async Task LanguagesCanBeOverriden()
        {
            string[] exportedFiles = await RunTemplateLocalizer(
                GetTestTemplateJsonContent(),
                _workingDirectory,
                args: new string[] { "localize", "export", _workingDirectory, "--language", "tr", "de" });

            Assert.Equal(2, exportedFiles.Length);
            Assert.True(File.Exists(Path.Combine(_workingDirectory, "localize", "templatestrings.tr.json")));
            Assert.True(File.Exists(Path.Combine(_workingDirectory, "localize", "templatestrings.de.json")));
            Assert.False(File.Exists(Path.Combine(_workingDirectory, "localize", "templatestrings.es.json")));
        }

        [Fact]
        public async Task SubdirectoriesAreNotSearchedByDefault()
        {
            Directory.CreateDirectory(Path.Combine(_workingDirectory, "subdir"));
            Directory.CreateDirectory(Path.Combine(_workingDirectory, "subdir2"));

            string templateJson = GetTestTemplateJsonContent();
            await File.WriteAllTextAsync(Path.Combine(_workingDirectory, "subdir", "template.json"), templateJson, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(_workingDirectory, "subdir2", "template.json"), templateJson, TestContext.Current.CancellationToken);

            int runResult = await Program.Main(new string[] { "localize", "export", _workingDirectory, "--language", "es" });
            // Error: no templates found under the given folder.
            Assert.NotEqual(0, runResult);

            Assert.False(File.Exists(Path.Combine(_workingDirectory, "subdir", "localize", "es.templatestrings.json")));
            Assert.False(File.Exists(Path.Combine(_workingDirectory, "subdir2", "localize", "es.templatestrings.json")));
        }

        [Fact]
        public async Task SubdirectoriesCanBeSearched()
        {
            Directory.CreateDirectory(Path.Combine(_workingDirectory, "subdir", ".template.config"));
            Directory.CreateDirectory(Path.Combine(_workingDirectory, ".template.config"));

            string templateJson = GetTestTemplateJsonContent();
            await File.WriteAllTextAsync(Path.Combine(_workingDirectory, "subdir", ".template.config", "template.json"), templateJson, TestContext.Current.CancellationToken);
            await File.WriteAllTextAsync(Path.Combine(_workingDirectory, ".template.config", "template.json"), templateJson, TestContext.Current.CancellationToken);

            int runResult = await Program.Main(new string[] { "localize", "export", _workingDirectory, "--language", "es", "--recursive" });
            Assert.Equal(0, runResult);

            Assert.True(File.Exists(Path.Combine(_workingDirectory, "subdir", ".template.config", "localize", "templatestrings.es.json")));
            Assert.True(File.Exists(Path.Combine(_workingDirectory, ".template.config", "localize", "templatestrings.es.json")));
        }

        [Fact]
        public async Task SubdirectoriesWithoutTemplateConfigFileAreNotSearched()
        {
            Directory.CreateDirectory(Path.Combine(_workingDirectory, "subdir"));

            await File.WriteAllTextAsync(Path.Combine(_workingDirectory, "subdir", "template.json"), GetTestTemplateJsonContent(), TestContext.Current.CancellationToken);

            int runResult = await Program.Main(new string[] { "localize", "export", _workingDirectory, "--language", "es", "--recursive" });
            // Error: no templates found under the given folder.
            Assert.NotEqual(0, runResult);

            Assert.False(File.Exists(Path.Combine(_workingDirectory, "subdir", "localize", "es.templatestrings.json")));
        }

        /// <summary>
        /// Creates a template.json file with given content in the given directory.
        /// Runs the template localizer tool with given arguments.
        /// Returns all the files found under "localize" folder.
        /// </summary>
        private static async Task<string[]> RunTemplateLocalizer(string jsonContent, string directory, params string[] args)
        {
            await File.WriteAllTextAsync(Path.Combine(directory, "template.json"), jsonContent);

            int runResult = await Program.Main(args);
            Assert.Equal(0, runResult);

            string expectedExportDirectory = Path.Combine(directory, "localize");
            try
            {
                return Directory.GetFiles(expectedExportDirectory);
            }
            catch (DirectoryNotFoundException)
            {
                // Since no templates were created, it is normal that no directory was created.
                return [];
            }
        }

        private static string GetTestTemplateInTempDir(string templateName)
        {
            string templateLocation = GetTestTemplateLocation(templateName);
            string tmpLocation = TestUtils.CreateTemporaryFolder();
            TestUtils.DirectoryCopy(templateLocation, tmpLocation, true);
            return tmpLocation;
        }

        private static string GetTestTemplateJsonContent()
        {
            string templateJsonPath = Path.Combine(
                TestTemplatesLocation,
                "TemplateWithLocalization",
                ".template.config",
                "template.json");

            return File.ReadAllText(templateJsonPath);
        }
    }
}
