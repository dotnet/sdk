// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core
{
    internal static class TemplateStringUpdater
    {
        /// <summary>
        /// The UTF8 BOM sequence 0xEF,0xBB,0xBF cached in a static field.
        /// </summary>
        private static readonly byte[] Utf8Bom = new UTF8Encoding(true).GetPreamble();

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
                _ = Directory.CreateDirectory(targetDirectory);
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
            catch (IOException ex) when (ex is DirectoryNotFoundException or FileNotFoundException)
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
            JsonWriterOptions writerOptions = new()
            {
                // Allow unescaped characters in the strings. This allows writing "aren't" instead of "aren\u0027t".
                // This is only considered unsafe in a context where symbols may be interpreted as special characters.
                // For instance, '<' character should be escaped in html documents where this json will be embedded.
                Encoder = new ExtendedJavascriptEncoder(),
                Indented = true,
            };

            // Determine what strings to write. If they are identical to the existing ones, no need to make disk IO.
            List<(string Key, string Value)> valuesToWrite = new();

            foreach (TemplateString templateString in templateStrings)
            {
                if (!forceUpdate && (existingStrings?.TryGetValue(templateString.LocalizationKey, out string? localizedText) ?? false))
                {
                    logger.LogDebug(LocalizableStrings.stringUpdater_log_localizedStringAlreadyExists, templateString.LocalizationKey);
                }
                else
                {
                    // Existing file did not contain a localized version of the string.
                    // Or we want 'forceUpdate': ignore the existing localizations.
                    // Use the original value from template.json.
                    localizedText = templateString.Value;
                }

                valuesToWrite.Add((templateString.LocalizationKey, localizedText!));

                // A translation and the related comment should be next to each other. Write the comment now before any other text.
                string commentKey = "_" + templateString.LocalizationKey + ".comment";
                if (existingStrings != null && existingStrings.TryGetValue(commentKey, out string? comment))
                {
                    valuesToWrite.Add((commentKey, comment));
                }
            }

            if (SequenceEqual(valuesToWrite, existingStrings))
            {
                // Data appears to be same as before. Don't rewrite it.
                // Rewriting the same data causes differences in encoding etc, which marks files as 'changed' in git.
                logger.LogDebug(LocalizableStrings.stringUpdater_log_dataIsUnchanged, filePath);
                return;
            }

            logger.LogDebug(LocalizableStrings.stringUpdater_log_openingTemplatesJson, filePath);
            using FileStream fileStream = new(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite);
            await TruncateFileWhilePreservingBom(fileStream, cancellationToken).ConfigureAwait(false);

            using Utf8JsonWriter jsonWriter = new(fileStream, writerOptions);

            jsonWriter.WriteStartObject();

            foreach ((string key, string value) in valuesToWrite)
            {
                jsonWriter.WritePropertyName(key);
                jsonWriter.WriteStringValue(value);
            }

            jsonWriter.WriteEndObject();
            await jsonWriter.FlushAsync(cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Truncates the file represented by the given <see cref="FileStream"/> to contain only the UTF8 BOM preamble
        /// or to contain nothing (i.e. become empty) if the stream does not start with the UTF8 BOM sequence.
        /// </summary>
        /// <param name="fileStream">The <see cref="FileStream"/> representing the file to truncate.</param>
        /// <param name="cancellationToken">The async cancellation token.</param>
        /// <returns>A task that represents the asynchronous operation.</returns>
        private static async Task TruncateFileWhilePreservingBom(FileStream fileStream, CancellationToken cancellationToken)
        {
            byte[] preamble = new byte[Utf8Bom.Length];
            int offset = 0;
            int read;
            // Read bytes from the stream until we fill the preamble array or hit EOF.
            do
            {
                read = await fileStream.ReadAsync(preamble, offset, preamble.Length - offset, cancellationToken).ConfigureAwait(false);
                offset += read;
                // Optimization to not call .ReadAsync twice
                if (offset == preamble.Length)
                {
                    break;
                }
            }
            while (read > 0);

            fileStream.SetLength(offset == Utf8Bom.Length && preamble.SequenceEqual(Utf8Bom) ? offset : 0);
        }

        private static bool SequenceEqual(List<(string, string)> lhs, Dictionary<string, string>? rhs)
        {
            if (lhs.Count != (rhs?.Count ?? 0))
            {
                return false;
            }

            if (rhs != null)
            {
                foreach ((string key, string value) in lhs)
                {
                    if (!rhs.TryGetValue(key, out string existingValue)
                        || value != existingValue)
                    {
                        return false;
                    }
                }
            }

            return true;
        }
    }
}
