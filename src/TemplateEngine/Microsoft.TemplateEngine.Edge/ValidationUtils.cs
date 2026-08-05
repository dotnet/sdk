// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Edge
{
    internal static class ValidationUtils
    {
        internal static void LogValidationResults(ILogger logger, IReadOnlyList<IScanTemplateInfo> templates)
        {
            foreach (IScanTemplateInfo template in templates)
            {
                string templateDisplayName = GetTemplateDisplayName(template);
                logger.LogDebug("Found template {0}", templateDisplayName);

                ValidateTemplate(logger, template, templateDisplayName);

                foreach (KeyValuePair<string, ILocalizationLocator> locator in template.Localizations)
                {
                    ILocalizationLocator localizationInfo = locator.Value;
                    ValidateLocalization(logger, templateDisplayName, localizationInfo);
                }

                if (!template.IsValid)
                {
                    logger.LogError(LocalizableStrings.Validation_InvalidTemplate, templateDisplayName);
                }
                foreach (ILocalizationLocator invalidLoc in template.Localizations.Values.Where(li => !li.IsValid))
                {
                    logger.LogWarning(LocalizableStrings.Validation_InvalidTemplateLoc, invalidLoc.Locale, templateDisplayName);
                }
            }
        }

        internal static void LogValidationResults(ILogger logger, IReadOnlyList<ITemplate> templates)
        {
            foreach (ITemplate template in templates)
            {
                string templateDisplayName = GetTemplateDisplayName(template);
                logger.LogDebug("Found template {0}", templateDisplayName);

                ValidateTemplate(logger, template, templateDisplayName);

                ILocalizationLocator? localizationInfo = template.Localization;

                if (localizationInfo != null)
                {
                    ValidateLocalization(logger, templateDisplayName, localizationInfo);
                }

                if (!template.IsValid)
                {
                    logger.LogError(LocalizableStrings.Validation_InvalidTemplate, templateDisplayName);
                }
                if (!template.Localization?.IsValid ?? false)
                {
                    logger.LogWarning(LocalizableStrings.Validation_InvalidTemplateLoc, template.Localization!.Locale, templateDisplayName);
                }
            }
        }

        private static void ValidateTemplate(ILogger logger, IValidationInfo template, string templateDisplayName)
        {
            LogValidationEntries(
                logger,
                string.Format(LocalizableStrings.Validation_Error_Header, templateDisplayName),
                template.ValidationErrors,
                IValidationEntry.SeverityLevel.Error);
            LogValidationEntries(
                logger,
                string.Format(LocalizableStrings.Validation_Warning_Header, templateDisplayName),
                template.ValidationErrors,
                IValidationEntry.SeverityLevel.Warning);
            LogValidationEntries(
                logger,
                string.Format(LocalizableStrings.Validation_Info_Header, templateDisplayName),
                template.ValidationErrors,
                IValidationEntry.SeverityLevel.Info);
        }

        private static void ValidateLocalization(ILogger logger, string templateDisplayName, ILocalizationLocator localizationInfo)
        {
            LogValidationEntries(
                logger,
                string.Format(LocalizableStrings.Validation_LocError_Header, templateDisplayName, localizationInfo.Locale),
                localizationInfo.ValidationErrors,
                IValidationEntry.SeverityLevel.Error);
            LogValidationEntries(
                logger,
                string.Format(LocalizableStrings.Validation_LocWarning_Header, templateDisplayName, localizationInfo.Locale),
                localizationInfo.ValidationErrors,
                IValidationEntry.SeverityLevel.Warning);
            LogValidationEntries(
                logger,
                string.Format(LocalizableStrings.Validation_LocInfo_Header, templateDisplayName, localizationInfo.Locale),
                localizationInfo.ValidationErrors,
                IValidationEntry.SeverityLevel.Info);
        }

        private static string GetTemplateDisplayName(ITemplateMetadata template)
        {
            string templateName = string.IsNullOrEmpty(template.Name) ? "<no name>" : template.Name;
            return $"'{templateName}' ({template.Identity})";
        }

        private static string PrintError(IValidationEntry error) => $"   [{error.Severity}][{error.Code}] {error.ErrorMessage}";

        private static void LogValidationEntries(ILogger logger, string header, IReadOnlyList<IValidationEntry> errors, IValidationEntry.SeverityLevel severity)
        {
            Action<string> log = severity switch
            {
                IValidationEntry.SeverityLevel.None => (s) => throw new NotSupportedException($"{IValidationEntry.SeverityLevel.None} severity is not supported."),
                IValidationEntry.SeverityLevel.Info => (s) => logger.LogDebug(s),
                IValidationEntry.SeverityLevel.Warning => (s) => logger.LogWarning(s),
                IValidationEntry.SeverityLevel.Error => (s) => logger.LogError(s),
                _ => throw new InvalidOperationException($"{severity} is not expected value for {nameof(IValidationEntry.SeverityLevel)}."),
            };

            if (!errors.Any(e => e.Severity == severity))
            {
                return;
            }

            StringBuilder sb = new();
            sb.AppendLine(header);
            foreach (IValidationEntry error in errors.Where(e => e.Severity == severity))
            {
                sb.AppendLine(PrintError(error));
            }
            log(sb.ToString());
        }
    }
}
