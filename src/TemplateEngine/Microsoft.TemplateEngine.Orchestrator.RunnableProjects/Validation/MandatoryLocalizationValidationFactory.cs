// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.ConfigModel;
using Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Localization;

namespace Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Validation
{
    internal class MandatoryLocalizationValidationFactory : ITemplateValidatorFactory
    {
        public ValidationScope Scope => ValidationScope.Scanning | ValidationScope.Instantiation;

        public Guid Id { get; } = new Guid("{3C4D9A06-B9D7-402F-A890-2392894D0798}");

        public Task<ITemplateValidator> CreateValidatorAsync(IEngineEnvironmentSettings engineEnvironmentSettings, CancellationToken cancellationToken) => Task.FromResult((ITemplateValidator)new MandatoryLocalizationValidation(this));

        internal class MandatoryLocalizationValidation : ITemplateValidator
        {
            internal MandatoryLocalizationValidation(ITemplateValidatorFactory factory)
            {
                Factory = factory;
            }

            public ITemplateValidatorFactory Factory { get; }

            public void ValidateTemplate(ITemplateValidationInfo templateValidationInfo)
            {
                foreach (KeyValuePair<CultureInfo, TemplateLocalizationInfo> localization in templateValidationInfo.Localizations)
                {
                    LocalizationModel locModel = localization.Value.Model;
                    int unusedPostActionLocs = locModel.PostActions.Count;
                    foreach (PostActionModel postAction in templateValidationInfo.ConfigModel.PostActionModels)
                    {
                        if (postAction.Id == null || !locModel.PostActions.TryGetValue(postAction.Id, out PostActionLocalizationModel postActionLocModel))
                        {
                            // Post action with no localization model.
                            continue;
                        }

                        unusedPostActionLocs--;

                        // Validate manual instructions.
                        bool instructionUsesDefaultKey = postAction.ManualInstructionInfo.Count == 1 && postAction.ManualInstructionInfo[0].Id == null &&
                            postActionLocModel.Instructions.ContainsKey(PostActionModel.DefaultIdForSingleManualInstruction);
                        if (instructionUsesDefaultKey)
                        {
                            // Just one manual instruction using the default key. No issues. Continue.
                            continue;
                        }

                        int unusedManualInstructionLocs = postActionLocModel.Instructions.Count;
                        foreach (var instruction in postAction.ManualInstructionInfo)
                        {
                            if (instruction.Id != null && postActionLocModel.Instructions.ContainsKey(instruction.Id))
                            {
                                unusedManualInstructionLocs--;
                            }
                        }

                        if (unusedManualInstructionLocs > 0)
                        {
                            // Localizations provide more translations than the number of manual instructions we have.
                            string excessInstructionLocalizationIds = string.Join(
                                ", ",
                                postActionLocModel.Instructions.Keys.Where(k => !postAction.ManualInstructionInfo.Any(i => i.Id == k)));

                            localization.Value.AddValidationEntry(new ValidationEntry(IValidationEntry.SeverityLevel.Error, "LOC001", string.Format(LocalizableStrings.Authoring_InvalidManualInstructionLocalizationIndex, excessInstructionLocalizationIds, postAction.Id)));
                        }
                    }

                    if (unusedPostActionLocs > 0)
                    {
                        // Localizations provide more translations than the number of post actions we have.
                        string excessPostActionLocalizationIds = string.Join(", ", locModel.PostActions.Keys.Where(k => !templateValidationInfo.ConfigModel.PostActionModels.Any(p => p.Id == k)).Select(k => k.ToString()));
                        localization.Value.AddValidationEntry(new ValidationEntry(IValidationEntry.SeverityLevel.Error, "LOC002", string.Format(LocalizableStrings.Authoring_InvalidPostActionLocalizationIndex, excessPostActionLocalizationIds)));
                    }
                }
            }
        }
    }
}
