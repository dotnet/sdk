// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Concurrent;
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
    /// CA1517: <inheritdoc cref="PreferReadOnlySpanOverSpanTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PreferReadOnlySpanOverSpanAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1517";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(PreferReadOnlySpanOverSpanTitle)),
            CreateLocalizableResourceString(nameof(PreferReadOnlySpanOverSpanMessage)),
            DiagnosticCategory.Maintainability,
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
            var memoryExtensions = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemMemoryExtensions);

            if (span is null || readOnlySpan is null || memory is null || readOnlyMemory is null || memoryExtensions is null)
            {
                return;
            }

            context.RegisterOperationBlockStartAction(blockStartContext =>
            {
                // Skip methods that can't be changed (virtual, override, interface, etc.)
                if (blockStartContext.OwningSymbol is not IMethodSymbol methodSymbol ||
                    methodSymbol.IsVirtual ||
                    methodSymbol.IsOverride ||
                    methodSymbol.ContainingType.TypeKind is TypeKind.Interface ||
                    methodSymbol.IsImplementationOfAnyInterfaceMember() ||
                    !blockStartContext.Options.MatchesConfiguredVisibility(Rule, methodSymbol, compilation, defaultRequiredVisibility: SymbolVisibilityGroup.Internal | SymbolVisibilityGroup.Private))
                {
                    return;
                }

                // Find candidate Span/Memory parameters
                ConcurrentDictionary<IParameterSymbol, INamedTypeSymbol>? candidateParameters = null;
                ConcurrentDictionary<IParameterSymbol, int> parameterReferenceCounts = new(SymbolEqualityComparer.Default);
                foreach (var parameter in methodSymbol.Parameters)
                {
                    if (IsConvertibleSpanOrMemoryParameter(parameter, span, memory, readOnlySpan, readOnlyMemory, out var readOnlyType) && readOnlyType is not null)
                    {
                        candidateParameters ??= new ConcurrentDictionary<IParameterSymbol, INamedTypeSymbol>(SymbolEqualityComparer.Default);
                        candidateParameters.TryAdd(parameter, readOnlyType);
                        parameterReferenceCounts.TryAdd(parameter, 0);
                    }
                }

                if (candidateParameters is not null)
                {
                    // Walk up from each relevant parameter reference looking to see whether it invalidates a read-only conversion.
                    blockStartContext.RegisterOperationAction(operationContext =>
                    {
                        IParameterSymbol parameter = ((IParameterReferenceOperation)operationContext.Operation).Parameter;
                        if (candidateParameters.ContainsKey(parameter))
                        {
                            parameterReferenceCounts.AddOrUpdate(parameter, 1, (_, count) => count + 1);
                            if (!IsUsageSafe(operationContext.Operation, parameter, candidateParameters, span, memory, readOnlySpan, readOnlyMemory, memoryExtensions, methodSymbol))
                            {
                                candidateParameters.TryRemove(parameter, out _);
                            }
                        }
                    }, OperationKind.ParameterReference);

                    // At the end, raise a diagnostic for any candidate parameters that weren't invalidated.
                    blockStartContext.RegisterOperationBlockEndAction(blockEndContext =>
                    {
                        foreach (var kvp in candidateParameters)
                        {
                            var parameter = kvp.Key;
                            if (parameterReferenceCounts.TryGetValue(parameter, out var refCount) &&
                                refCount is > 0)
                            {
                                blockEndContext.ReportDiagnostic(parameter.CreateDiagnostic(
                                    Rule,
                                    parameter.Name,
                                    kvp.Value.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                                    parameter.Type.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat)));
                            }
                        }
                    });
                }
            });
        }

        /// <summary>
        /// Walks up the parent chain from a parameter reference to determine if usage is safe for conversion to its read-only counterpart.
        /// </summary>
        private static bool IsUsageSafe(
            IOperation reference,
            IParameterSymbol parameter,
            ConcurrentDictionary<IParameterSymbol, INamedTypeSymbol> candidateParameters,
            INamedTypeSymbol span,
            INamedTypeSymbol memory,
            INamedTypeSymbol readOnlySpan,
            INamedTypeSymbol readOnlyMemory,
            INamedTypeSymbol memoryExtensions,
            IMethodSymbol containingMethod)
        {
            if ((reference.GetValueUsageInfo(containingMethod) & ValueUsageInfo.WritableReference) == ValueUsageInfo.WritableReference)
            {
                return false;
            }

            // Walk up the parent chain to find how this reference is being used.
            for (var current = reference.Parent; current is not null; current = current.Parent)
            {
                switch (current)
                {
                    // Check method calls off of the span/memory parameter.
                    case IInvocationOperation invocation:
                        // If this isn't invoked off of the parameter, skip it.
                        if (invocation.Instance is not IParameterReferenceOperation instParamRef ||
                            !SymbolEqualityComparer.Default.Equals(instParamRef.Parameter, parameter))
                        {
                            return false;
                        }

                        if (!IsInstanceMethodSafe(invocation, span, memory, readOnlySpan, readOnlyMemory) ||
                            invocation.TargetMethod.ReturnsByRef)
                        {
                            return false;
                        }

                        // If the method returns Span/Memory and is passed to a method expecting writable type, it's unsafe
                        if (invocation.Type is INamedTypeSymbol invocationResultType &&
                            IsSpanOrMemory(invocationResultType.OriginalDefinition, span, memory))
                        {
                            // Check if the invocation is used as an argument
                            if (invocation.Parent is IArgumentOperation arg)
                            {
                                return IsArgumentSafe(parameter, arg, readOnlySpan, readOnlyMemory, memoryExtensions);
                            }

                            // Continue checking parent chain; invalid consumption of a writeable result
                            // such as assignment to a writeable local will be caught higher up, e.g. by
                            // variable declaration checks.
                            continue;
                        }

                        // Method doesn't return Span/Memory, it's safe.
                        return true;

                    // Check property accesses off of the span/memory parameter.
                    case IPropertyReferenceOperation propRef:
                        if (propRef.Instance is IParameterReferenceOperation propParamRef &&
                            SymbolEqualityComparer.Default.Equals(propParamRef.Parameter, parameter))
                        {
                            if (IsPropertyAccessSafe(propRef, containingMethod))
                            {
                                // If the property returns Span/Memory (like range indexer), only safe if used as argument.
                                if (propRef.Type is INamedTypeSymbol resultType &&
                                    IsSpanOrMemory(resultType.OriginalDefinition, span, memory))
                                {
                                    return propRef.Parent is IArgumentOperation arg && IsArgumentSafe(parameter, arg, readOnlySpan, readOnlyMemory, memoryExtensions);
                                }

                                // Property access is safe.
                                return true;
                            }

                            // Propery access isn't safe.
                            return false;
                        }

                        continue;

                    case IArgumentOperation argument:
                        return IsArgumentSafe(parameter, argument, readOnlySpan, readOnlyMemory, memoryExtensions);

                    case IReturnOperation returnOp:
                        return IsReturnSafe(containingMethod, readOnlySpan, readOnlyMemory);

                    case ISimpleAssignmentOperation assignment:
                        return IsAssignmentTargetSafe(parameter, assignment, readOnlySpan, readOnlyMemory);

                    // Walk up through any passthrough operations.
                    case IAwaitOperation:
                    case IBlockOperation:
                    case IConditionalAccessOperation:
                    case IConstantPatternOperation:
                    case IDeclarationPatternOperation:
                    case IExpressionStatementOperation:
                    case IIsPatternOperation:
                    case IPropertySubpatternOperation:
                    case IRangeOperation:
                    case IRecursivePatternOperation:
                    case IRelationalPatternOperation:
                    case ISwitchExpressionArmOperation:
                    case ISwitchExpressionOperation:
                    case IVariableDeclarationGroupOperation:
                    case IVariableDeclarationOperation:
                        continue;

                    // These operation just read. They're safe and we can stop walking as the span/memory consumption ends in these constructs.
                    case IBinaryOperation:
                    case ICoalesceOperation:
                    case IConversionOperation conversion:
                    case IForEachLoopOperation:
                    case IInterpolatedStringOperation:
                    case IUnaryOperation:
                        return true;

                    // Anything else treat as unsafe.
                    default:
                        return false;
                }
            }

            // If we walked all the way up without finding any usage, it's safe (just reading).
            return true;
        }

        private static bool IsInstanceMethodSafe(
            IInvocationOperation invocation,
            INamedTypeSymbol span,
            INamedTypeSymbol memory,
            INamedTypeSymbol readOnlySpan,
            INamedTypeSymbol readOnlyMemory)
        {
            if (invocation.Instance?.Type is not INamedTypeSymbol instanceType)
            {
                return true;
            }

            // Check if this is a Span/Memory method
            if (!IsSpanOrMemory(instanceType.OriginalDefinition, span, memory))
            {
                return true;
            }

            // Find the readonly counterpart
            var readOnlyCounterpart = SymbolEqualityComparer.Default.Equals(instanceType.OriginalDefinition, span) ?
                readOnlySpan :
                readOnlyMemory;

            // Check if the method exists on the readonly version
            return readOnlyCounterpart.Construct(instanceType.TypeArguments.ToArray())
                .GetMembers(invocation.TargetMethod.Name)
                .OfType<IMethodSymbol>()
                .Any(m => m.ParametersAreSame(invocation.TargetMethod));
        }

        private static bool IsPropertyAccessSafe(IPropertyReferenceOperation propRef, IMethodSymbol containingMethod)
        {
            // Unsafe if indexer is being written to
            if (propRef.Parent is IAssignmentOperation assignment && assignment.Target == propRef)
            {
                return false;
            }

            // Unsafe if indexer is part of increment/decrement operation (e.g., data[i]++)
            if (propRef.Parent is IIncrementOrDecrementOperation)
            {
                return false;
            }

            // Unsafe if indexer result is passed as ref/out
            if (propRef.Parent is IArgumentOperation argument &&
                argument.Parameter?.RefKind is RefKind.Ref or RefKind.Out)
            {
                return false;
            }

            // Unsafe if stored in ref local
            // Check both direct parent and through initializer (ref int x = ref data[0])
            IVariableDeclaratorOperation? declarator = propRef.Parent as IVariableDeclaratorOperation;
            if (declarator is null && propRef.Parent is IVariableInitializerOperation initializer)
            {
                declarator = initializer.Parent as IVariableDeclaratorOperation;
            }
            
            if (declarator is not null && declarator.Symbol.RefKind != RefKind.None)
            {
                return false;
            }

            // Unsafe if returned as ref (indexer result from readonly span doesn't support ref returns)
            if (propRef.Parent is IReturnOperation && containingMethod.ReturnsByRef)
            {
                return false;
            }

            return true;
        }

        private static bool IsArgumentSafe(
            IParameterSymbol parameter,
            IArgumentOperation argument,
            INamedTypeSymbol readOnlySpan,
            INamedTypeSymbol readOnlyMemory,
            INamedTypeSymbol memoryExtensions)
        {
            // Special-case Span/Memory-accepting methods we know don't mutate any parameters.
            if (argument.Parent is IInvocationOperation invocation &&
                SymbolEqualityComparer.Default.Equals(invocation.TargetMethod.ContainingType, memoryExtensions))
            {
                if (invocation.TargetMethod.Name.Contains("BinarySearch", StringComparison.Ordinal) ||
                    invocation.TargetMethod.Name.Contains("Contains", StringComparison.Ordinal) ||
                    invocation.TargetMethod.Name.Contains("ContainsAny", StringComparison.Ordinal) ||
                    invocation.TargetMethod.Name.Contains("Count", StringComparison.Ordinal) ||
                    invocation.TargetMethod.Name.Equals("EndsWith", StringComparison.Ordinal) ||
                    invocation.TargetMethod.Name.Contains("IndexOf", StringComparison.Ordinal) ||
                    invocation.TargetMethod.Name.Contains("IndexOfAny", StringComparison.Ordinal) ||
                    invocation.TargetMethod.Name.Equals("StartsWith", StringComparison.Ordinal) ||
                    invocation.TargetMethod.Name.Equals("SequenceEqual", StringComparison.Ordinal) ||
                    invocation.TargetMethod.Name.Equals("Trim", StringComparison.Ordinal))
                {
                    return true;
                }
            }

            // Special-case when the argument is for the same parameter we're analyzing, e.g. a recursive call,
            // since by definition that usage is safe.
            if (SymbolEqualityComparer.Default.Equals(argument.Parameter, parameter))
            {
                return true;
            }

            // If the associated parameter is read-only, it's safe.
            if (argument.Parameter?.Type is INamedTypeSymbol paramType)
            {
                return IsReadOnlySpanOrMemory(paramType.OriginalDefinition, readOnlySpan, readOnlyMemory);
            }

            // Otherwise, we have to assume the method could mutate it.
            return false;
        }

        private static bool IsReturnSafe(
            IMethodSymbol containingMethod,
            INamedTypeSymbol readOnlySpan,
            INamedTypeSymbol readOnlyMemory)
        {
            // Unsafe if ref returning.
            if (containingMethod.ReturnsByRef)
            {
                return false;
            }

            // Need to be able to get the return type.
            if (containingMethod.ReturnType is not INamedTypeSymbol returnType)
            {
                return false;
            }

            // If the return type is a primitive, it can't have smuggled out the reference.
            // And if it's ReadOnlySpan/ReadOnlyMemory, then converting it is fine.
            return
                returnType.OriginalDefinition.IsPrimitiveType() ||
                IsReadOnlySpanOrMemory(returnType.OriginalDefinition, readOnlySpan, readOnlyMemory);
        }

        private static bool IsAssignmentTargetSafe(
            IParameterSymbol parameter,
            ISimpleAssignmentOperation assignment,
            INamedTypeSymbol readOnlySpan,
            INamedTypeSymbol readOnlyMemory)
        {
            if (assignment.Target.Type is INamedTypeSymbol targetType)
            {
                // Safe if assigning to read-only type
                if (IsReadOnlySpanOrMemory(targetType.OriginalDefinition, readOnlySpan, readOnlyMemory))
                {
                    return true;
                }

                // Safe if assigning back to same parameter (e.g. param = param.Slice(...)), as in that
                // case we're already tracking the parameter, and if we change the type of the parameter,
                // we're also changing the type of the destination.
                if (assignment.Target is IParameterReferenceOperation paramRef &&
                    SymbolEqualityComparer.Default.Equals(paramRef.Parameter, parameter))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsConvertibleSpanOrMemoryParameter(
            IParameterSymbol parameter,
            INamedTypeSymbol span,
            INamedTypeSymbol memory,
            INamedTypeSymbol readOnlySpan,
            INamedTypeSymbol readOnlyMemory,
            out INamedTypeSymbol? readOnlyType)
        {
            if (parameter.RefKind is RefKind.None &&
                parameter.Type is INamedTypeSymbol namedType)
            {
                var originalDefinition = namedType.OriginalDefinition;

                if (SymbolEqualityComparer.Default.Equals(originalDefinition, span))
                {
                    if (namedType.TypeArguments.Length is 1)
                    {
                        readOnlyType = readOnlySpan.Construct(namedType.TypeArguments[0]);
                        return true;
                    }
                }
                else if (SymbolEqualityComparer.Default.Equals(originalDefinition, memory))
                {
                    if (namedType.TypeArguments.Length is 1)
                    {
                        readOnlyType = readOnlyMemory.Construct(namedType.TypeArguments[0]);
                        return true;
                    }
                }
            }

            readOnlyType = null;
            return false;
        }

        private static bool IsSpanOrMemory(INamedTypeSymbol target, INamedTypeSymbol span, INamedTypeSymbol memory) =>
            SymbolEqualityComparer.Default.Equals(target, span) ||
            SymbolEqualityComparer.Default.Equals(target, memory);

        private static bool IsReadOnlySpanOrMemory(INamedTypeSymbol target, INamedTypeSymbol readOnlySpan, INamedTypeSymbol readOnlyMemory) =>
            SymbolEqualityComparer.Default.Equals(target, readOnlySpan) ||
            SymbolEqualityComparer.Default.Equals(target, readOnlyMemory);
    }
}
