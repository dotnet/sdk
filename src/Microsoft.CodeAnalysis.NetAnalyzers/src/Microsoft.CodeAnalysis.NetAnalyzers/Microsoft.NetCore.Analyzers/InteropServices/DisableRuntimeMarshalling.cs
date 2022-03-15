// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.Lightup;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    using static MicrosoftNetCoreAnalyzersResources;

    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    internal sealed class DisableRuntimeMarshallingAnalyzer : DiagnosticAnalyzer
    {
        internal const string FeatureUnsupportedWhenRuntimeMarshallingDisabledId = "CA1420";

        private static readonly DiagnosticDescriptor FeatureUnsupportedWhenRuntimeMarshallingDisabledSetLastErrorTrue =
            DiagnosticDescriptorHelper.Create(
                FeatureUnsupportedWhenRuntimeMarshallingDisabledId,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledTitle)),
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledMessageSetLastError)),
                DiagnosticCategory.Interoperability,
                RuleLevel.BuildWarning,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        private static readonly DiagnosticDescriptor FeatureUnsupportedWhenRuntimeMarshallingDisabledHResultSwapping =
            DiagnosticDescriptorHelper.Create(
                FeatureUnsupportedWhenRuntimeMarshallingDisabledId,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledTitle)),
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledMessageHResultSwapping)),
                DiagnosticCategory.Interoperability,
                RuleLevel.BuildWarning,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        private static readonly DiagnosticDescriptor FeatureUnsupportedWhenRuntimeMarshallingDisabledUsingLCIDConversionAttribute =
            DiagnosticDescriptorHelper.Create(
                FeatureUnsupportedWhenRuntimeMarshallingDisabledId,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledTitle)),
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledMessageLCIDConversionAttribute)),
                DiagnosticCategory.Interoperability,
                RuleLevel.BuildWarning,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        private static readonly DiagnosticDescriptor FeatureUnsupportedWhenRuntimeMarshallingDisabledVarargPInvokes =
            DiagnosticDescriptorHelper.Create(
                FeatureUnsupportedWhenRuntimeMarshallingDisabledId,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledTitle)),
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledMessageVarargPInvokes)),
                DiagnosticCategory.Interoperability,
                RuleLevel.BuildWarning,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        private static readonly DiagnosticDescriptor FeatureUnsupportedWhenRuntimeMarshallingDisabledByRefParameters =
            DiagnosticDescriptorHelper.Create(
                FeatureUnsupportedWhenRuntimeMarshallingDisabledId,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledTitle)),
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledMessageByRefParameters)),
                DiagnosticCategory.Interoperability,
                RuleLevel.BuildWarning,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        private static readonly DiagnosticDescriptor FeatureUnsupportedWhenRuntimeMarshallingDisabledManagedParameterOrReturnTypes =
            DiagnosticDescriptorHelper.Create(
                FeatureUnsupportedWhenRuntimeMarshallingDisabledId,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledTitle)),
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledMessageManagedParameterOrReturnTypes)),
                DiagnosticCategory.Interoperability,
                RuleLevel.BuildWarning,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        private static readonly DiagnosticDescriptor FeatureUnsupportedWhenRuntimeMarshallingDisabledAutoLayoutTypes =
            DiagnosticDescriptorHelper.Create(
                FeatureUnsupportedWhenRuntimeMarshallingDisabledId,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledTitle)),
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledMessageAutoLayoutTypes)),
                DiagnosticCategory.Interoperability,
                RuleLevel.BuildWarning,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        internal const string MethodUsesRuntimeMarshallingEvenWhenMarshallingDisabledId = "CA1421";

        private static readonly DiagnosticDescriptor MethodUsesRuntimeMarshallingEvenWhenMarshallingDisabled =
            DiagnosticDescriptorHelper.Create(
                MethodUsesRuntimeMarshallingEvenWhenMarshallingDisabledId,
                CreateLocalizableResourceString(nameof(MethodUsesRuntimeMarshallingEvenWhenMarshallingDisabledTitle)),
                CreateLocalizableResourceString(nameof(MethodUsesRuntimeMarshallingEvenWhenMarshallingDisabledMessage)),
                DiagnosticCategory.Interoperability,
                RuleLevel.IdeSuggestion,
                CreateLocalizableResourceString(nameof(MethodUsesRuntimeMarshallingEvenWhenMarshallingDisabledDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        public const string CanConvertToDisabledMarshallingEquivalentKey = nameof(CanConvertToDisabledMarshallingEquivalentKey);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            FeatureUnsupportedWhenRuntimeMarshallingDisabledSetLastErrorTrue,
            FeatureUnsupportedWhenRuntimeMarshallingDisabledHResultSwapping,
            FeatureUnsupportedWhenRuntimeMarshallingDisabledUsingLCIDConversionAttribute,
            FeatureUnsupportedWhenRuntimeMarshallingDisabledVarargPInvokes,
            FeatureUnsupportedWhenRuntimeMarshallingDisabledByRefParameters,
            FeatureUnsupportedWhenRuntimeMarshallingDisabledManagedParameterOrReturnTypes,
            FeatureUnsupportedWhenRuntimeMarshallingDisabledAutoLayoutTypes,
            MethodUsesRuntimeMarshallingEvenWhenMarshallingDisabled);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(context =>
            {
                if (context.Compilation.TryGetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.SystemRuntimeCompilerServicesDisableRuntimeMarshallingAttribute,
                    out INamedTypeSymbol? disableRuntimeMarshallingAttribute)
                    && context.Compilation.Assembly.HasAttribute(disableRuntimeMarshallingAttribute))
                {
                    var perCompilationAnalyzer = new PerCompilationAnalyzer(context.Compilation);
                    perCompilationAnalyzer.RegisterActions(context);
                }
            });
        }

        private class PerCompilationAnalyzer
        {
            private readonly INamedTypeSymbol? _unmanagedFunctionPointerAttribute;
            private readonly INamedTypeSymbol? _structLayoutAttribute;
            private readonly INamedTypeSymbol? _lcidConversionAttribute;
            private readonly ImmutableArray<ISymbol> _marshalMethods;
            private readonly ConcurrentDictionary<ITypeSymbol, bool> _isAutoLayoutOrContainsAutoLayoutCache = new();

            public PerCompilationAnalyzer(Compilation compilation)
            {
                _unmanagedFunctionPointerAttribute = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesUnmanagedFunctionPoitnerAttribute);
                _structLayoutAttribute = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesStructLayoutAttribute);
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
            }

            public void RegisterActions(CompilationStartAnalysisContext context)
            {
                context.RegisterSymbolAction(AnalyzeMethod, SymbolKind.Method);

                context.RegisterOperationAction(AnalyzeMethodCall, OperationKind.Invocation);

                context.RegisterOperationAction(AnalyzeFunctionPointerCall, OperationKindEx.FunctionPointerInvocation);

                if (_unmanagedFunctionPointerAttribute is not null)
                {
                    context.RegisterSymbolAction(AnalyzeType, SymbolKind.NamedType);
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
                        "SizeOf" => invocation.TargetMethod.IsGenericMethod || (invocation.Arguments.Length > 0 && invocation.Arguments[0].Value is ITypeOfOperation { TypeOperand.IsUnmanagedType: true }),
                        "StructureToPtr" => invocation.Arguments.Length > 0 && invocation.Arguments[0].Value is { Type.IsUnmanagedType: true },
                        "PtrToStructure" => invocation.Type is not null,
                        _ => false
                    };
                }
            }

            public void AnalyzeFunctionPointerCall(OperationAnalysisContext context)
            {
                var functionPointerInvocation = IFunctionPointerInvocationOperationWrapper.FromOperation(context.Operation);

                if (functionPointerInvocation.GetFunctionPointerSignature().CallingConvention() == System.Reflection.Metadata.SignatureCallingConvention.Default)
                {
                    return;
                }

                AnalyzeMethodSignature(context.ReportDiagnostic, functionPointerInvocation.GetFunctionPointerSignature(), ImmutableArray.Create(functionPointerInvocation.WrappedOperation.Syntax.GetLocation()));
            }

            public void AnalyzeType(SymbolAnalysisContext context)
            {
                Debug.Assert(_unmanagedFunctionPointerAttribute is not null);
                INamedTypeSymbol type = (INamedTypeSymbol)context.Symbol;
                if (type.TypeKind != TypeKind.Delegate || !type.HasAttribute(_unmanagedFunctionPointerAttribute))
                {
                    return;
                }

                AnalyzeMethodSignature(context.ReportDiagnostic, type.DelegateInvokeMethod);
            }

            public void AnalyzeMethod(SymbolAnalysisContext context)
            {
                IMethodSymbol method = (IMethodSymbol)context.Symbol;

                DllImportData? dllImportData = method.GetDllImportData();
                if (dllImportData is null)
                {
                    return;
                }

                if (dllImportData.SetLastError)
                {
                    context.ReportDiagnostic(method.CreateDiagnostic(FeatureUnsupportedWhenRuntimeMarshallingDisabledSetLastErrorTrue));
                }

                if (!method.MethodImplementationFlags().HasFlag(System.Reflection.MethodImplAttributes.PreserveSig))
                {
                    context.ReportDiagnostic(method.CreateDiagnostic(FeatureUnsupportedWhenRuntimeMarshallingDisabledHResultSwapping));
                }

                if (_lcidConversionAttribute is not null && method.HasAttribute(_lcidConversionAttribute))
                {
                    context.ReportDiagnostic(method.CreateDiagnostic(FeatureUnsupportedWhenRuntimeMarshallingDisabledUsingLCIDConversionAttribute));
                }

                if (method.IsVararg)
                {
                    context.ReportDiagnostic(method.CreateDiagnostic(FeatureUnsupportedWhenRuntimeMarshallingDisabledVarargPInvokes));
                }

                AnalyzeMethodSignature(context.ReportDiagnostic, method);
            }

            private void AnalyzeMethodSignature(Action<Diagnostic> reportDiagnostic, IMethodSymbol method, ImmutableArray<Location> locationsOverride = default)
            {
                AnalyzeSignatureType(locationsOverride.IsDefaultOrEmpty ? method.Locations : locationsOverride, method.ReturnType);
                foreach (var param in method.Parameters)
                {
                    var paramLocation = locationsOverride.IsDefaultOrEmpty ? param.Locations : locationsOverride;
                    if (param.RefKind != RefKind.None)
                    {
                        reportDiagnostic(paramLocation.CreateDiagnostic(FeatureUnsupportedWhenRuntimeMarshallingDisabledByRefParameters));
                    }
                    AnalyzeSignatureType(paramLocation, param.Type);
                }

                void AnalyzeSignatureType(ImmutableArray<Location> locations, ITypeSymbol type)
                {
                    if (type.SpecialType == SpecialType.System_Void)
                    {
                        return;
                    }

                    if (type.Language == LanguageNames.CSharp)
                    {
                        if (!type.IsUnmanagedType)
                        {
                            reportDiagnostic(locations.CreateDiagnostic(FeatureUnsupportedWhenRuntimeMarshallingDisabledManagedParameterOrReturnTypes));
                        }
                    }
                    // For non-C# languages, we'll do a quick check to catch simple cases
                    // since IsUnmanagedType only works in languages that support unmanaged types
                    // and non-C# languages that might not support is (such as VB) aren't a big focus of the attribute
                    // this analyzer validates.
                    else if (type.IsReferenceType || type.GetMembers().Any(m => m is IFieldSymbol { IsStatic: false, Type.IsReferenceType: true }))
                    {
                        reportDiagnostic(locations.CreateDiagnostic(FeatureUnsupportedWhenRuntimeMarshallingDisabledManagedParameterOrReturnTypes));
                    }

                    if (type.IsValueType && TypeIsAutoLayoutOrContainsAutoLayout(type))
                    {
                        reportDiagnostic(locations.CreateDiagnostic(FeatureUnsupportedWhenRuntimeMarshallingDisabledAutoLayoutTypes));
                    }
                }
            }

            private bool TypeIsAutoLayoutOrContainsAutoLayout(ITypeSymbol type)
            {
                Debug.Assert(type.IsValueType);

                if (_isAutoLayoutOrContainsAutoLayoutCache.TryGetValue(type, out bool isAutoLayoutOrContainsAutoLayout))
                {
                    return isAutoLayoutOrContainsAutoLayout;
                }

                if (_structLayoutAttribute is not null)
                {
                    foreach (var attr in type.GetAttributes(_structLayoutAttribute))
                    {
                        if (attr.ConstructorArguments.Length > 0
                            && attr.ConstructorArguments[0] is TypedConstant argument
                            && argument.Type is not null)
                        {
                            SpecialType specialType = argument.Type.TypeKind == TypeKind.Enum ?
                                ((INamedTypeSymbol)argument.Type).EnumUnderlyingType.SpecialType :
                                argument.Type.SpecialType;

                            if (DiagnosticHelpers.TryConvertToUInt64(argument.Value, specialType, out ulong convertedLayoutKindValue) &&
                                convertedLayoutKindValue == (ulong)LayoutKind.Auto)
                            {
                                _isAutoLayoutOrContainsAutoLayoutCache.TryAdd(type, true);
                                return true;
                            }
                        }
                    }
                }

                foreach (var member in type.GetMembers())
                {
                    if (member is IFieldSymbol { IsStatic: false, Type.IsValueType: true } valueTypeField
                        && TypeIsAutoLayoutOrContainsAutoLayout(valueTypeField.Type))
                    {
                        _isAutoLayoutOrContainsAutoLayoutCache.TryAdd(type, true);
                        return true;
                    }
                }

                _isAutoLayoutOrContainsAutoLayoutCache.TryAdd(type, false);
                return false;
            }
        }
    }
}
