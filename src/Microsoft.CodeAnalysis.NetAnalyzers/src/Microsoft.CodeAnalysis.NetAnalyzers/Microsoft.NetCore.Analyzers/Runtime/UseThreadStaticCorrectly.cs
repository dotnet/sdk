// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2259: <inheritdoc cref="AttributeHasNoEffectTitle"/>
    /// CA2019: <inheritdoc cref="ThreadStaticInitializedInlineTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseThreadStaticCorrectly : DiagnosticAnalyzer
    {
        internal const string ThreadStaticNonStaticFieldRuleId = "CA2259";
        internal const string ThreadStaticInitializedInlineRuleId = "CA2019";

        // CA2259 has one message per reason the attribute has no effect.

        // [ThreadStatic], [ContextStatic] or [FixedAddressValueType]
        // private object t_nonStaticField;
        internal static readonly DiagnosticDescriptor NonStaticFieldRule = DiagnosticDescriptorHelper.Create(ThreadStaticNonStaticFieldRuleId,
            CreateLocalizableResourceString(nameof(AttributeHasNoEffectTitle)),
            CreateLocalizableResourceString(nameof(AttributeHasNoEffectNonStaticFieldMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.BuildWarning,
            CreateLocalizableResourceString(nameof(AttributeHasNoEffectDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        // [FixedAddressValueType]
        // private static int s_field;
        internal static readonly DiagnosticDescriptor UnsupportedTypeFieldRule = DiagnosticDescriptorHelper.Create(ThreadStaticNonStaticFieldRuleId,
            CreateLocalizableResourceString(nameof(AttributeHasNoEffectTitle)),
            CreateLocalizableResourceString(nameof(AttributeHasNoEffectUnsupportedTypeFieldMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.BuildWarning,
            CreateLocalizableResourceString(nameof(AttributeHasNoEffectDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        // [SuppressGCTransition] on a method that is not a P/Invoke
        internal static readonly DiagnosticDescriptor NotPInvokeRule = DiagnosticDescriptorHelper.Create(ThreadStaticNonStaticFieldRuleId,
            CreateLocalizableResourceString(nameof(AttributeHasNoEffectTitle)),
            CreateLocalizableResourceString(nameof(AttributeHasNoEffectNotPInvokeMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.BuildWarning,
            CreateLocalizableResourceString(nameof(AttributeHasNoEffectDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        // [ThreadStatic]
        // private static object t_field = new object();
        internal static readonly DiagnosticDescriptor ThreadStaticInitializedInlineRule = DiagnosticDescriptorHelper.Create(ThreadStaticInitializedInlineRuleId,
            CreateLocalizableResourceString(nameof(ThreadStaticInitializedInlineTitle)),
            CreateLocalizableResourceString(nameof(ThreadStaticInitializedInlineMessage)),
            DiagnosticCategory.Reliability,
            RuleLevel.IdeSuggestion,
            CreateLocalizableResourceString(nameof(ThreadStaticInitializedInlineDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(NonStaticFieldRule, UnsupportedTypeFieldRule, NotPInvokeRule, ThreadStaticInitializedInlineRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(context =>
            {
                // Ensure ThreadStatic exists
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemThreadStaticAttribute, out var threadStaticAttributeType))
                {
                    return;
                }

                var contextStaticAttributeType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemContextStaticAttribute);
                var fixedAddressValueTypeAttributeType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesFixedAddressValueTypeAttribute);
                var enumType = context.Compilation.GetSpecialType(SpecialType.System_Enum);

                // Warn on any [ThreadStatic], [ContextStatic] or [FixedAddressValueType] instance field,
                // and on [FixedAddressValueType] on a static field that is not of a struct type, or is of a primitive type.
                context.RegisterSymbolAction(context =>
                {
                    ISymbol symbol = context.Symbol;

                    // If it's an auto-prop, find its backing field if there is one.
                    // If it's a field, it's the symbol we'll check.
                    if (!symbol.IsPropertyWithBackingField(out IFieldSymbol? fieldSymbol))
                    {
                        fieldSymbol = symbol as IFieldSymbol;
                    }

                    if (fieldSymbol is null)
                    {
                        return;
                    }

                    if (!symbol.IsStatic)
                    {
                        if (fieldSymbol.HasAnyAttribute(threadStaticAttributeType))
                        {
                            context.ReportDiagnostic(symbol.CreateDiagnostic(NonStaticFieldRule, "ThreadStatic", symbol.Name));
                        }

                        if (fieldSymbol.HasAnyAttribute(contextStaticAttributeType))
                        {
                            context.ReportDiagnostic(symbol.CreateDiagnostic(NonStaticFieldRule, "ContextStatic", symbol.Name));
                        }

                        if (fieldSymbol.HasAnyAttribute(fixedAddressValueTypeAttributeType))
                        {
                            context.ReportDiagnostic(symbol.CreateDiagnostic(NonStaticFieldRule, "FixedAddressValueType", symbol.Name));
                        }
                    }
                    else if (fieldSymbol.HasAnyAttribute(fixedAddressValueTypeAttributeType) &&
                        (fieldSymbol.Type.IsReferenceType || fieldSymbol.Type.IsPrimitiveType() || fieldSymbol.Type.SpecialType is SpecialType.System_IntPtr or SpecialType.System_UIntPtr || fieldSymbol.Type.TypeKind is TypeKind.Enum or TypeKind.Pointer or TypeKind.FunctionPointer ||
                        (fieldSymbol.Type.TypeKind == TypeKind.TypeParameter && fieldSymbol.Type.DerivesFrom(enumType))))
                    {
                        // A type parameter only counts when its constraints make it a reference type or an enum, since it may be a supported struct otherwise.
                        context.ReportDiagnostic(symbol.CreateDiagnostic(UnsupportedTypeFieldRule, "FixedAddressValueType", symbol.Name));
                    }
                }, SymbolKind.Field, SymbolKind.Property);

                // The backing field of a field-like event is not exposed as a symbol, so its [field: ...] attributes
                // are found as attribute operations. The field has a delegate type, which is a reference type.
                context.RegisterOperationAction(context =>
                {
                    ISymbol symbol = context.ContainingSymbol;
                    ITypeSymbol? attributeType = ((IAttributeOperation)context.Operation).Operation.Type;

                    // Without a field: target the attribute belongs to the event itself, which is already a compiler error.
                    if (symbol is not IEventSymbol ||
                        symbol.GetAttributes().Any(a => a.ApplicationSyntaxReference?.Span == context.Operation.Syntax.Span && a.ApplicationSyntaxReference.SyntaxTree == context.Operation.Syntax.SyntaxTree))
                    {
                        return;
                    }

                    if (!symbol.IsStatic)
                    {
                        if (SymbolEqualityComparer.Default.Equals(attributeType, threadStaticAttributeType))
                        {
                            context.ReportDiagnostic(symbol.CreateDiagnostic(NonStaticFieldRule, "ThreadStatic", symbol.Name));
                        }

                        if (SymbolEqualityComparer.Default.Equals(attributeType, contextStaticAttributeType))
                        {
                            context.ReportDiagnostic(symbol.CreateDiagnostic(NonStaticFieldRule, "ContextStatic", symbol.Name));
                        }

                        if (SymbolEqualityComparer.Default.Equals(attributeType, fixedAddressValueTypeAttributeType))
                        {
                            context.ReportDiagnostic(symbol.CreateDiagnostic(NonStaticFieldRule, "FixedAddressValueType", symbol.Name));
                        }
                    }
                    else if (SymbolEqualityComparer.Default.Equals(attributeType, fixedAddressValueTypeAttributeType))
                    {
                        context.ReportDiagnostic(symbol.CreateDiagnostic(UnsupportedTypeFieldRule, "FixedAddressValueType", symbol.Name));
                    }
                }, OperationKind.Attribute);

                // Warn on [SuppressGCTransition] on a method that is not a P/Invoke. The interop source generators
                // also forward it from [LibraryImport] methods and the abstract instance methods of [GeneratedComInterface] interfaces.
                if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesSuppressGCTransitionAttribute, out var suppressGCTransitionAttributeType))
                {
                    var libraryImportAttributeType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesLibraryImportAttribute);
                    var generatedComInterfaceAttributeType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesMarshallingGeneratedComInterfaceAttribute);

                    context.RegisterSymbolAction(context => AnalyzeMethod((IMethodSymbol)context.Symbol, context.ReportDiagnostic), SymbolKind.Method);

                    // Local functions and lambdas are not visited by symbol actions.
                    context.RegisterOperationAction(context =>
                    {
                        var method = context.Operation switch
                        {
                            ILocalFunctionOperation localFunction => localFunction.Symbol,
                            IAnonymousFunctionOperation anonymousFunction => anonymousFunction.Symbol,
                            _ => null,
                        };

                        if (method is not null)
                        {
                            AnalyzeMethod(method, context.ReportDiagnostic);
                        }
                    }, OperationKind.LocalFunction, OperationKind.AnonymousFunction);

                    void AnalyzeMethod(IMethodSymbol method, Action<Diagnostic> reportDiagnostic)
                    {
                        // The two parts of a partial method share their attributes, so only the defining part is reported.
                        if (method.PartialDefinitionPart is not null)
                        {
                            return;
                        }

                        if (method.HasAnyAttribute(suppressGCTransitionAttributeType) &&
                            method.GetDllImportData() is null &&
                            !method.HasAnyAttribute(libraryImportAttributeType) &&
                            !(method is { IsStatic: false, IsAbstract: true } && method.ContainingType.HasAnyAttribute(generatedComInterfaceAttributeType)))
                        {
                            reportDiagnostic(method.CreateDiagnostic(NotPInvokeRule, "SuppressGCTransition", method.MethodKind == MethodKind.AnonymousFunction ? method.ToDisplayString() : method.Name));
                        }
                    }
                }

                // Warn on any [ThreadStatic] field inline initialization.
                context.RegisterOperationAction(context =>
                {
                    var fieldInit = (IFieldInitializerOperation)context.Operation;
                    foreach (IFieldSymbol field in fieldInit.InitializedFields)
                    {
                        if (field.IsStatic && field.HasAnyAttribute(threadStaticAttributeType))
                        {
                            context.ReportDiagnostic(fieldInit.CreateDiagnostic(ThreadStaticInitializedInlineRule));
                            break;
                        }
                    }
                }, OperationKind.FieldInitializer);
            });
        }
    }
}