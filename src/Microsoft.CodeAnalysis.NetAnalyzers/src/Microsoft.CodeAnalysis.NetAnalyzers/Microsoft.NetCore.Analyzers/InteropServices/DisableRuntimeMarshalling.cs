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

        private static readonly DiagnosticDescriptor FeatureUnsupportedWhenRuntimeMarshallingDisabledDelegateUsage =
            DiagnosticDescriptorHelper.Create(
                FeatureUnsupportedWhenRuntimeMarshallingDisabledId,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledTitle)),
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledMessageDelegateUsage)),
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
            // Report diagnostics in generated code to enable this analyzer to catch usages of
            // delegates with non-blittable parameters that do not have [UnmanagedFunctionPointer] in source-generated interop.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterCompilationStartAction(context =>
            {
                if (context.Compilation.TryGetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.SystemRuntimeCompilerServicesDisableRuntimeMarshallingAttribute,
                    out INamedTypeSymbol? disableRuntimeMarshallingAttribute))
                {
                    AutoLayoutTypeCache autoLayoutCache = new(context.Compilation);
                    var hasDisableRuntimeMarshallingAttribute = context.Compilation.Assembly.HasAttribute(disableRuntimeMarshallingAttribute);
                    if (hasDisableRuntimeMarshallingAttribute)
                    {
                        var disabledRuntimeMarshallingAssemblyAnalyzer = new DisabledRuntimeMarshallingAssemblyAnalyzer(context.Compilation, autoLayoutCache);
                        disabledRuntimeMarshallingAssemblyAnalyzer.RegisterActions(context);
                    }
                    var delegateInteropUsageAnalyzer = new DelegateInteropUsageAnalyzer(context.Compilation, autoLayoutCache);
                    delegateInteropUsageAnalyzer.RegisterActions(context, hasDisableRuntimeMarshallingAttribute);
                }
            });
        }

        private class AutoLayoutTypeCache
        {
            private readonly INamedTypeSymbol? _structLayoutAttribute;
            private readonly ConcurrentDictionary<ITypeSymbol, bool> _cache = new();

            public AutoLayoutTypeCache(Compilation compilation)
            {
                _structLayoutAttribute = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesStructLayoutAttribute);
            }


            public bool TypeIsAutoLayoutOrContainsAutoLayout(ITypeSymbol type)
            {
                return TypeIsAutoLayoutOrContainsAutoLayout(type, ImmutableHashSet<ITypeSymbol>.Empty.WithComparer(SymbolEqualityComparer.Default));
                bool TypeIsAutoLayoutOrContainsAutoLayout(ITypeSymbol type, ImmutableHashSet<ITypeSymbol> seenTypes)
                {
                    Debug.Assert(type.IsValueType);

                    if (_cache.TryGetValue(type, out bool isAutoLayoutOrContainsAutoLayout))
                    {
                        return isAutoLayoutOrContainsAutoLayout;
                    }

                    if (seenTypes.Contains(type.OriginalDefinition))
                    {
                        // If we have a recursive type, we are in one of two scenarios.
                        // 1. We're analyzing CoreLib and see the struct definition of a primitive type.
                        // In all of these cases, the type does not have auto layout.
                        // 2. We found a recursive type definition.
                        // Recursive type definitions are invalid and Roslyn will emit another error diagnostic anyway,
                        // so we don't care here.
                        _cache.TryAdd(type, false);
                        return false;
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
                                    _cache.TryAdd(type, true);
                                    return true;
                                }
                            }
                        }
                    }

                    var seenTypesWithCurrentType = seenTypes.Add(type.OriginalDefinition);

                    foreach (var member in type.GetMembers())
                    {
                        if (member is IFieldSymbol { IsStatic: false, Type.IsValueType: true } valueTypeField
                            && TypeIsAutoLayoutOrContainsAutoLayout(valueTypeField.Type, seenTypesWithCurrentType))
                        {
                            _cache.TryAdd(type, true);
                            return true;
                        }
                    }

                    _cache.TryAdd(type, false);
                    return false;
                }
            }
        }

        private class DelegateInteropUsageAnalyzer
        {
            private readonly IMethodSymbol? _getDelegateForFunctionPointerNonGeneric;
            private readonly IMethodSymbol? _getDelegateForFunctionPointerGeneric;
            private readonly IMethodSymbol? _getFunctionPointerForDelegateNonGeneric;
            private readonly IMethodSymbol? _getFunctionPointerForDelegateGeneric;
            private readonly AutoLayoutTypeCache _autoLayoutCache;

            public DelegateInteropUsageAnalyzer(Compilation compilation, AutoLayoutTypeCache autoLayoutCache)
            {
                if (compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesMarshal, out var marshalType))
                {
                    var getDelegateForFunctionPointerMethods = marshalType.GetMembers("GetDelegateForFunctionPointer").OfType<IMethodSymbol>();
                    _getDelegateForFunctionPointerNonGeneric = getDelegateForFunctionPointerMethods.First(m => m.TypeArguments.Length == 0);
                    _getDelegateForFunctionPointerGeneric = getDelegateForFunctionPointerMethods.First(m => m.TypeArguments.Length != 0);
                    var getFunctionPointerForDelegateMethods = marshalType.GetMembers("GetFunctionPointerForDelegate").OfType<IMethodSymbol>();
                    _getFunctionPointerForDelegateNonGeneric = getFunctionPointerForDelegateMethods.First(m => m.TypeArguments.Length == 0);
                    _getFunctionPointerForDelegateGeneric = getFunctionPointerForDelegateMethods.First(m => m.TypeArguments.Length != 0);
                }

                _autoLayoutCache = autoLayoutCache;
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
                    if (operation.Arguments.Length == 2 && operation.Arguments[1] is { Value: ITypeOfOperation { TypeOperand: INamedTypeSymbol { TypeKind: TypeKind.Delegate } delegateType } })
                    {
                        AnalyzeDelegateType(context, operation.Arguments[1], delegateType);
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
                    AnalyzeMethodSignature(_autoLayoutCache, context.ReportDiagnostic, delegateType.DelegateInvokeMethod, ImmutableArray.Create(operation.Syntax.GetLocation()), FeatureUnsupportedWhenRuntimeMarshallingDisabledDelegateUsage);
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
                    AnalyzeMethodSignature(_autoLayoutCache, reportDiagnostic, delegateType.DelegateInvokeMethod, signatureSymbol.Locations, FeatureUnsupportedWhenRuntimeMarshallingDisabledDelegateUsage);
                }
            }
        }

        private class DisabledRuntimeMarshallingAssemblyAnalyzer
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
                if (type.TypeKind != TypeKind.Delegate || !type.HasAttribute(_unmanagedFunctionPointerAttribute))
                {
                    return;
                }

                AnalyzeMethodSignature(_autoLayoutCache, context.ReportDiagnostic, type.DelegateInvokeMethod);
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

                if (!method.MethodImplementationFlags().HasFlag(System.Reflection.MethodImplAttributes.PreserveSig))
                {
                    reportDiagnostic(method.CreateDiagnostic(FeatureUnsupportedWhenRuntimeMarshallingDisabledHResultSwapping));
                }

                if (method.HasAttribute(_lcidConversionAttribute))
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

        private static void AnalyzeMethodSignature(AutoLayoutTypeCache autoLayoutCache, Action<Diagnostic> reportDiagnostic, IMethodSymbol method, ImmutableArray<Location> locationsOverride = default, DiagnosticDescriptor? descriptorOverride = null)
        {
            AnalyzeSignatureType(locationsOverride.IsDefaultOrEmpty ? method.Locations : locationsOverride, method.ReturnType);
            foreach (var param in method.Parameters)
            {
                var paramLocation = locationsOverride.IsDefaultOrEmpty ? param.Locations : locationsOverride;
                if (param.RefKind != RefKind.None)
                {
                    reportDiagnostic(paramLocation.CreateDiagnostic(descriptorOverride ?? FeatureUnsupportedWhenRuntimeMarshallingDisabledByRefParameters));
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
                        reportDiagnostic(locations.CreateDiagnostic(descriptorOverride ?? FeatureUnsupportedWhenRuntimeMarshallingDisabledManagedParameterOrReturnTypes));
                    }
                }
                // For non-C# languages, we'll do a quick check to catch simple cases
                // since IsUnmanagedType only works in languages that support unmanaged types
                // and non-C# languages that might not support is (such as VB) aren't a big focus of the attribute
                // this analyzer validates.
                else if (type.IsReferenceType || type.GetMembers().Any(m => m is IFieldSymbol { IsStatic: false, Type.IsReferenceType: true }))
                {
                    reportDiagnostic(locations.CreateDiagnostic(descriptorOverride ?? FeatureUnsupportedWhenRuntimeMarshallingDisabledManagedParameterOrReturnTypes));
                }

                if (type.IsValueType && autoLayoutCache.TypeIsAutoLayoutOrContainsAutoLayout(type))
                {
                    reportDiagnostic(locations.CreateDiagnostic(descriptorOverride ?? FeatureUnsupportedWhenRuntimeMarshallingDisabledAutoLayoutTypes));
                }
            }
        }
    }
}
