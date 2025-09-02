// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1802: <inheritdoc cref="UseLiteralsWhereAppropriateTitle"/>
    /// </summary>
    public abstract class UseLiteralsWhereAppropriateAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1802";

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(UseLiteralsWhereAppropriateTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(UseLiteralsWhereAppropriateDescription));

        internal static readonly DiagnosticDescriptor DefaultRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UseLiteralsWhereAppropriateMessageDefault)),
            DiagnosticCategory.Performance,
            RuleLevel.CandidateForRemoval,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor EmptyStringRule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(UseLiteralsWhereAppropriateMessageEmptyString)),
            DiagnosticCategory.Performance,
            RuleLevel.CandidateForRemoval,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(DefaultRule, EmptyStringRule);

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

#pragma warning disable IDE0039 // Use local function
                Action<OperationAnalysisContext> operationAction = context =>
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
                        (lastField.Type?.Name == lastField.Name && fieldInitializerValue.DescendantsAndSelf().Any(d => d is IFieldReferenceOperation field && lastField.Type.Equals(field.Field.Type, SymbolEqualityComparer.Default))) ||
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
                        fieldInitializerValue.Type is { } fieldInitializerType &&
                        !constantIncompatibleTypes.Contains(fieldInitializerType))
                    {
                        if (fieldInitializerType.SpecialType == SpecialType.System_String &&
                            ((string)initializerValue).Length == 0)
                        {
                            context.ReportDiagnostic(lastField.CreateDiagnostic(EmptyStringRule, lastField.Name));
                            return;
                        }

                        context.ReportDiagnostic(lastField.CreateDiagnostic(DefaultRule, lastField.Name));
                    }
                };

                context.RegisterSymbolStartAction(context =>
                {
                    context.RegisterOperationAction(operationAction, OperationKind.FieldInitializer);
                }, SymbolKind.Field);
            });
        }

        protected abstract bool IsConstantInterpolatedStringSupported(ParseOptions compilation);
    }
}