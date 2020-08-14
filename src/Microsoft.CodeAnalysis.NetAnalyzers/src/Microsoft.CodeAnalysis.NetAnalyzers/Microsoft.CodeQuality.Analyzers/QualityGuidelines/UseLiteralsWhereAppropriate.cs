// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    /// <summary>
    /// CA1802: Use literals where appropriate
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseLiteralsWhereAppropriateAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1802";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UseLiteralsWhereAppropriateTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessageDefault = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UseLiteralsWhereAppropriateMessageDefault), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageEmptyString = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UseLiteralsWhereAppropriateMessageEmptyString), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UseLiteralsWhereAppropriateDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor DefaultRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageDefault,
                                                                             DiagnosticCategory.Performance,
                                                                             RuleLevel.CandidateForRemoval,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);
        internal static DiagnosticDescriptor EmptyStringRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessageEmptyString,
                                                                             DiagnosticCategory.Performance,
                                                                             RuleLevel.CandidateForRemoval,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DefaultRule, EmptyStringRule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterOperationAction(saContext =>
            {
                var fieldInitializer = saContext.Operation as IFieldInitializerOperation;

                // Diagnostics are reported on the last initialized field to retain the previous FxCop behavior
                // Note all the descriptors/rules for this analyzer have the same ID and category and hence
                // will always have identical configured visibility.
                var lastField = fieldInitializer?.InitializedFields.LastOrDefault();
                var fieldInitializerValue = fieldInitializer?.Value;
                if (fieldInitializerValue == null ||
                    lastField == null ||
                    lastField.IsConst ||
                    !lastField.IsReadOnly ||
                    !fieldInitializerValue.ConstantValue.HasValue ||
                    !lastField.MatchesConfiguredVisibility(saContext.Options, DefaultRule, saContext.Compilation, saContext.CancellationToken, defaultRequiredVisibility: SymbolVisibilityGroup.Internal | SymbolVisibilityGroup.Private) ||
                    !lastField.MatchesConfiguredModifiers(saContext.Options, DefaultRule, saContext.Compilation, saContext.CancellationToken, defaultRequiredModifiers: SymbolModifiers.Static))
                {
                    return;
                }

                var initializerValue = fieldInitializerValue.ConstantValue.Value;

                // Though null is const we don't fire the diagnostic to be FxCop Compact
                if (initializerValue != null)
                {
                    if (fieldInitializerValue.Type?.SpecialType == SpecialType.System_String &&
                        ((string)initializerValue).Length == 0)
                    {
                        saContext.ReportDiagnostic(lastField.CreateDiagnostic(EmptyStringRule, lastField.Name));
                        return;
                    }

                    saContext.ReportDiagnostic(lastField.CreateDiagnostic(DefaultRule, lastField.Name));
                }
            },
            OperationKind.FieldInitializer);
        }
    }
}