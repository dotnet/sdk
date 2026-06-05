// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1051: <inheritdoc cref="DoNotDeclareVisibleInstanceFieldsTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotDeclareVisibleInstanceFieldsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1051";

        // TODO: Need to revisit the "RuleLevel" for this Rule.
        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotDeclareVisibleInstanceFieldsTitle)),
            CreateLocalizableResourceString(nameof(DoNotDeclareVisibleInstanceFieldsMessage)),
            DiagnosticCategory.Design,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: CreateLocalizableResourceString(nameof(DoNotDeclareVisibleInstanceFieldsDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                var structLayoutAttributeType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesStructLayoutAttribute);

                context.RegisterSymbolAction(symbolAnalysisContext =>
                {
                    var field = (IFieldSymbol)symbolAnalysisContext.Symbol;

                    // Only report diagnostic on non-static, non-const, non-private fields.
                    if (field.IsStatic ||
                        field.IsConst ||
                        field.DeclaredAccessibility == Accessibility.Private)
                    {
                        return;
                    }

                    // Do not report on types marked with StructLayoutAttribute
                    // See https://github.com/dotnet/roslyn-analyzers/issues/4149
                    if (field.ContainingType.HasAnyAttribute(structLayoutAttributeType))
                    {
                        return;
                    }

                    var excludeStructs = symbolAnalysisContext.Options.GetBoolOptionValue(EditorConfigOptionNames.ExcludeStructs, Rule,
                        field, symbolAnalysisContext.Compilation, defaultValue: false);
                    if (excludeStructs &&
                        field.ContainingType?.TypeKind == TypeKind.Struct)
                    {
                        return;
                    }

                    // Additionally, by default only report externally visible fields for FxCop compat.
                    if (symbolAnalysisContext.Options.MatchesConfiguredVisibility(Rule, field, symbolAnalysisContext.Compilation))
                    {
                        symbolAnalysisContext.ReportDiagnostic(field.CreateDiagnostic(Rule));
                    }
                }, SymbolKind.Field);
            });
        }
    }
}
