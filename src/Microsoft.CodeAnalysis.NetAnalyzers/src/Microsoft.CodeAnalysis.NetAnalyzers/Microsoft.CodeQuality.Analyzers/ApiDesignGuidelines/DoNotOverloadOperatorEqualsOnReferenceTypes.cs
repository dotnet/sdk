// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotOverloadOperatorEqualsOnReferenceTypes : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1046";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotOverloadOperatorEqualsOnReferenceTypesTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotOverloadOperatorEqualsOnReferenceTypesMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotOverloadOperatorEqualsOnReferenceTypesDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
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
                var iequatableType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIEquatable1);
                var icomparableType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIComparable);
                var icomparableGenericType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIComparable1);

                context.RegisterSymbolAction(context =>
                {
                    var method = (IMethodSymbol)context.Symbol;

                    // Method is not operator equality or not on a class
                    if (method.MethodKind != MethodKind.UserDefinedOperator ||
                        method.Name != WellKnownMemberNames.EqualityOperatorName ||
                        method.ContainingType.TypeKind != TypeKind.Class)
                    {
                        return;
                    }

                    // There's a CONSIDER rule for overriding op_Equality for anything that implements IEquatable.
                    if (method.ContainingType.Inherits(iequatableType))
                    {
                        return;
                    }

                    // FxCop compat: bail-out if the type overrides Object.Equals or is IComparable/IComparable<T>
                    if (method.ContainingType.OverridesEquals() ||
                        method.ContainingType.Inherits(icomparableType) ||
                        method.ContainingType.Inherits(icomparableGenericType))
                    {
                        return;
                    }

                    // FxCop compat: only analyze externally visible symbols by default.
                    if (!context.Options.MatchesConfiguredVisibility(Rule, method, context.Compilation))
                    {
                        return;
                    }

                    context.ReportDiagnostic(method.CreateDiagnostic(Rule, method.ContainingType.Name));
                }, SymbolKind.Method);
            });
        }
    }
}
