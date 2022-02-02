// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core.UnitTests
{
    public class StringUpdaterTests : IDisposable
    {
        private static readonly IReadOnlyList<TemplateString> InputStrings = new List<TemplateString>()
        {
            new("..name", "name", "Class library"),
            new("..description", "description", "dEscRiPtiON: ,./|\\<>{}!@#$%^&*()_+-=? 12 äÄßöÖüÜçÇğĞıIİşŞ"),
            new("..symbols.targetframeworkoverride.displayname", "symbols.TargetFrameworkOverride.displayName", "tfm display name"),
            new("..symbols.targetframeworkoverride.description", "symbols.TargetFrameworkOverride.description", "tfm description"),
            new("..symbols.framework.displayname", "symbols.Framework.displayName", "framework display name"),
            new("..symbols.framework.description", "symbols.Framework.description", "framework description"),
            new("..symbols.framework.choices.0.displayname", "symbols.Framework.choices.net5_0.displayName", "net5.0 display name"),
            new("..symbols.framework.choices.0.description", "symbols.Framework.choices.net5_0.description", "Target net5.0"),
            new("..symbols.framework.choices.1.description", "symbols.Framework.choices.netstandard2_1.description", "Target netstandard2.1"),
            new("..symbols.framework.choices.2.displayname", "symbols.Framework.choices.netstandard2_0.displayName", "netstandard2.0 display name"),
            new("..symbols.framework.choices.2.description", "symbols.Framework.choices.netstandard2_0.description", "Target netstandard2.0"),
            new("..postactions.0.description", "postActions[0].description", "Restore NuGet packages required by this project."),
            new("..postactions.0.manualinstructions.0.text", "postActions[0].manualInstructions[0].text", "Run 'dotnet restore'"),
            new("..postactions.1.description", "postActions[1].description", "Opens Class1.cs in the editor")
        };

        private string _workingDirectory;

        public StringUpdaterTests()
        {
            _workingDirectory = Path.Combine(Path.GetTempPath(), "Microsoft.TemplateEngine.TemplateLocalizer.Core.UnitTests", Path.GetRandomFileName());
            Directory.CreateDirectory(_workingDirectory);
        }

        public void Dispose()
        {
            Directory.Delete(_workingDirectory, true);
        }

        [Fact]
        public async Task AllStringsAreWrittenToFile()
        {
            CancellationTokenSource cts = new CancellationTokenSource(10000);
            await TemplateStringUpdater.UpdateStringsAsync(InputStrings, "en", new string[] { "tr" }, _workingDirectory, dryRun: false, NullLogger.Instance, cts.Token)
                .ConfigureAwait(false);

            string expectedFilename = Path.Combine(_workingDirectory, "templatestrings.tr.json");
            Assert.True(File.Exists(expectedFilename));

            // Only the specified language file should be generated (no more than 1 file in the directory).
            Assert.Single(Directory.EnumerateFileSystemEntries(_workingDirectory));

            Dictionary<string, string> resultStrings = await ReadTemplateStringsFromJsonFile(expectedFilename, cts.Token).ConfigureAwait(false);

            // All the InputStrings should be in the resultStrings
            Assert.True(InputStrings.All(i => resultStrings.TryGetValue(i.LocalizationKey, out var value) && value == i.Value));
            Assert.All(InputStrings, i =>
            {
                Assert.Contains(i.LocalizationKey, (IDictionary<string, string>)resultStrings);
                Assert.Equal(i.Value, resultStrings[i.LocalizationKey]);
            });
        }

        [Fact]
        public async Task DryRunPreventsWritingToFileSystem()
        {
            CancellationTokenSource cts = new CancellationTokenSource(10000);
            await TemplateStringUpdater.UpdateStringsAsync(InputStrings, "en", new string[] { "tr" }, _workingDirectory, dryRun: true, NullLogger.Instance, cts.Token)
                .ConfigureAwait(false);

            string expectedFilename = Path.Combine(_workingDirectory, "templatestrings.tr.json");
            Assert.False(File.Exists(expectedFilename));
        }

        [Fact]
        public async Task StringOrderIsPreserved()
        {
            CancellationTokenSource cts = new CancellationTokenSource(10000);
            await TemplateStringUpdater.UpdateStringsAsync(InputStrings, "en", new string[] { "tr" }, _workingDirectory, dryRun: false, NullLogger.Instance, cts.Token)
                .ConfigureAwait(false);

            string expectedFilename = Path.Combine(_workingDirectory, "templatestrings.tr.json");
            Assert.True(File.Exists(expectedFilename));

            using FileStream locFileStream = File.OpenRead(expectedFilename);
            JsonDocument locFile = await JsonDocument.ParseAsync(locFileStream).ConfigureAwait(false);

            int inputIndex = 0;
            foreach (JsonProperty property in locFile.RootElement.EnumerateObject())
            {
                if (inputIndex >= InputStrings.Count)
                {
                    return;
                }

                Assert.Equal(InputStrings[inputIndex].LocalizationKey, property.Name);
                Assert.Equal(InputStrings[inputIndex].Value, property.Value.GetString());
                inputIndex++;
            }
        }

        [Fact]
        public async Task ExistingTranslationsAndCommentsArePreserved()
        {
            CancellationTokenSource cts = new CancellationTokenSource(10000);
            string expectedFilename = Path.Combine(_workingDirectory, "templatestrings.tr.json");

            await File.WriteAllTextAsync(
                expectedFilename,
                @"
{
    ""name"": ""existing translations should be preserved."",
    ""_name.comment"": ""comments should be preserved.""
}",
                cts.Token).ConfigureAwait(false);

            await TemplateStringUpdater.UpdateStringsAsync(InputStrings, "en", new string[] { "tr" }, _workingDirectory, dryRun: false, NullLogger.Instance, cts.Token)
                .ConfigureAwait(false);

            string fileContent = await File.ReadAllTextAsync(expectedFilename, cts.Token).ConfigureAwait(false);

            Assert.Contains("existing translations should be preserved.", fileContent);
            Assert.Contains("comments should be preserved.", fileContent);
        }

        [Fact]
        public async Task ExistingValuesOfAuthoringLanguageShouldBeRemoved()
        {
            CancellationTokenSource cts = new CancellationTokenSource(10000);
            string expectedFilename = Path.Combine(_workingDirectory, "templatestrings.en.json");

            await File.WriteAllTextAsync(
                expectedFilename,
                @"
{
    ""name"": ""existing translations in authoring language should be removed.""
}",
                cts.Token).ConfigureAwait(false);

            await TemplateStringUpdater.UpdateStringsAsync(InputStrings, "en", new string[] { "en" }, _workingDirectory, dryRun: false, NullLogger.Instance, cts.Token)
                .ConfigureAwait(false);

            string fileContent = await File.ReadAllTextAsync(expectedFilename, cts.Token).ConfigureAwait(false);

            Assert.DoesNotContain("existing translations in authoring language should be removed.", fileContent);
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task UnchangedFileShouldntBeOverwritten(bool fileStartsWithBom)
        {
            CancellationTokenSource cts = new CancellationTokenSource(10000);
            string expectedFilename = Path.Combine(_workingDirectory, "templatestrings.fr.json");

            // Manually create a json to be observed.
            JsonWriterOptions writerOptions = new JsonWriterOptions()
            {
                Encoder = new ExtendedJavascriptEncoder(),
                Indented = true,
            };
            using (FileStream fileStream = new FileStream(expectedFilename, FileMode.Create, FileAccess.Write))
            {
                if (fileStartsWithBom)
                {
                    byte[] bom = new UTF8Encoding(true).GetPreamble();
                    fileStream.Write(bom, 0, bom.Length);
                }

                using Utf8JsonWriter jsonWriter = new Utf8JsonWriter(fileStream, writerOptions);

                jsonWriter.WriteStartObject();

                foreach (TemplateString locString in InputStrings)
                {
                    jsonWriter.WritePropertyName(locString.LocalizationKey);
                    jsonWriter.WriteStringValue(locString.Value);
                }

                jsonWriter.WriteEndObject();
                await jsonWriter.FlushAsync(cts.Token).ConfigureAwait(false);
            }

            // Open the file and allow subsequent readings, but prevent writing.
            // If something attempts to write to the file, it will get IOException.
            using FileStream fileLock = new FileStream(expectedFilename, FileMode.Open, FileAccess.Read, FileShare.Read);

            // Attempt to update the previously created json to see if it will be overwritten.
            // The content is identical. So we can read, but we shouldn't write to the file after this point.
            await TemplateStringUpdater.UpdateStringsAsync(InputStrings, "en", new string[] { "fr" }, _workingDirectory, dryRun: false, NullLogger.Instance, cts.Token)
                .ConfigureAwait(false);

            // An exception will be thrown, failing the test, if the call above tries to write to the file.
            // The execution should reach this point if the call did not try to write to the file, which indicates success for the test.
        }

        [Fact]
        public async Task ExistingCommentsOfAuthoringLanguageArePreserved()
        {
            CancellationTokenSource cts = new CancellationTokenSource(10000);
            string expectedFilename = Path.Combine(_workingDirectory, "templatestrings.en.json");

            await File.WriteAllTextAsync(
                expectedFilename,
                @"
{
    ""name"": ""existing translations should be discarded."",
    ""_name.comment"": ""comments should be preserved.""
}",
                cts.Token).ConfigureAwait(false);

            await TemplateStringUpdater.UpdateStringsAsync(InputStrings, "en", new string[] { "en" }, _workingDirectory, dryRun: false, NullLogger.Instance, cts.Token)
                .ConfigureAwait(false);

            string fileContent = await File.ReadAllTextAsync(expectedFilename, cts.Token).ConfigureAwait(false);

            Assert.DoesNotContain("existing translations should be discarded.", fileContent);
            Assert.Contains("Class library", fileContent);
            Assert.Contains("_name.comment", fileContent);
            Assert.Contains("comments should be preserved.", fileContent);
        }

        [Fact]
        public async Task AllowedCharactersAreNotEscaped()
        {
            List<TemplateString> locStrings = new List<TemplateString>()
            {
                // No-break space shouldn't be escaped.
                new TemplateString("..name", "name", "\u00A0")
            };

            CancellationTokenSource cts = new CancellationTokenSource(10000);
            await TemplateStringUpdater.UpdateStringsAsync(
                locStrings,
                templateJsonLanguage: "en",
                languages: new string[] { "it" },
                _workingDirectory,
                dryRun: false,
                NullLogger.Instance,
                cts.Token)
                .ConfigureAwait(false);

            string expectedFilename = Path.Combine(_workingDirectory, "templatestrings.it.json");
            string fileContent = File.ReadAllText(expectedFilename);

            Assert.Contains("\u00A0", fileContent);
            Assert.DoesNotContain("\\u00A0", fileContent, StringComparison.OrdinalIgnoreCase);

            Dictionary<string, string> resultStrings =
                await ReadTemplateStringsFromJsonFile(expectedFilename, cts.Token)
                .ConfigureAwait(false);

            Assert.Equal(locStrings.Count, resultStrings.Count);
            Assert.All(locStrings, x => Assert.Contains(x.Value, resultStrings.Values));
        }

        [Theory]
        [InlineData(false)]
        [InlineData(true)]
        public async Task BomPreambleIsPreserved(bool fileStartsWithBom)
        {
            CancellationTokenSource cts = new CancellationTokenSource(10000);
            string expectedFilename = Path.Combine(_workingDirectory, "templatestrings.en.json");

            await File.WriteAllTextAsync(
                expectedFilename,
                @"
{
    ""name"": ""translation""
}",
                new UTF8Encoding(fileStartsWithBom),
                cts.Token).ConfigureAwait(false);

            await TemplateStringUpdater.UpdateStringsAsync(InputStrings, "en", new string[] { "en" }, _workingDirectory, dryRun: false, NullLogger.Instance, cts.Token)
                .ConfigureAwait(false);

            byte[] fileContent = await File.ReadAllBytesAsync(expectedFilename, cts.Token).ConfigureAwait(false);
            Assert.Equal(fileStartsWithBom, fileContent.AsSpan().StartsWith(new UTF8Encoding(true).Preamble));
        }

        private static async Task<Dictionary<string, string>> ReadTemplateStringsFromJsonFile(string path, CancellationToken cancellationToken)
        {
            using FileStream openStream = File.OpenRead(path);
            JsonSerializerOptions serializerOptions = new()
            {
                AllowTrailingCommas = true,
                MaxDepth = 1,
            };

            return await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(openStream, serializerOptions, cancellationToken)
                .ConfigureAwait(false)
                ?? new Dictionary<string, string>();
        }
    }
}
