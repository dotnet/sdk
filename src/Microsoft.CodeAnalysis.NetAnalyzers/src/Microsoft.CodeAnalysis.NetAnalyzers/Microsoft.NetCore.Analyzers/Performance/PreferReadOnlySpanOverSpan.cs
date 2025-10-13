// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Concurrent;
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
                // Skip if not a method, is override, is interface implementation, or doesn't match visibility
                if (blockStartContext.OwningSymbol is not IMethodSymbol methodSymbol ||
                    methodSymbol.IsOverride ||
                    methodSymbol.IsImplementationOfAnyInterfaceMember() ||
                    !blockStartContext.Options.MatchesConfiguredVisibility(Rule, methodSymbol, compilation,
                        defaultRequiredVisibility: SymbolVisibilityGroup.Internal | SymbolVisibilityGroup.Private))
                {
                    return;
                }

                // Track which parameters are writable Span/Memory types and whether they're written to
                ConcurrentDictionary<IParameterSymbol, INamedTypeSymbol>? candidateParameters = null;
                
                foreach (var parameter in methodSymbol.Parameters)
                {
                    if (IsConvertibleSpanOrMemoryParameter(parameter, span, memory, readOnlySpan, readOnlyMemory, out var readOnlyType) && readOnlyType != null)
                    {
                        candidateParameters ??= new ConcurrentDictionary<IParameterSymbol, INamedTypeSymbol>(SymbolEqualityComparer.Default);
                        candidateParameters.TryAdd(parameter, readOnlyType);
                    }
                }

                if (candidateParameters == null)
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
                            candidateParameters.TryRemove(parameterReference.Parameter, out _);
                        }
                    }
                }, OperationKind.ParameterReference);

                // Check for writes through properties
                blockStartContext.RegisterOperationAction(operationContext =>
                {
                    var propertyRef = (IPropertyReferenceOperation)operationContext.Operation;
                    if (propertyRef.Instance is IParameterReferenceOperation paramRef &&
                        candidateParameters.ContainsKey(paramRef.Parameter))
                    {
                        // Check if this property reference is on the left side of an assignment
                        if (propertyRef.Parent is IAssignmentOperation assignment && assignment.Target == propertyRef)
                        {
                            candidateParameters.TryRemove(paramRef.Parameter, out _);
                        }
                    }
                }, OperationKind.PropertyReference);

                // Check for writes through indexers (e.g., span[0] = value)
                blockStartContext.RegisterOperationAction(operationContext =>
                {
                    var arrayElementRef = (IArrayElementReferenceOperation)operationContext.Operation;
                    if (arrayElementRef.ArrayReference is IParameterReferenceOperation paramRef &&
                        candidateParameters.ContainsKey(paramRef.Parameter))
                    {
                        // Check if this element reference is on the left side of an assignment
                        if (arrayElementRef.Parent is IAssignmentOperation assignment && assignment.Target == arrayElementRef)
                        {
                            candidateParameters.TryRemove(paramRef.Parameter, out _);
                        }
                    }
                }, OperationKind.ArrayElementReference);

                // Check for parameters passed to methods that might write to them
                blockStartContext.RegisterOperationAction(operationContext =>
                {
                    var argument = (IArgumentOperation)operationContext.Operation;
                    if (argument.Value is IParameterReferenceOperation paramRef &&
                        candidateParameters.ContainsKey(paramRef.Parameter))
                    {
                        // If parameter is passed to a method that expects a non-readonly type, remove it
                        if (argument.Parameter?.Type is INamedTypeSymbol argType)
                        {
                            var paramType = paramRef.Parameter.Type as INamedTypeSymbol;
                            if (paramType != null && 
                                !SymbolEqualityComparer.Default.Equals(argType.OriginalDefinition, readOnlySpan) &&
                                !SymbolEqualityComparer.Default.Equals(argType.OriginalDefinition, readOnlyMemory))
                            {
                                // Target expects writable Span/Memory, so this parameter must remain writable
                                candidateParameters.TryRemove(paramRef.Parameter, out _);
                            }
                        }
                    }
                }, OperationKind.Argument);

                // Check for invocations where the parameter is used as an extension method receiver
                // and the method doesn't exist on the readonly version
                blockStartContext.RegisterOperationAction(operationContext =>
                {
                    var invocation = (IInvocationOperation)operationContext.Operation;
                    if (invocation.TargetMethod.IsExtensionMethod &&
                        invocation.Arguments.Length > 0 &&
                        invocation.Arguments[0].Value is IParameterReferenceOperation paramRef &&
                        candidateParameters.ContainsKey(paramRef.Parameter))
                    {
                        // Check if this extension method's first parameter (the 'this' parameter) is a Span/Memory
                        var firstParamType = invocation.TargetMethod.Parameters[0].Type;
                        if (firstParamType is INamedTypeSymbol firstParamNamedType)
                        {
                            // If the extension method takes Span<T> or Memory<T> (not readonly versions),
                            // then the parameter must remain writable
                            if (SymbolEqualityComparer.Default.Equals(firstParamNamedType.OriginalDefinition, span) ||
                                SymbolEqualityComparer.Default.Equals(firstParamNamedType.OriginalDefinition, memory))
                            {
                                candidateParameters.TryRemove(paramRef.Parameter, out _);
                            }
                        }
                    }
                }, OperationKind.Invocation);

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
            INamedTypeSymbol readOnlySpan,
            INamedTypeSymbol readOnlyMemory,
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
                if (namedType.TypeArguments.Length == 1)
                {
                    readOnlyType = readOnlySpan.Construct(namedType.TypeArguments[0]);
                    return true;
                }
            }
            else if (SymbolEqualityComparer.Default.Equals(originalDefinition, memory))
            {
                // Memory<T> -> ReadOnlyMemory<T>
                if (namedType.TypeArguments.Length == 1)
                {
                    readOnlyType = readOnlyMemory.Construct(namedType.TypeArguments[0]);
                    return true;
                }
            }

            return false;
        }


    }
}
