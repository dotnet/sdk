// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Edge.Settings;

namespace Microsoft.TemplateEngine.Authoring.CLI.Commands
{
    internal class ValidateCommand : ExecutableCommand<ValidateCommandArgs>
    {
        private const string CommandName = "validate";

        private readonly CliArgument<string> _templateLocationArg = new("template-location")
        {
            Description = LocalizableStrings.command_validate_help_description,
            Arity = new ArgumentArity(1, 1)
        };

        public ValidateCommand() : base(CommandName, LocalizableStrings.command_validate_help_locationArg_description)
        {
            Arguments.Add(_templateLocationArg);
        }

        protected internal override ValidateCommandArgs ParseContext(ParseResult parseResult)
        {
            return new ValidateCommandArgs(parseResult.GetValue(_templateLocationArg) ?? throw new InvalidOperationException("The command should be run with one argument."));
        }

        protected override async Task<int> ExecuteAsync(ValidateCommandArgs args, ILoggerFactory loggerFactory, CancellationToken cancellationToken)
        {
            ILogger logger = loggerFactory.CreateLogger(CommandName);
            cancellationToken.ThrowIfCancellationRequested();

            using IEngineEnvironmentSettings settings = SetupSettings(loggerFactory);
            Scanner scanner = new(settings);

            logger.LogInformation(LocalizableStrings.command_validate_info_scanning_in_progress, args.TemplateLocation);

            ScanResult scanResult = await scanner.ScanAsync(
                args.TemplateLocation,
                logValidationResults: false,
                returnInvalidTemplates: true,
                cancellationToken).ConfigureAwait(false);
            cancellationToken.ThrowIfCancellationRequested();
            logger.LogInformation("Scanning completed");
            PrintResults(logger, scanResult);
            return scanResult.Templates.Any(t => !t.IsValid) ? 1 : 0;
        }

        private IEngineEnvironmentSettings SetupSettings(ILoggerFactory loggerFactory)
        {
            var builtIns = new List<(Type InterfaceType, IIdentifiedComponent Instance)>();
            builtIns.AddRange(Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Components.AllComponents);
            builtIns.AddRange(Microsoft.TemplateEngine.Edge.Components.AllComponents);

            ITemplateEngineHost host = new DefaultTemplateEngineHost("template-validator", "1.0", builtIns: builtIns, loggerFactory: loggerFactory);
            IEngineEnvironmentSettings engineEnvironmentSettings = new EngineEnvironmentSettings(host, virtualizeSettings: true);

            return engineEnvironmentSettings;
        }

        private void PrintResults(ILogger logger, ScanResult scanResult)
        {
            using var scope = logger.BeginScope("Results");
            logger.LogInformation(LocalizableStrings.command_validate_info_scanning_completed, scanResult.MountPoint.MountPointUri, scanResult.Templates.Count);
            foreach (IScanTemplateInfo template in scanResult.Templates.OrderBy(t => t.Identity, StringComparer.Ordinal))
            {
                string templateDisplayName = GetTemplateDisplayName(template);
                StringBuilder sb = new();

                LogValidationEntries(
                    sb,
                    string.Format(LocalizableStrings.command_validate_info_template_header, templateDisplayName),
                    template.ValidationErrors);
                foreach (KeyValuePair<string, ILocalizationLocator> locator in template.Localizations.OrderBy(l => l.Value.Locale, StringComparer.Ordinal))
                {
                    ILocalizationLocator localizationInfo = locator.Value;

                    LogValidationEntries(
                        sb,
                        string.Format(LocalizableStrings.command_validate_info_template_loc_header, localizationInfo.Locale, templateDisplayName),
                        localizationInfo.ValidationErrors);
                }

                if (!template.IsValid)
                {
                    sb.AppendFormat(LocalizableStrings.command_validate_info_summary_invalid, templateDisplayName);
                }
                else
                {
                    sb.AppendFormat(LocalizableStrings.command_validate_info_summary_valid, templateDisplayName);
                }
                sb.AppendLine();
                foreach (ILocalizationLocator loc in template.Localizations.Values.OrderBy(l => l.Locale, StringComparer.Ordinal))
                {
                    if (loc.IsValid)
                    {
                        sb.AppendFormat(LocalizableStrings.command_validate_info_summary_loc_valid, loc.Locale, templateDisplayName);
                    }
                    else
                    {
                        sb.AppendFormat(LocalizableStrings.command_validate_info_summary_loc_invalid, loc.Locale, templateDisplayName);
                    }
                    sb.AppendLine();
                }
                logger.LogInformation(sb.ToString());
            }

            static string GetTemplateDisplayName(IScanTemplateInfo template)
            {
                string templateName = string.IsNullOrEmpty(template.Name) ? "<no name>" : template.Name;
                return $"'{templateName}' ({template.Identity})";
            }

            static string PrintError(IValidationEntry error) => $"   [{error.Severity}][{error.Code}] {error.ErrorMessage}";

            static void LogValidationEntries(StringBuilder sb, string header, IReadOnlyList<IValidationEntry> errors)
            {
                sb.AppendLine(header);
                if (!errors.Any())
                {
                    sb.AppendLine("   " + LocalizableStrings.command_validate_info_no_entries);
                }
                else
                {
                    foreach (IValidationEntry error in errors.OrderByDescending(e => e.Severity))
                    {
                        sb.AppendLine(PrintError(error));
                    }
                }
            }

        }
    }
}
