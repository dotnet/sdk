// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Authoring.Tasks.Utilities;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Authoring.Tasks.Tasks
{
    /// <summary>
    /// A task that exposes template localization functionality of
    /// Microsoft.TemplateEngine.TemplateLocalizer through MSBuild.
    /// </summary>
    public sealed class ValidateTemplates : Build.Utilities.Task, ICancelableTask
    {
        private readonly CancellationTokenSource _cancellationTokenSource = new();

        /// <summary>
        /// Gets or sets the path to the template(s) to be validated.
        /// </summary>
        [Required]
        public string? TemplateLocation { get; set; }

        public override bool Execute()
        {
            if (string.IsNullOrWhiteSpace(TemplateLocation))
            {
                Log.LogError(LocalizableStrings.Log_Error_MissingRequiredProperty, nameof(TemplateLocation), nameof(ValidateTemplates));
                return false;
            }

            string templateLocation = Path.GetFullPath(TemplateLocation);

            using var loggerProvider = new MSBuildLoggerProvider(Log);
            ILoggerFactory msbuildLoggerFactory = new LoggerFactory(new[] { loggerProvider });
            CancellationToken cancellationToken = _cancellationTokenSource.Token;

            try
            {
                using IEngineEnvironmentSettings settings = SetupSettings(msbuildLoggerFactory);
                Scanner scanner = new(settings);
                ScanResult scanResult = Task.Run(async () => await scanner.ScanAsync(
                    templateLocation!,
                    logValidationResults: false,
                    returnInvalidTemplates: true,
                    cancellationToken).ConfigureAwait(false)).GetAwaiter().GetResult();

                cancellationToken.ThrowIfCancellationRequested();

                LogResults(scanResult);
                return !Log.HasLoggedErrors;
            }
            catch (Exception ex)
            {
                Log.LogErrorFromException(ex);
                return false;
            }
        }

        public void Cancel() => _cancellationTokenSource.Cancel();

        public void Dispose() => _cancellationTokenSource.Dispose();

        private IEngineEnvironmentSettings SetupSettings(ILoggerFactory loggerFactory)
        {
            var builtIns = new List<(Type InterfaceType, IIdentifiedComponent Instance)>();
            builtIns.AddRange(Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Components.AllComponents);
            builtIns.AddRange(Microsoft.TemplateEngine.Edge.Components.AllComponents);

            ITemplateEngineHost host = new DefaultTemplateEngineHost("template-validator", "1.0", builtIns: builtIns, loggerFactory: loggerFactory);
            IEngineEnvironmentSettings engineEnvironmentSettings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            return engineEnvironmentSettings;
        }

        private void LogResults(ScanResult scanResult)
        {
            Log.LogMessage(LocalizableStrings.Validate_Log_FoundTemplate, scanResult.MountPoint.MountPointUri, scanResult.Templates.Count);
            foreach (IScanTemplateInfo template in scanResult.Templates)
            {
                string templateDisplayName = GetTemplateDisplayName(template);
                StringBuilder sb = new();

                LogValidationEntries(LocalizableStrings.Validate_Log_TemplateConfiguration_Subcategory, template.ValidationErrors);
                foreach (KeyValuePair<string, ILocalizationLocator> locator in template.Localizations)
                {
                    ILocalizationLocator localizationInfo = locator.Value;
                    LogValidationEntries(LocalizableStrings.Validate_Log_TemplateLocalization_Subcategory, localizationInfo.ValidationErrors);
                }
            }

            static string GetTemplateDisplayName(IScanTemplateInfo template)
            {
                string templateName = string.IsNullOrEmpty(template.Name) ? "<no name>" : template.Name;
                return $"'{templateName}' ({template.Identity})";
            }

            void LogValidationEntries(string subCategory, IReadOnlyList<IValidationEntry> errors)
            {
                foreach (IValidationEntry error in errors.OrderByDescending(e => e.Severity))
                {
                    switch (error.Severity)
                    {
                        case IValidationEntry.SeverityLevel.Error:
                            Log.LogError(
                                subcategory: subCategory,
                                errorCode: error.Code,
                                helpKeyword: string.Empty,
                                file: error.Location?.Filename ?? string.Empty,
                                lineNumber: error.Location?.LineNumber ?? 0,
                                columnNumber: error.Location?.Position ?? 0,
                                endLineNumber: 0,
                                endColumnNumber: 0,
                                message: error.ErrorMessage);
                            break;
                        case IValidationEntry.SeverityLevel.Warning:
                            Log.LogWarning(
                                subcategory: subCategory,
                                warningCode: error.Code,
                                helpKeyword: string.Empty,
                                file: error.Location?.Filename ?? string.Empty,
                                lineNumber: error.Location?.LineNumber ?? 0,
                                columnNumber: error.Location?.Position ?? 0,
                                endLineNumber: 0,
                                endColumnNumber: 0,
                                message: error.ErrorMessage);
                            break;
                        case IValidationEntry.SeverityLevel.Info:
                            Log.LogMessage(
                                subcategory: subCategory,
                                code: error.Code,
                                helpKeyword: string.Empty,
                                file: error.Location?.Filename ?? string.Empty,
                                lineNumber: error.Location?.LineNumber ?? 0,
                                columnNumber: error.Location?.Position ?? 0,
                                endLineNumber: 0,
                                endColumnNumber: 0,
                                MessageImportance.High,
                                message: error.ErrorMessage);
                            break;
                    }
                }
            }

        }
    }
}
