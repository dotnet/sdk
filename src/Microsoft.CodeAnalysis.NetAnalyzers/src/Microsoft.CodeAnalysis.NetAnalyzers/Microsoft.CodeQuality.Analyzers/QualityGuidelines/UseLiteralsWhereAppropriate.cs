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
    public abstract class UseLiteralsWhereAppropriateAnalyzer : DiagnosticAnalyzer
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

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                var builder = ImmutableHashSet.CreateBuilder<ITypeSymbol>();
                builder.Add(context.Compilation.GetSpecialType(SpecialType.System_IntPtr));
                builder.Add(context.Compilation.GetSpecialType(SpecialType.System_UIntPtr));

                var constantIncompatibleTypes = builder.ToImmutable();

                context.RegisterOperationAction(context =>
                {
                    var fieldInitializer = context.Operation as IFieldInitializerOperation;

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
                        !context.Options.MatchesConfiguredVisibility(DefaultRule, lastField, context.Compilation, defaultRequiredVisibility: SymbolVisibilityGroup.Internal | SymbolVisibilityGroup.Private) ||
                        !context.Options.MatchesConfiguredModifiers(DefaultRule, lastField, context.Compilation, defaultRequiredModifiers: SymbolModifiers.Static))
                    {
                        return;
                    }

                    var initializerValue = fieldInitializerValue.ConstantValue.Value;

                    if (fieldInitializerValue.Kind == OperationKind.InterpolatedString &&
                        !IsConstantInterpolatedStringSupported(fieldInitializerValue.Syntax.SyntaxTree.Options))
                    {
                        return;
                    }

                    // Though null is const we don't fire the diagnostic to be FxCop Compact
                    if (initializerValue != null &&
                        !constantIncompatibleTypes.Contains(fieldInitializerValue.Type))
                    {
                        if (fieldInitializerValue.Type?.SpecialType == SpecialType.System_String &&
                            ((string)initializerValue).Length == 0)
                        {
                            context.ReportDiagnostic(lastField.CreateDiagnostic(EmptyStringRule, lastField.Name));
                            return;
                        }

                        context.ReportDiagnostic(lastField.CreateDiagnostic(DefaultRule, lastField.Name));
                    }
                },
                OperationKind.FieldInitializer);
            });
        }

        protected abstract bool IsConstantInterpolatedStringSupported(ParseOptions compilation);
    }
}