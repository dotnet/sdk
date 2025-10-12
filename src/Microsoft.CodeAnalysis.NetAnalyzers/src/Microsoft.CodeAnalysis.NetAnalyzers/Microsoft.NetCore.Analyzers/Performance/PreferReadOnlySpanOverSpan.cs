// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1876: <inheritdoc cref="PreferReadOnlySpanOverSpanTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferReadOnlySpanOverSpanAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1876";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(PreferReadOnlySpanOverSpanTitle)),
            CreateLocalizableResourceString(nameof(PreferReadOnlySpanOverSpanMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(PreferReadOnlySpanOverSpanDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            var compilation = context.Compilation;
            var span = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSpan1);
            var readOnlySpan = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlySpan1);
            var memory = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemMemory1);
            var readOnlyMemory = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlyMemory1);

            if (span == null || readOnlySpan == null || memory == null || readOnlyMemory == null)
            {
                return;
            }

            context.RegisterOperationBlockStartAction(blockStartContext =>
            {
                if (blockStartContext.OwningSymbol is not IMethodSymbol methodSymbol)
                {
                    return;
                }

                // Skip if method is override (don't analyze overridden members)
                if (methodSymbol.IsOverride)
                {
                    return;
                }

                // Skip if method is interface implementation
                if (methodSymbol.ExplicitInterfaceImplementations.Length > 0)
                {
                    return;
                }

                // Check visibility
                if (!blockStartContext.Options.MatchesConfiguredVisibility(Rule, methodSymbol, compilation,
                    defaultRequiredVisibility: SymbolVisibilityGroup.Internal | SymbolVisibilityGroup.Private))
                {
                    return;
                }

                // Track which parameters are writable Span/Memory types and whether they're written to
                var candidateParameters = ImmutableDictionary.CreateBuilder<IParameterSymbol, INamedTypeSymbol>();
                
                foreach (var parameter in methodSymbol.Parameters)
                {
                    if (IsConvertibleSpanOrMemoryParameter(parameter, span, memory, out var readOnlyType) && readOnlyType != null)
                    {
                        candidateParameters.Add(parameter, readOnlyType);
                    }
                }

                if (candidateParameters.Count == 0)
                {
                    return;
                }

                blockStartContext.RegisterOperationAction(operationContext =>
                {
                    // Check parameter references for writes
                    if (operationContext.Operation is IParameterReferenceOperation parameterReference &&
                        candidateParameters.ContainsKey(parameterReference.Parameter))
                    {
                        var valueUsage = operationContext.Operation.GetValueUsageInfo(methodSymbol);
                        
                        // If the parameter is written to or has a writable reference, remove it from candidates
                        if ((valueUsage & (ValueUsageInfo.Write | ValueUsageInfo.WritableReference)) != 0)
                        {
                            candidateParameters.Remove(parameterReference.Parameter);
                        }
                    }
                }, OperationKind.ParameterReference);

                blockStartContext.RegisterOperationBlockEndAction(blockEndContext =>
                {
                    // Report diagnostics for parameters that were never written to
                    foreach (var kvp in candidateParameters)
                    {
                        var parameter = kvp.Key;
                        var readOnlyType = kvp.Value;

                        var diagnostic = parameter.CreateDiagnostic(
                            Rule,
                            parameter.Name,
                            readOnlyType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                            parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat));
                        blockEndContext.ReportDiagnostic(diagnostic);
                    }
                });
            });
        }

        private static bool IsConvertibleSpanOrMemoryParameter(
            IParameterSymbol parameter,
            INamedTypeSymbol span,
            INamedTypeSymbol memory,
            out INamedTypeSymbol? readOnlyType)
        {
            readOnlyType = null;

            if (parameter.Type is not INamedTypeSymbol namedType)
            {
                return false;
            }

            var originalDefinition = namedType.OriginalDefinition;

            if (SymbolEqualityComparer.Default.Equals(originalDefinition, span))
            {
                // Span<T> -> ReadOnlySpan<T>
                var readOnlySpan = span.ContainingNamespace.GetTypeMembers("ReadOnlySpan").FirstOrDefault();
                if (readOnlySpan != null && namedType.TypeArguments.Length == 1)
                {
                    readOnlyType = readOnlySpan.Construct(namedType.TypeArguments[0]);
                    return true;
                }
            }
            else if (SymbolEqualityComparer.Default.Equals(originalDefinition, memory))
            {
                // Memory<T> -> ReadOnlyMemory<T>
                var readOnlyMemory = memory.ContainingNamespace.GetTypeMembers("ReadOnlyMemory").FirstOrDefault();
                if (readOnlyMemory != null && namedType.TypeArguments.Length == 1)
                {
                    readOnlyType = readOnlyMemory.Construct(namedType.TypeArguments[0]);
                    return true;
                }
            }

            return false;
        }


    }
}
