// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core
{
    internal static class TemplateStringUpdater
    {
        /// <summary>
        /// Updates the templatestrings.json files for given languages with the provided strings.
        /// </summary>
        /// <param name="strings">Template strings to be included in the templatestrings.json files.
        /// These strings are typicall extracted from template.json file using <see cref="TemplateStringExtractor"/>.</param>
        /// <param name="templateJsonLanguage">The language of the <paramref name="strings"/> as declared in the template.json file.</param>
        /// <param name="languages">The list of languages for which templatestrings.json file will be created.</param>
        /// <param name="targetDirectory">The directory that will contain the generated templatestrings.json files.</param>
        /// <param name="dryRun">If true, the changes will not be written to file system.</param>
        /// <param name="logger"><see cref="ILogger"/> to be used for logging.</param>
        /// <param name="cancellationToken">Cancellation token to cancel the operation.</param>
        /// <returns>The task that tracks the status of the async operation.</returns>
        public static async Task UpdateStringsAsync(
            IEnumerable<TemplateString> strings,
            string templateJsonLanguage,
            IEnumerable<string> languages,
            string targetDirectory,
            bool dryRun,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            if (!dryRun)
            {
                Directory.CreateDirectory(targetDirectory);
            }

            foreach (string language in languages)
            {
                string locFilePath = Path.Combine(targetDirectory, "templatestrings." + language + ".json");

                Dictionary<string, string>? existingStrings = await GetExistingStringsAsync(locFilePath, logger, cancellationToken)
                    .ConfigureAwait(false);

                if (!dryRun)
                {
                    // Ignore existing translations for the original language, only keep the comments.
                    bool forceUpdate = language == templateJsonLanguage;
                    await SaveTemplateStringsFileAsync(strings, existingStrings, locFilePath, forceUpdate, logger, cancellationToken)
                        .ConfigureAwait(false);
                }
            }
        }

        private static async Task<Dictionary<string, string>> GetExistingStringsAsync(string locFilePath, ILogger logger, CancellationToken cancellationToken)
        {
            try
            {
                logger.LogDebug(LocalizableStrings.stringUpdater_log_loadingLocFile, locFilePath);
                using FileStream openStream = File.OpenRead(locFilePath);

                JsonSerializerOptions serializerOptions = new()
                {
                    AllowTrailingCommas = true,
                    MaxDepth = 1,
                };

                return await JsonSerializer.DeserializeAsync<Dictionary<string, string>>(openStream, serializerOptions, cancellationToken)
                    .ConfigureAwait(false)
                    ?? new Dictionary<string, string>();
            }
            catch (IOException ex) when (ex is DirectoryNotFoundException || ex is FileNotFoundException)
            {
                // templatestrings.json file doesn't exist. It will be created from scratch.
                return new();
            }
            catch (Exception)
            {
                logger.LogError(LocalizableStrings.stringUpdater_log_failedToReadLocFile, locFilePath);
                throw;
            }
        }

        private static async Task SaveTemplateStringsFileAsync(
            IEnumerable<TemplateString> templateStrings,
            Dictionary<string, string>? existingStrings,
            string filePath,
            bool forceUpdate,
            ILogger logger,
            CancellationToken cancellationToken)
        {
            JsonWriterOptions writerOptions = new JsonWriterOptions()
            {
                // Allow unescaped characters in the strings. This allows writing "aren't" instead of "aren\u0027t".
                // This is only considered unsafe in a context where symbols may be interpreted as special characters.
                // For instance, '<' character should be escaped in html documents where this json will be embedded.
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                Indented = true,
            };
            logger.LogDebug(LocalizableStrings.stringUpdater_log_openingTemplatesJson, filePath);
            using FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write);
            using Utf8JsonWriter jsonWriter = new Utf8JsonWriter(fileStream, writerOptions);

            jsonWriter.WriteStartObject();

            foreach (TemplateString templateString in templateStrings)
            {
                string? localizedText = null;
                if (!forceUpdate && (existingStrings?.TryGetValue(templateString.LocalizationKey, out localizedText) ?? false))
                {
                    logger.LogDebug(
                        "The file already contains a localized string for key \"{0}\". The old value will be preserved.",
                        templateString.LocalizationKey);
                }
                else
                {
                    // Existing file did not contain a localized version of the string.
                    // Or we want 'forceUpdate': ignore the existing localizations.
                    // Use the original value from template.json.
                    localizedText = templateString.Value;
                }

                jsonWriter.WritePropertyName(templateString.LocalizationKey);
                jsonWriter.WriteStringValue(localizedText);

                // A translation and the related comment should be next to each other. Write the comment now before any other text.
                string commentKey = "_" + templateString.LocalizationKey + ".comment";
                if (existingStrings != null && existingStrings.TryGetValue(commentKey, out string? comment))
                {
                    jsonWriter.WritePropertyName(commentKey);
                    jsonWriter.WriteStringValue(comment);
                }
            }

            jsonWriter.WriteEndObject();
            await jsonWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
        }
    }
}
