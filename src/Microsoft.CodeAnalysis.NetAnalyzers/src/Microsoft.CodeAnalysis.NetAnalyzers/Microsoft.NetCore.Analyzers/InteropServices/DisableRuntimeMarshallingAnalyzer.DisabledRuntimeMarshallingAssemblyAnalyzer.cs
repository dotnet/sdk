// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.Lightup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    internal sealed partial class DisableRuntimeMarshallingAnalyzer
    {
        private sealed class DisabledRuntimeMarshallingAssemblyAnalyzer
        {
            private readonly INamedTypeSymbol? _unmanagedFunctionPointerAttribute;
            private readonly INamedTypeSymbol? _lcidConversionAttribute;
            private readonly ImmutableArray<ISymbol> _marshalMethods;
            private readonly AutoLayoutTypeCache _autoLayoutCache;

            public DisabledRuntimeMarshallingAssemblyAnalyzer(Compilation compilation, AutoLayoutTypeCache autoLayoutCache)
            {
                _unmanagedFunctionPointerAttribute = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesUnmanagedFunctionPoitnerAttribute);
                _lcidConversionAttribute = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesLCIDConversionAttribute);
                if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesMarshal, out var marshalType))
                {
                    var marshalMethods = ImmutableArray.CreateBuilder<ISymbol>();
                    marshalMethods.AddRange(marshalType.GetMembers("SizeOf"));
                    marshalMethods.AddRange(marshalType.GetMembers("OffsetOf"));
                    marshalMethods.AddRange(marshalType.GetMembers("StructureToPtr"));
                    marshalMethods.AddRange(marshalType.GetMembers("PtrToStructure"));
                    _marshalMethods = marshalMethods.ToImmutable();
                }

                _autoLayoutCache = autoLayoutCache;
            }

            public void RegisterActions(CompilationStartAnalysisContext context)
            {
                context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);

                context.RegisterOperationAction(AnalyzeLocalFunction, OperationKind.LocalFunction);

                context.RegisterOperationAction(AnalyzeMethodCall, OperationKind.Invocation);

                context.RegisterOperationAction(AnalyzeFunctionPointerCall, OperationKindEx.FunctionPointerInvocation);

                context.RegisterSymbolAction(AnalyzeEvent, SymbolKind.Event);

                if (_unmanagedFunctionPointerAttribute is not null)
                {
                    context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
                }
            }

            private void AnalyzeEvent(SymbolAnalysisContext context)
            {
                // The getter or setter of a static extern event can be a P/Invoke.
                IEventSymbol property = (IEventSymbol)context.Symbol;
                if (property.AddMethod is not null)
                {
                    AnalyzeMethod(context.ReportDiagnostic, property.AddMethod);
                }
                else if (property.RemoveMethod is not null)
                {
                    AnalyzeMethod(context.ReportDiagnostic, property.RemoveMethod);
                }
            }

            public void AnalyzeMethodCall(OperationAnalysisContext context)
            {
                IInvocationOperation invocation = (IInvocationOperation)context.Operation;

                if (_marshalMethods.Contains(invocation.TargetMethod.ConstructedFrom))
                {
                    bool canTransformToDisabledMarshallingEquivalent = CanTransformToDisabledMarshallingEquivalent(invocation);
                    context.ReportDiagnostic(invocation.CreateDiagnostic(
                        MethodUsesRuntimeMarshallingEvenWhenMarshallingDisabled,
                        ImmutableDictionary.Create<string, string?>().Add(CanConvertToDisabledMarshallingEquivalentKey, canTransformToDisabledMarshallingEquivalent ? "true" : null),
                        invocation.TargetMethod.ToDisplayString()));
                }

                static bool CanTransformToDisabledMarshallingEquivalent(IInvocationOperation invocation)
                {
                    return invocation.TargetMethod.Name switch
                    {
                        "OffsetOf" => false,
                        "SizeOf" => invocation.TargetMethod.IsGenericMethod || (invocation.Arguments.Length > 0 && invocation.Arguments.GetArgumentForParameterAtIndex(0).Value is ITypeOfOperation { TypeOperand.IsUnmanagedType: true }),
                        "StructureToPtr" => invocation.Arguments.Length > 0 && invocation.Arguments.GetArgumentForParameterAtIndex(0).Value is { Type.IsUnmanagedType: true },
                        "PtrToStructure" => invocation.Type is not null,
                        _ => false
                    };
                }
            }

            public void AnalyzeFunctionPointerCall(OperationAnalysisContext context)
            {
                var functionPointerInvocation = IFunctionPointerInvocationOperationWrapper.FromOperation(context.Operation);

                if (functionPointerInvocation.GetFunctionPointerSignature().CallingConvention == System.Reflection.Metadata.SignatureCallingConvention.Default)
                {
                    return;
                }

                AnalyzeMethodSignature(_autoLayoutCache, context.ReportDiagnostic, functionPointerInvocation.GetFunctionPointerSignature(), ImmutableArray.Create(functionPointerInvocation.WrappedOperation.Syntax.GetLocation()));
            }

            public void AnalyzeLocalFunction(OperationAnalysisContext context)
            {
                var functionPointerInvocation = (ILocalFunctionOperation)context.Operation;
                AnalyzeMethod(context.ReportDiagnostic, functionPointerInvocation.Symbol);
            }

            public void AnalyzeType(SymbolAnalysisContext context)
            {
                Debug.Assert(_unmanagedFunctionPointerAttribute is not null);
                INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;
                if (type.TypeKind != TypeKind.Delegate || !type.HasAnyAttribute(_unmanagedFunctionPointerAttribute))
                {
                    return;
                }

                AnalyzeMethodSignature(_autoLayoutCache, context.ReportDiagnostic, type.DelegateInvokeMethod!);
            }

            public void AnalyzeMethod(SymbolAnalysisContext context)
            {
                IMethodSymbol method = (IMethodSymbol)context.Symbol;

                AnalyzeMethod(context.ReportDiagnostic, method);
            }

            private void AnalyzeMethod(Action<Diagnostic> reportDiagnostic, IMethodSymbol method)
            {
                // DisableRuntimeMarshalling only applies to DllImport-attributed methods.
                DllImportData? dllImportData = method.GetDllImportData();
                if (dllImportData is null)
                {
                    return;
                }

                if (dllImportData.SetLastError)
                {
                    reportDiagnostic(method.CreateDiagnostic(FeatureUnsupportedWhenRuntimeMarshallingDisabledSetLastErrorTrue));
                }

                if (!method.MethodImplementationFlags.HasFlag(System.Reflection.MethodImplAttributes.PreserveSig))
                {
                    reportDiagnostic(method.CreateDiagnostic(FeatureUnsupportedWhenRuntimeMarshallingDisabledHResultSwapping));
                }

                if (method.HasAnyAttribute(_lcidConversionAttribute))
                {
                    reportDiagnostic(method.CreateDiagnostic(FeatureUnsupportedWhenRuntimeMarshallingDisabledUsingLCIDConversionAttribute));
                }

                if (method.IsVararg)
                {
                    reportDiagnostic(method.CreateDiagnostic(FeatureUnsupportedWhenRuntimeMarshallingDisabledVarargPInvokes));
                }

                AnalyzeMethodSignature(_autoLayoutCache, reportDiagnostic, method);
            }
        }
    }
}
