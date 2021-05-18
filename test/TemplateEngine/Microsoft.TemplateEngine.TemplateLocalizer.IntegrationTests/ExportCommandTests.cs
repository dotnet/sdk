// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.IO;
using System.Threading.Tasks;
using Xunit;

namespace Microsoft.TemplateEngine.TemplateLocalizer.IntegrationTests
{
    public class ExportCommandTests : IDisposable
    {
        private string _workingDirectory;

        public ExportCommandTests()
        {
            _workingDirectory = Path.Combine(Path.GetTempPath(), "Microsoft.TemplateEngine.TemplateLocalizer.IntegrationTests", Path.GetRandomFileName());
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
                args: new string[] { "export", _workingDirectory })
                .ConfigureAwait(false);

            Assert.True(exportedFiles.Length > 0);
            Assert.All(exportedFiles, p =>
            {
                Assert.StartsWith("templatestrings.", Path.GetFileName(p));
                Assert.EndsWith(".json", p);
            });
        }

        [Fact]
        public async Task LocFilesAreNotExportedWithDryRun()
        {
            string[] exportedFiles = await RunTemplateLocalizer(
                GetTestTemplateJsonContent(),
                _workingDirectory,
                args: new string[] { "export", _workingDirectory, "--dry-run" })
                .ConfigureAwait(false);

            Assert.Empty(exportedFiles);
        }

        [Fact]
        public async Task LanguagesCanBeOverriden()
        {
            string[] exportedFiles = await RunTemplateLocalizer(
                GetTestTemplateJsonContent(),
                _workingDirectory,
                args: new string[] { "export", _workingDirectory, "--language", "tr" })
                .ConfigureAwait(false);

            Assert.Single(exportedFiles);
            Assert.True(File.Exists(Path.Combine(_workingDirectory, "localize", "templatestrings.tr.json")));
            Assert.False(File.Exists(Path.Combine(_workingDirectory, "localize", "templatestrings.es.json")));
        }

        [Fact]
        public async Task SubdirectoriesAreNotSearchedByDefault()
        {
            Directory.CreateDirectory(Path.Combine(_workingDirectory, "subdir"));
            Directory.CreateDirectory(Path.Combine(_workingDirectory, "subdir2"));

            string templateJson = GetTestTemplateJsonContent();
            await File.WriteAllTextAsync(Path.Combine(_workingDirectory, "subdir", "template.json"), templateJson).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(_workingDirectory, "subdir2", "template.json"), templateJson).ConfigureAwait(false);

            int runResult = await Program.Main(new string[] { "export", _workingDirectory, "--language", "es" }).ConfigureAwait(false);
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
            await File.WriteAllTextAsync(Path.Combine(_workingDirectory, "subdir", ".template.config", "template.json"), templateJson).ConfigureAwait(false);
            await File.WriteAllTextAsync(Path.Combine(_workingDirectory, ".template.config", "template.json"), templateJson).ConfigureAwait(false);

            int runResult = await Program.Main(new string[] { "export", _workingDirectory, "--language", "es", "--recursive" }).ConfigureAwait(false);
            Assert.Equal(0, runResult);

            Assert.True(File.Exists(Path.Combine(_workingDirectory, "subdir", ".template.config", "localize", "templatestrings.es.json")));
            Assert.True(File.Exists(Path.Combine(_workingDirectory, ".template.config", "localize", "templatestrings.es.json")));
        }

        [Fact]
        public async Task SubdirectoriesWithoutTemplateConfigFileAreNotSearched()
        {
            Directory.CreateDirectory(Path.Combine(_workingDirectory, "subdir"));

            await File.WriteAllTextAsync(Path.Combine(_workingDirectory, "subdir", "template.json"), GetTestTemplateJsonContent()).ConfigureAwait(false);

            int runResult = await Program.Main(new string[] { "export", _workingDirectory, "--language", "es", "--recursive" }).ConfigureAwait(false);
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
            await File.WriteAllTextAsync(Path.Combine(directory, "template.json"), jsonContent).ConfigureAwait(false);

            int runResult = await Program.Main(args).ConfigureAwait(false);
            Assert.Equal(0, runResult);

            string expectedExportDirectory = Path.Combine(directory, "localize");
            try
            {
                return Directory.GetFiles(expectedExportDirectory);
            }
            catch (DirectoryNotFoundException)
            {
                // Since no templates were created, it is normal that no directory was created.
                return Array.Empty<string>();
            }
        }

        private static string GetTestTemplateJsonContent()
        {
            string thisDir = Path.GetDirectoryName(typeof(ExportCommandTests).Assembly.Location);
            string templateJsonPath = Path.GetFullPath(Path.Combine(
                thisDir,
                "..",
                "..",
                "..",
                "..",
                "..",
                "test",
                "Microsoft.TemplateEngine.TestTemplates",
                "test_templates",
                "TemplateWithLocalization",
                ".template.config",
                "template.json"));

            return File.ReadAllText(templateJsonPath);
        }
    }
}
