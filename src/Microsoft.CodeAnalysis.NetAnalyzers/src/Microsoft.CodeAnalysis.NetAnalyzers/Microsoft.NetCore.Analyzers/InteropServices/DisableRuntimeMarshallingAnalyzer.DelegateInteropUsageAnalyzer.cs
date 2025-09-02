// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    internal sealed partial class DisableRuntimeMarshallingAnalyzer
    {
        private sealed class DelegateInteropUsageAnalyzer
        {
            private readonly IMethodSymbol? _getDelegateForFunctionPointerNonGeneric;
            private readonly IMethodSymbol? _getDelegateForFunctionPointerGeneric;
            private readonly IMethodSymbol? _getFunctionPointerForDelegateNonGeneric;
            private readonly IMethodSymbol? _getFunctionPointerForDelegateGeneric;
            private readonly AutoLayoutTypeCache _autoLayoutCache;
            private readonly INamedTypeSymbol _disableRuntimeMarshallingAttribute;

            public DelegateInteropUsageAnalyzer(Compilation compilation, AutoLayoutTypeCache autoLayoutCache, INamedTypeSymbol disableRuntimeMarshallingAttribute)
            {
                if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesMarshal, out var marshalType))
                {
                    var getDelegateForFunctionPointerMethods = marshalType.GetMembers("GetDelegateForFunctionPointer").OfType<IMethodSymbol>();
                    _getDelegateForFunctionPointerNonGeneric = getDelegateForFunctionPointerMethods.FirstOrDefault(m => m.TypeArguments.Length == 0);
                    _getDelegateForFunctionPointerGeneric = getDelegateForFunctionPointerMethods.FirstOrDefault(m => m.TypeArguments.Length != 0);
                    var getFunctionPointerForDelegateMethods = marshalType.GetMembers("GetFunctionPointerForDelegate").OfType<IMethodSymbol>();
                    _getFunctionPointerForDelegateNonGeneric = getFunctionPointerForDelegateMethods.FirstOrDefault(m => m.TypeArguments.Length == 0);
                    _getFunctionPointerForDelegateGeneric = getFunctionPointerForDelegateMethods.FirstOrDefault(m => m.TypeArguments.Length != 0);
                }

                _autoLayoutCache = autoLayoutCache;
                _disableRuntimeMarshallingAttribute = disableRuntimeMarshallingAttribute;
            }

            public void RegisterActions(CompilationStartAnalysisContext context, bool hasDisableRuntimeMarshallingAttribute)
            {
                context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);

                if (!hasDisableRuntimeMarshallingAttribute)
                {
                    context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);

                    context.RegisterOperationAction(AnalyzeLocalFunction, OperationKind.LocalFunction);
                }
            }

            private void AnalyzeInvocation(OperationAnalysisContext context)
            {
                IInvocationOperation operation = (IInvocationOperation)context.Operation;
                if (SymbolEqualityComparer.Default.Equals(operation.TargetMethod.OriginalDefinition, _getDelegateForFunctionPointerNonGeneric))
                {
                    if (operation.Arguments.Length == 2 && operation.Arguments.GetArgumentForParameterAtIndex(1) is { Value: ITypeOfOperation { TypeOperand: INamedTypeSymbol { TypeKind: TypeKind.Delegate } delegateType } } arg)
                    {
                        AnalyzeDelegateType(context, arg, delegateType);
                    }
                }
                else if (SymbolEqualityComparer.Default.Equals(operation.TargetMethod.OriginalDefinition, _getDelegateForFunctionPointerGeneric))
                {
                    if (operation.TargetMethod.TypeArguments.Length == 1 && operation.TargetMethod.TypeArguments[0] is INamedTypeSymbol { TypeKind: TypeKind.Delegate } delegateType)
                    {
                        AnalyzeDelegateType(context, operation, delegateType);
                    }
                }
                else if (SymbolEqualityComparer.Default.Equals(operation.TargetMethod.OriginalDefinition, _getFunctionPointerForDelegateNonGeneric))
                {
                    if (operation.HasArgument<IConversionOperation>(out var conversion))
                    {
                        if (conversion.Operand.Type is INamedTypeSymbol { TypeKind: TypeKind.Delegate } delegateType)
                        {
                            AnalyzeDelegateType(context, conversion.Operand, delegateType);
                        }
                    }
                }
                else if (SymbolEqualityComparer.Default.Equals(operation.TargetMethod.OriginalDefinition, _getFunctionPointerForDelegateGeneric))
                {
                    if (operation.HasArgument<IOperation>(out var argumentValue))
                    {
                        if (argumentValue.Type is INamedTypeSymbol { TypeKind: TypeKind.Delegate } delegateType)
                        {
                            AnalyzeDelegateType(context, argumentValue, delegateType);
                        }
                    }
                }

                void AnalyzeDelegateType(OperationAnalysisContext context, IOperation operation, INamedTypeSymbol delegateType)
                {
                    if (delegateType.ContainingAssembly.HasAnyAttribute(_disableRuntimeMarshallingAttribute))
                    {
                        AnalyzeMethodSignature(_autoLayoutCache, context.ReportDiagnostic, delegateType.DelegateInvokeMethod!, ImmutableArray.Create(operation.Syntax.GetLocation()), FeatureUnsupportedWhenRuntimeMarshallingDisabledDelegateUsage);
                    }
                }
            }

            private void AnalyzeMethod(SymbolAnalysisContext context)
            {
                AnalyzeMethod(context.ReportDiagnostic, (IMethodSymbol)context.Symbol);
            }

            public void AnalyzeLocalFunction(OperationAnalysisContext context)
            {
                var functionPointerInvocation = (ILocalFunctionOperation)context.Operation;
                AnalyzeMethod(context.ReportDiagnostic, functionPointerInvocation.Symbol);
            }

            private void AnalyzeMethod(Action<Diagnostic> reportDiagnostic, IMethodSymbol symbol)
            {
                // Analyze delegate parameters and return values of P/Invokes.
                if (symbol.GetDllImportData() is not null)
                {
                    foreach (var param in symbol.Parameters)
                    {
                        if (param.Type.TypeKind == TypeKind.Delegate)
                        {
                            AnalyzeDelegateMethodSignature((INamedTypeSymbol)param.Type, param);
                        }
                        else if (param.Type is IArrayTypeSymbol { ElementType.TypeKind: TypeKind.Delegate })
                        {
                            AnalyzeDelegateMethodSignature((INamedTypeSymbol)((IArrayTypeSymbol)param.Type).ElementType, param);
                        }
                    }

                    if (symbol.ReturnType.TypeKind == TypeKind.Delegate)
                    {
                        AnalyzeDelegateMethodSignature((INamedTypeSymbol)symbol.ReturnType, symbol);
                    }
                    else if (symbol.ReturnType is IArrayTypeSymbol { ElementType.TypeKind: TypeKind.Delegate })
                    {
                        AnalyzeDelegateMethodSignature((INamedTypeSymbol)((IArrayTypeSymbol)symbol.ReturnType).ElementType, symbol);
                    }
                }

                void AnalyzeDelegateMethodSignature(INamedTypeSymbol delegateType, ISymbol signatureSymbol)
                {
                    if (delegateType.ContainingAssembly.HasAnyAttribute(_disableRuntimeMarshallingAttribute))
                    {
                        AnalyzeMethodSignature(_autoLayoutCache, reportDiagnostic, delegateType.DelegateInvokeMethod!, signatureSymbol.Locations, FeatureUnsupportedWhenRuntimeMarshallingDisabledDelegateUsage);
                    }
                }
            }
        }
    }
}
