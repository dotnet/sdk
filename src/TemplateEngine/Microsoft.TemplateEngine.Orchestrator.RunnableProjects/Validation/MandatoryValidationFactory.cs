// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Mount;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Validation
{
    internal class MandatoryValidationFactory : ITemplateValidatorFactory
    {
        public ValidationScope Scope => ValidationScope.Scanning | ValidationScope.Instantiation;

        public Guid Id { get; } = new Guid("{7EE20FEC-E85C-44FC-9F7D-B477C4FDBE41}");

        public Task<ITemplateValidator> CreateValidatorAsync(IEngineEnvironmentSettings engineEnvironmentSettings, CancellationToken cancellationToken) => Task.FromResult((ITemplateValidator)new MandatoryValidation(this));

        internal class MandatoryValidation : ITemplateValidator
        {
            private readonly IReadOnlyList<IValidationCheck> _validationChecks = new IValidationCheck[]
            {
                new ActionValidationCheck(
                    "MV001",
                    IValidationEntry.SeverityLevel.Error,
                    vi => string.Format(LocalizableStrings.Authoring_MissingValue, "identity"),
                    vi => string.IsNullOrWhiteSpace(vi.ConfigModel.Identity)),

                new ActionValidationCheck(
                    "MV002",
                    IValidationEntry.SeverityLevel.Error,
                    vi => string.Format(LocalizableStrings.Authoring_MissingValue, "name"),
                    vi => string.IsNullOrWhiteSpace(vi.ConfigModel.Name)),

                new ActionValidationCheck(
                    "MV003",
                    IValidationEntry.SeverityLevel.Error,
                    vi => string.Format(LocalizableStrings.Authoring_MissingValue, "shortName"),
                    vi => (vi.ConfigModel.ShortNameList?.Count ?? 0) == 0),

                new MultiValidationCheck<ParameterSymbol>(
                    "MV004",
                    IValidationEntry.SeverityLevel.Error,
                    (vi, ps) => string.Format(
                        LocalizableStrings.Authoring_InvalidMultichoiceSymbol,
                        string.IsNullOrWhiteSpace(ps.DisplayName) ? ps.Name : ps.DisplayName,
                        string.Join(", ", MultiValueParameter.MultiValueSeparators.Select(c => $"'{c}'")),
                        string.Join(
                            ", ",
                            ps.Choices.Where(c => !c.Key.IsValidMultiValueParameterValue())
                                .Select(c => $"{{{c.Key}}}"))),
                    (vi) => vi.ConfigModel.Symbols
                    .OfType<ParameterSymbol>()
                    .Where(p => p.AllowMultipleValues)
                    .Where(p => p.Choices.Any(c => !c.Key.IsValidMultiValueParameterValue()))),

                new ActionValidationCheck(
                    "MV005",
                    IValidationEntry.SeverityLevel.Info,
                    vi => string.Format(LocalizableStrings.Authoring_MissingValue, "sourceName"),
                    vi => string.IsNullOrWhiteSpace(vi.ConfigModel.SourceName)),

                new ActionValidationCheck(
                    "MV006",
                    IValidationEntry.SeverityLevel.Info,
                    vi => string.Format(LocalizableStrings.Authoring_MissingValue, "author"),
                    vi => string.IsNullOrWhiteSpace(vi.ConfigModel.Author)),

                new ActionValidationCheck(
                    "MV007",
                    IValidationEntry.SeverityLevel.Info,
                    vi => string.Format(LocalizableStrings.Authoring_MissingValue, "groupIdentity"),
                    vi => string.IsNullOrWhiteSpace(vi.ConfigModel.GroupIdentity)),

                new ActionValidationCheck(
                    "MV008",
                    IValidationEntry.SeverityLevel.Info,
                    vi => string.Format(LocalizableStrings.Authoring_MissingValue, "generatorVersions"),
                    vi => string.IsNullOrWhiteSpace(vi.ConfigModel.GeneratorVersions)),

                new ActionValidationCheck(
                    "MV009",
                    IValidationEntry.SeverityLevel.Info,
                    vi => string.Format(LocalizableStrings.Authoring_MissingValue, "precedence"),
                    vi => vi.ConfigModel.Precedence == 0),

                new ActionValidationCheck(
                    "MV010",
                    IValidationEntry.SeverityLevel.Info,
                    vi => string.Format(LocalizableStrings.Authoring_MissingValue, "classifications"),
                    vi => (vi.ConfigModel.Classifications?.Count ?? 0) == 0),

                new ActionValidationCheck(
                    "MV011",
                    IValidationEntry.SeverityLevel.Info,
                    vi => LocalizableStrings.Authoring_MalformedPostActionManualInstructions,
                    vi => vi.ConfigModel.PostActionModels != null && vi.ConfigModel.PostActionModels.Any(x => x.ManualInstructionInfo == null || x.ManualInstructionInfo.Count == 0)),

                new TemplateSourcesCheck("MV012", IValidationEntry.SeverityLevel.Error),

            };

            internal MandatoryValidation(ITemplateValidatorFactory factory)
            {
                Factory = factory;
            }

            public ITemplateValidatorFactory Factory { get; }

            public void ValidateTemplate(ITemplateValidationInfo templateValidationInfo)
            {
                _validationChecks.ForEach(check => check.Process(templateValidationInfo));
                templateValidationInfo.ConfigModel.ValidationErrors.ForEach(templateValidationInfo.AddValidationError);
            }

            private class TemplateSourcesCheck : BaseValidationCheck
            {
                public TemplateSourcesCheck(string code, IValidationEntry.SeverityLevel severity) : base(code, severity)
                {
                }

                public override void Process(ITemplateValidationInfo validationInfo)
                {
                    foreach (ExtendedFileSource source in validationInfo.ConfigModel.Sources)
                    {
                        try
                        {
                            IFile? file = validationInfo.TemplateSourceRoot.FileInfo(source.Source);
                            //template source root should not be a file
                            if (file?.Exists ?? false)
                            {
                                AddValidationError(validationInfo, string.Format(LocalizableStrings.Authoring_SourceMustBeDirectory, source.Source));
                            }
                            else
                            {
                                IDirectory? sourceRoot = validationInfo.TemplateSourceRoot.DirectoryInfo(source.Source);
                                if (sourceRoot is null)
                                {
                                    AddValidationError(validationInfo, string.Format(LocalizableStrings.Authoring_SourceIsOutsideInstallSource, source.Source));
                                }
                                else if (!sourceRoot.Exists)
                                {
                                    AddValidationError(validationInfo, string.Format(LocalizableStrings.Authoring_SourceDoesNotExist, source.Source));
                                }
                            }
                        }
                        catch
                        {
                            // outside the mount point root
                            // TODO: after the null ref exception in DirectoryInfo is fixed, change how this check works.
                            AddValidationError(validationInfo, string.Format(LocalizableStrings.Authoring_SourceIsOutsideInstallSource, source.Source));
                        }
                    }
                }
            }
        }
    }
}
