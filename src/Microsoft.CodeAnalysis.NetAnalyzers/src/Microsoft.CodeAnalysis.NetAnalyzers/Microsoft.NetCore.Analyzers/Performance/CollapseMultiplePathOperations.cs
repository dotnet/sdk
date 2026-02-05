// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Performance
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1877: <inheritdoc cref="CollapseMultiplePathOperationsTitle"/>
    /// Detects nested Path.Combine or Path.Join calls that can be collapsed into a single call.
    /// Example: Path.Combine(Path.Combine(a, b), c) -> Path.Combine(a, b, c)
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class CollapseMultiplePathOperationsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1877";
        internal const string MethodNameKey = "MethodName";
        internal const string ArgumentCountKey = "ArgumentCount";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            id: RuleId,
            title: CreateLocalizableResourceString(nameof(CollapseMultiplePathOperationsTitle)),
            messageFormat: CreateLocalizableResourceString(nameof(CollapseMultiplePathOperationsMessage)),
            category: DiagnosticCategory.Performance,
            ruleLevel: RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(CollapseMultiplePathOperationsDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationContext =>
            {
                var compilation = compilationContext.Compilation;

                // Get the Path type
                if (!compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIOPath, out var pathType))
                {
                    return;
                }

                // Get Span types (may be null if not available in the target framework)
                compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSpan1, out var spanType);
                compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemReadOnlySpan1, out var readOnlySpanType);

                // Get Combine and Join methods
                var combineMethods = ImmutableArray.CreateBuilder<IMethodSymbol>();
                var joinMethods = ImmutableArray.CreateBuilder<IMethodSymbol>();

                foreach (var member in pathType.GetMembers())
                {
                    if (member is IMethodSymbol method && method.IsStatic)
                    {
                        if (method.Name == "Combine" && IsStringReturningMethod(method))
                        {
                            combineMethods.Add(method);
                        }
                        else if (method.Name == "Join" && IsStringReturningMethod(method))
                        {
                            joinMethods.Add(method);
                        }
                    }
                }

                if (combineMethods.Count == 0 && joinMethods.Count == 0)
                {
                    return;
                }

                var combineMethodsArray = combineMethods.ToImmutable();
                var joinMethodsArray = joinMethods.ToImmutable();

                compilationContext.RegisterOperationAction(operationContext =>
                {
                    var invocation = (IInvocationOperation)operationContext.Operation;
                    AnalyzeInvocation(operationContext, invocation, pathType, spanType, readOnlySpanType, combineMethodsArray, joinMethodsArray);
                }, OperationKind.Invocation);
            });
        }

        private static bool IsStringReturningMethod(IMethodSymbol method)
        {
            return method.ReturnType.SpecialType == SpecialType.System_String;
        }

        private static void AnalyzeInvocation(
            OperationAnalysisContext context,
            IInvocationOperation invocation,
            INamedTypeSymbol pathType,
            INamedTypeSymbol? spanType,
            INamedTypeSymbol? readOnlySpanType,
            ImmutableArray<IMethodSymbol> combineMethods,
            ImmutableArray<IMethodSymbol> joinMethods)
        {
            var targetMethod = invocation.TargetMethod;
            
            // Early check: must have arguments, be a static method, and be on System.IO.Path
            if (invocation.Arguments.IsEmpty ||
                !targetMethod.IsStatic || 
                !SymbolEqualityComparer.Default.Equals(targetMethod.ContainingType, pathType))
            {
                return;
            }

            string? methodName = null;
            ImmutableArray<IMethodSymbol> methodsToCheck = default;

            // Check if this is a Combine or Join call
            foreach (var method in combineMethods)
            {
                if (SymbolEqualityComparer.Default.Equals(targetMethod.OriginalDefinition, method.OriginalDefinition))
                {
                    methodName = "Combine";
                    methodsToCheck = combineMethods;
                    break;
                }
            }

            if (methodName == null)
            {
                foreach (var method in joinMethods)
                {
                    if (SymbolEqualityComparer.Default.Equals(targetMethod.OriginalDefinition, method.OriginalDefinition))
                    {
                        methodName = "Join";
                        methodsToCheck = joinMethods;
                        break;
                    }
                }
            }

            if (methodName == null)
            {
                return;
            }

            // Check if this invocation is itself an argument to another Path.Combine/Join call
            // If so, skip it - we'll report on the outermost call only
            if (IsNestedInSimilarCall(invocation, methodsToCheck))
            {
                return;
            }

            // Check if any argument is itself a Path.Combine/Join call of the same method
            foreach (var argument in invocation.Arguments)
            {
                if (argument.Value is IInvocationOperation nestedInvocation)
                {
                    var nestedMethod = nestedInvocation.TargetMethod;

                    foreach (var method in methodsToCheck)
                    {
                        if (SymbolEqualityComparer.Default.Equals(nestedMethod.OriginalDefinition, method.OriginalDefinition))
                        {
                            // Found a nested call that can potentially be collapsed
                            // Count total arguments to ensure we don't exceed available overloads
                            int totalArgs = CountTotalArguments(invocation, methodsToCheck);

                            // For Combine and Join with string parameters, we can use params overload for any count
                            // Check if target framework has the overloads we need
                            if (CanCollapse(invocation, spanType, readOnlySpanType, totalArgs))
                            {
                                var properties = ImmutableDictionary.CreateBuilder<string, string?>();
                                properties.Add(MethodNameKey, methodName);
                                properties.Add(ArgumentCountKey, totalArgs.ToString());

                                context.ReportDiagnostic(invocation.CreateDiagnostic(Rule, properties.ToImmutable(), methodName));
                                return;
                            }
                        }
                    }
                }
            }
        }

        private static bool IsNestedInSimilarCall(IInvocationOperation invocation, ImmutableArray<IMethodSymbol> methodsToCheck)
        {
            // Walk up the tree to see if this invocation is an argument to another Path.Combine/Join call
            var current = invocation.Parent;
            while (current != null)
            {
                // Check if we're inside an argument
                if (current is IArgumentOperation)
                {
                    // Get the invocation that contains this argument
                    var grandParent = current.Parent;
                    if (grandParent is IInvocationOperation parentInvocation)
                    {
                        var parentMethod = parentInvocation.TargetMethod;
                        foreach (var method in methodsToCheck)
                        {
                            if (SymbolEqualityComparer.Default.Equals(parentMethod.OriginalDefinition, method.OriginalDefinition))
                            {
                                return true;
                            }
                        }
                    }
                }
                current = current.Parent;
            }
            return false;
        }

        private static int CountTotalArguments(IInvocationOperation invocation, ImmutableArray<IMethodSymbol> methodsToCheck)
        {
            int count = 0;

            foreach (var argument in invocation.Arguments)
            {
                if (argument.Value is IInvocationOperation nestedInvocation)
                {
                    var nestedMethod = nestedInvocation.TargetMethod;
                    bool isNestedPathMethod = false;

                    foreach (var method in methodsToCheck)
                    {
                        if (SymbolEqualityComparer.Default.Equals(nestedMethod.OriginalDefinition, method.OriginalDefinition))
                        {
                            isNestedPathMethod = true;
                            break;
                        }
                    }

                    if (isNestedPathMethod)
                    {
                        // Recursively count arguments from nested call
                        count += CountTotalArguments(nestedInvocation, methodsToCheck);
                    }
                    else
                    {
                        count++;
                    }
                }
                else
                {
                    count++;
                }
            }

            return count;
        }

        private static bool CanCollapse(IInvocationOperation invocation, INamedTypeSymbol? spanType, INamedTypeSymbol? readOnlySpanType, int totalArgs)
        {
            // We can collapse if there's a params overload available
            // Path.Combine(params string[]) and Path.Join(params string[]) exist in supported frameworks
            // The only constraint is that we need at least 2 arguments total
            
            // However, if any of the parameters are spans, we can't use the params overload
            // Check if any argument involves span types
            if (HasSpanArguments(invocation, spanType, readOnlySpanType))
            {
                // With span arguments, we're limited to the non-params overloads
                // which support up to 4 arguments for Join(ReadOnlySpan<char>)
                return totalArgs >= 2 && totalArgs <= 4;
            }

            return totalArgs >= 2;
        }

        private static bool HasSpanArguments(IInvocationOperation invocation, INamedTypeSymbol? spanType, INamedTypeSymbol? readOnlySpanType)
        {
            foreach (var argument in invocation.Arguments)
            {
                if (IsSpanType(argument.Value.Type, spanType, readOnlySpanType))
                {
                    return true;
                }

                // Check nested invocations
                if (argument.Value is IInvocationOperation nestedInvocation)
                {
                    if (HasSpanArguments(nestedInvocation, spanType, readOnlySpanType))
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        private static bool IsSpanType(ITypeSymbol? type, INamedTypeSymbol? spanType, INamedTypeSymbol? readOnlySpanType)
        {
            if (type is not INamedTypeSymbol namedType)
            {
                return false;
            }

            // Use symbol comparison with the span types looked up from compilation
            var originalDefinition = namedType.OriginalDefinition;
            return (spanType != null && SymbolEqualityComparer.Default.Equals(originalDefinition, spanType)) ||
                   (readOnlySpanType != null && SymbolEqualityComparer.Default.Equals(originalDefinition, readOnlySpanType));
        }
    }
}
