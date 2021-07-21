// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1061: Do not hide base class methods
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotHideBaseClassMethodsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1061";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotHideBaseClassMethodsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotHideBaseClassMethodsMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.DoNotHideBaseClassMethodsDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        public static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            s_localizableTitle,
            s_localizableMessage,
            DiagnosticCategory.Design,
            RuleLevel.IdeSuggestion,
            description: s_localizableDescription,
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(SymbolAnalyzer, SymbolKind.Method);
        }

        private void SymbolAnalyzer(SymbolAnalysisContext context)
        {
            var method = (IMethodSymbol)context.Symbol;

            // Bail out if this method is a constructor
            if (method.IsConstructor())
            {
                return;
            }

            // Bail out if this method overrides another (parameter types must match) or doesn't have any parameters
            if (method.IsOverride || !method.Parameters.Any())
            {
                return;
            }

            foreach (var hiddenMethod in GetMethodsHiddenByMethod(method, method.ContainingType.BaseType))
            {
                var diagnostic = context.Symbol.CreateDiagnostic(Rule, method.ToDisplayString(), hiddenMethod.ToDisplayString());
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static IEnumerable<IMethodSymbol> GetMethodsHiddenByMethod(IMethodSymbol method, INamedTypeSymbol baseType)
        {
            while (true)
            {
                if (baseType?.BaseType == null)
                {
                    // We must be at System.Object, and the methods in System.Object cannot be hidden
                    yield break;
                }

                var baseMethods = baseType.GetMembers(method.Name)
                    .OfType<IMethodSymbol>()
                    .Where(x => !(x.IsStatic || x.IsVirtual)
                           && x.ReturnType.Equals(method.ReturnType)
                           && x.DeclaredAccessibility != Accessibility.Private
                           && x.Parameters.Length == method.Parameters.Length);

                foreach (var baseMethod in baseMethods)
                {
                    var isMethodHidden = false;

                    for (var i = 0; i < baseMethod.Parameters.Length; ++i)
                    {
                        var baseMethodParameter = baseMethod.Parameters[i];
                        var derivedMethodParameter = method.Parameters[i];

                        // All parameter names must match
                        if (baseMethodParameter.Name != derivedMethodParameter.Name)
                        {
                            isMethodHidden = false;
                            break;
                        }

                        // All parameter types must match except for those that are subtypes of the
                        // derived method's parameter type - there must be at least one.
                        if (!baseMethodParameter.Type.Equals(derivedMethodParameter.Type))
                        {
                            if (!baseMethodParameter.Type.DerivesFrom(derivedMethodParameter.Type))
                            {
                                isMethodHidden = false;
                                break;
                            }

                            isMethodHidden = true;
                        }
                    }

                    if (isMethodHidden)
                    {
                        yield return baseMethod;
                    }
                }

                // Repeat the same checks with the base type of this base type
                baseType = baseType.BaseType;
            }
        }
    }
}
