// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TemplateEngine.TemplateLocalizer.Core.Exceptions;

namespace Microsoft.TemplateEngine.TemplateLocalizer.Core
{
    /// <summary>
    /// Creates and updates localization files for template.json files.
    /// Public members of this type is thread-safe.
    /// </summary>
    public sealed class TemplateLocalizer
    {
        private readonly ILoggerFactory _loggerFactory;

        private readonly ILogger _logger;

        public TemplateLocalizer() : this(null) { }

        public TemplateLocalizer(ILoggerFactory? loggerFactory)
        {
            _loggerFactory = loggerFactory ?? NullLoggerFactory.Instance;
            _logger = _loggerFactory.CreateLogger<TemplateLocalizer>();
        }

        public async Task<ExportResult> ExportLocalizationFilesAsync(string templateJsonPath, ExportOptions options, CancellationToken cancellationToken = default)
        {
            JsonDocumentOptions jsonOptions = new JsonDocumentOptions()
            {
                CommentHandling = JsonCommentHandling.Skip,
                AllowTrailingCommas = true,
            };

            try
            {
                using FileStream fileStream = new FileStream(templateJsonPath, FileMode.Open, FileAccess.Read);
                using JsonDocument jsonDocument = await JsonDocument.ParseAsync(fileStream, jsonOptions, cancellationToken).ConfigureAwait(false);

                TemplateStringExtractor stringExtractor = new TemplateStringExtractor(jsonDocument, _loggerFactory);
                IReadOnlyList<TemplateString> templateJsonStrings = stringExtractor.ExtractStrings(out string templateJsonLanguage);

                string targetDirectory = options.TargetDirectory ?? Path.Combine(Path.GetDirectoryName(templateJsonPath) ?? string.Empty, "localize");

                await TemplateStringUpdater.UpdateStringsAsync(
                    templateJsonStrings,
                    templateJsonLanguage,
                    options.Languages ?? ExportOptions.DefaultLanguages,
                    targetDirectory,
                    options.DryRun,
                    _logger,
                    cancellationToken).ConfigureAwait(false);

                return new ExportResult(templateJsonPath);
            }
            catch (Exception exception)
                when (exception is JsonMemberMissingException || exception is LocalizationKeyIsNotUniqueException)
            {
                // Output a more friendly text without stack trace for known errors.
                return new ExportResult(templateJsonPath, exception.Message);
            }
            catch (Exception exception)
            {
                return new ExportResult(templateJsonPath, null, exception);
            }
        }
    }
}
