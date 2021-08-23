// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1051: Do not declare visible instance fields
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotDeclareVisibleInstanceFieldsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1051";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotDeclareVisibleInstanceFieldsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotDeclareVisibleInstanceFieldsMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotDeclareVisibleInstanceFieldsDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        // TODO: Need to revisit the "RuleLevel" for this Rule.
        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Design,
                                                                             RuleLevel.IdeHidden_BulkConfigurable,
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

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
                    if (field.ContainingType.HasAttribute(structLayoutAttributeType))
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
