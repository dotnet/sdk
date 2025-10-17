// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.Lightup;
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

                // Check parameter references for writes
                blockStartContext.RegisterOperationAction(operationContext =>
                {
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

                // Check for writes through property-based indexers (Span<T> uses property indexers)
                blockStartContext.RegisterOperationAction(operationContext =>
                {
                    var propertyRef = (IPropertyReferenceOperation)operationContext.Operation;
                    if (propertyRef.Instance is not IParameterReferenceOperation paramRef ||
                        !candidateParameters.ContainsKey(paramRef.Parameter))
                    {
                        return;
                    }

                    // Check if this property reference is on the left side of an assignment
                    // This handles Span<T> indexer writes like: span[0] = value
                    if (propertyRef.Parent is IAssignmentOperation assignment && assignment.Target == propertyRef)
                    {
                        candidateParameters.TryRemove(paramRef.Parameter, out _);
                        return;
                    }

                    // Check if this property reference is being passed as ref/out argument
                    // This handles cases like: SwapIfGreater(ref span[0], ref span[1])
                    if (propertyRef.Parent is IArgumentOperation argument && 
                        argument.Parameter?.RefKind is RefKind.Ref or RefKind.Out)
                    {
                        candidateParameters.TryRemove(paramRef.Parameter, out _);
                        return;
                    }

                    // Check if this property reference is used in a ref variable declaration
                    // This handles cases like: ref int i = ref span[0];
                    // Walk up the parent chain to find a VariableDeclaratorOperation with RefKind
                    var parent = propertyRef.Parent;
                    while (parent != null)
                    {
                        if (parent is IVariableDeclaratorOperation variableDeclarator &&
                            variableDeclarator.Symbol.RefKind != RefKind.None)
                        {
                            candidateParameters.TryRemove(paramRef.Parameter, out _);
                            return;
                        }
                        if (parent is IBlockOperation or IMethodBodyOperation)
                        {
                            // Stop at block/method boundaries
                            break;
                        }
                        parent = parent.Parent;
                    }

                    // Check if this property reference is being returned by ref
                    // This handles cases where span[0] is being returned by ref
                    var containingMethod = operationContext.ContainingSymbol as IMethodSymbol;
                    if (containingMethod?.ReturnsByRef == true &&
                        propertyRef.Parent is IReturnOperation)
                    {
                        candidateParameters.TryRemove(paramRef.Parameter, out _);
                    }
                }, OperationKind.PropertyReference);

                // Check for writes through implicit indexers (Index/Range operators like span[^1] or span[1..5])
                blockStartContext.RegisterOperationAction(operationContext =>
                {
                    // Get the instance from the first child of the implicit indexer reference
                    var instance = operationContext.Operation.Children.FirstOrDefault();

                    // Check if this element reference is on the left side of an assignment
                    if (instance is IParameterReferenceOperation paramRef &&
                        candidateParameters.ContainsKey(paramRef.Parameter) &&
                        operationContext.Operation.Parent is IAssignmentOperation assignment && 
                        assignment.Target == operationContext.Operation)
                    {
                        candidateParameters.TryRemove(paramRef.Parameter, out _);
                    }
                }, OperationKindEx.ImplicitIndexerReference);

                // Check for parameters passed to methods that might write to them
                blockStartContext.RegisterOperationAction(operationContext =>
                {
                    var argument = (IArgumentOperation)operationContext.Operation;
                    // If parameter is passed to a method that expects a non-readonly type, remove it
                    if (argument.Value is IParameterReferenceOperation paramRef &&
                        candidateParameters.ContainsKey(paramRef.Parameter) &&
                        argument.Parameter?.Type is INamedTypeSymbol argType &&
                        paramRef.Parameter.Type is INamedTypeSymbol &&
                        !SymbolEqualityComparer.Default.Equals(argType.OriginalDefinition, readOnlySpan) &&
                        !SymbolEqualityComparer.Default.Equals(argType.OriginalDefinition, readOnlyMemory))
                    {
                        // Target expects writable Span/Memory, so this parameter must remain writable
                        candidateParameters.TryRemove(paramRef.Parameter, out _);
                    }
                }, OperationKind.Argument);

                // Check for invocations where the parameter is used as an extension method receiver
                // and the method doesn't exist on the readonly version
                blockStartContext.RegisterOperationAction(operationContext =>
                {
                    var invocation = (IInvocationOperation)operationContext.Operation;
                    // Check if this extension method's first parameter (the 'this' parameter) is a Span/Memory
                    // If the extension method takes Span<T> or Memory<T> (not readonly versions),
                    // then the parameter must remain writable
                    if (invocation.TargetMethod.IsExtensionMethod &&
                        invocation.Arguments.Length > 0 &&
                        invocation.Arguments[0].Value is IParameterReferenceOperation paramRef &&
                        candidateParameters.ContainsKey(paramRef.Parameter) &&
                        invocation.TargetMethod.Parameters[0].Type is INamedTypeSymbol firstParamNamedType &&
                        (SymbolEqualityComparer.Default.Equals(firstParamNamedType.OriginalDefinition, span) ||
                         SymbolEqualityComparer.Default.Equals(firstParamNamedType.OriginalDefinition, memory)))
                    {
                        candidateParameters.TryRemove(paramRef.Parameter, out _);
                    }
                }, OperationKind.Invocation);

                // Check for parameters returned from the method
                blockStartContext.RegisterOperationAction(operationContext =>
                {
                    var returnOp = (IReturnOperation)operationContext.Operation;
                    if (returnOp.ReturnedValue is IParameterReferenceOperation paramRef &&
                        candidateParameters.ContainsKey(paramRef.Parameter))
                    {
                        // If method returns by ref, the parameter must remain writable
                        if (methodSymbol.ReturnsByRef)
                        {
                            candidateParameters.TryRemove(paramRef.Parameter, out _);
                            return;
                        }

                        // Returning the parameter means it escapes, so we need to check if return type is compatible
                        if (methodSymbol.ReturnType is INamedTypeSymbol returnNamedType &&
                            !SymbolEqualityComparer.Default.Equals(returnNamedType.OriginalDefinition, readOnlySpan) &&
                            !SymbolEqualityComparer.Default.Equals(returnNamedType.OriginalDefinition, readOnlyMemory))
                        {
                            // Return type requires writable Span/Memory
                            candidateParameters.TryRemove(paramRef.Parameter, out _);
                        }
                    }
                }, OperationKind.Return);

                // Check for parameters stored in fields or properties
                blockStartContext.RegisterOperationAction(operationContext =>
                {
                    var fieldRef = (IFieldReferenceOperation)operationContext.Operation;
                    if (fieldRef.Parent is IAssignmentOperation assignment && 
                        assignment.Target == fieldRef &&
                        assignment.Value is IParameterReferenceOperation paramRef &&
                        candidateParameters.ContainsKey(paramRef.Parameter))
                    {
                        // Parameter is being stored to a field - must remain writable if field type is not readonly
                        if (fieldRef.Field.Type is INamedTypeSymbol fieldNamedType &&
                            !SymbolEqualityComparer.Default.Equals(fieldNamedType.OriginalDefinition, readOnlySpan) &&
                            !SymbolEqualityComparer.Default.Equals(fieldNamedType.OriginalDefinition, readOnlyMemory))
                        {
                            candidateParameters.TryRemove(paramRef.Parameter, out _);
                        }
                    }
                }, OperationKind.FieldReference);

                // Check for assignments where parameter is the value being assigned
                blockStartContext.RegisterOperationAction(operationContext =>
                {
                    var assignment = (IAssignmentOperation)operationContext.Operation;
                    // Check if the target type is compatible with readonly version
                    if (assignment.Value is IParameterReferenceOperation paramRef &&
                        candidateParameters.ContainsKey(paramRef.Parameter) &&
                        assignment.Target.Type is INamedTypeSymbol targetNamedType &&
                        !SymbolEqualityComparer.Default.Equals(targetNamedType.OriginalDefinition, readOnlySpan) &&
                        !SymbolEqualityComparer.Default.Equals(targetNamedType.OriginalDefinition, readOnlyMemory))
                    {
                        // Target expects writable Span/Memory
                        candidateParameters.TryRemove(paramRef.Parameter, out _);
                    }
                }, OperationKind.SimpleAssignment);

                // Check for array creation where parameter is an element
                blockStartContext.RegisterOperationAction(operationContext =>
                {
                    var arrayCreation = (IArrayCreationOperation)operationContext.Operation;
                    if (arrayCreation.Initializer != null && arrayCreation.Type is IArrayTypeSymbol arrayType)
                    {
                        foreach (var element in arrayCreation.Initializer.ElementValues)
                        {
                            // Check if array element type is compatible with readonly version
                            if (element is IParameterReferenceOperation paramRef &&
                                candidateParameters.ContainsKey(paramRef.Parameter) &&
                                arrayType.ElementType is INamedTypeSymbol elementNamedType &&
                                !SymbolEqualityComparer.Default.Equals(elementNamedType.OriginalDefinition, readOnlySpan) &&
                                !SymbolEqualityComparer.Default.Equals(elementNamedType.OriginalDefinition, readOnlyMemory))
                            {
                                // Array expects writable Span/Memory elements
                                candidateParameters.TryRemove(paramRef.Parameter, out _);
                            }
                        }
                    }
                }, OperationKind.ArrayCreation);

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
