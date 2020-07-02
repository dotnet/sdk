// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    /// <summary>
    /// CA1024: Use properties where appropriate
    ///
    /// Cause:
    /// A public or protected method has a name that starts with Get, takes no parameters, and returns a value that is not an array.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UsePropertiesWhereAppropriateAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1024";
        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UsePropertiesWhereAppropriateTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UsePropertiesWhereAppropriateMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.UsePropertiesWhereAppropriateDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                         s_localizableTitle,
                                                                         s_localizableMessage,
                                                                         DiagnosticCategory.Design,
                                                                         RuleLevel.Disabled,    // Heuristic based rule.
                                                                         description: s_localizableDescription,
                                                                         isPortedFxCopRule: true,
                                                                         isDataflowRule: false,
                                                                         isEnabledByDefaultInFxCopAnalyzers: false);
        private const string GetHashCodeName = "GetHashCode";
        private const string GetEnumeratorName = "GetEnumerator";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterOperationBlockStartAction(context =>
            {

                if (!(context.OwningSymbol is IMethodSymbol methodSymbol) ||
                    methodSymbol.ReturnsVoid ||
                    methodSymbol.ReturnType.Kind == SymbolKind.ArrayType ||
<<<<<<< HEAD
                    !
<<<<<<< HEAD
                    methodSymbol.Parameters.IsEmpty ||
                    !methodSymbol.MatchesConfiguredVisibility(context.Options, Rule, context.Compilation, context.CancellationToken) ||
=======
                    !methodSymbol.Parameters.IsEmpty ||
                    !methodSymbol.MatchesConfiguredVisibility(context.Options, Rule, context.CancellationToken) ||
>>>>>>> upstream/2.9.x
                    methodSymbol.IsAccessorMethod() ||
                    !IsPropertyLikeName(methodSymbol.Name))
                {
                    return;
                }

                // A few additional checks to reduce the noise for this diagnostic:
                // Ensure that the method is non-generic, non-virtual/override, has no overloads and doesn't have special names: 'GetHashCode' or 'GetEnumerator'.
                // Also avoid generating this diagnostic if the method body has any invocation expressions.
                // Also avoid implicit interface implementation (explicit are handled through the member accessibility)
                if (methodSymbol.IsGenericMethod ||
                    methodSymbol.IsVirtual ||
                    methodSymbol.IsOverride ||
                    methodSymbol.ContainingType.GetMembers(methodSymbol.Name).Length > 1 ||
                    methodSymbol.Name == GetHashCodeName ||
                    methodSymbol.Name == GetEnumeratorName ||
                    methodSymbol.IsImplementationOfAnyImplicitInterfaceMember())
                {
                    return;
                }

                bool hasInvocations = false;
                context.RegisterOperationAction(operationContext =>
                {
                    hasInvocations = true;
                }, OperationKind.Invocation);

                context.RegisterOperationBlockEndAction(endContext =>
                {
                    if (!hasInvocations)
                    {
                        endContext.ReportDiagnostic(endContext.OwningSymbol.CreateDiagnostic(Rule));
                    }
                });
            });
        }

        private static bool IsPropertyLikeName(string methodName)
        {
            return methodName.Length > 3 &&
                methodName.StartsWith("Get", StringComparison.OrdinalIgnoreCase) &&
                !char.IsLower(methodName[3]);
        }
    }
}
