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

        // [ThreadStatic], [ContextStatic] or [FixedAddressValueType]
        // private object t_nonStaticField;
        // [SuppressGCTransition] on a method that is not a P/Invoke
        internal static readonly DiagnosticDescriptor AttributeHasNoEffectRule = DiagnosticDescriptorHelper.Create(ThreadStaticNonStaticFieldRuleId,
            CreateLocalizableResourceString(nameof(AttributeHasNoEffectTitle)),
            CreateLocalizableResourceString(nameof(AttributeHasNoEffectMessage)),
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

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(AttributeHasNoEffectRule, ThreadStaticInitializedInlineRule);

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

                // Warn on any [ThreadStatic], [ContextStatic] or [FixedAddressValueType] instance field.
                context.RegisterSymbolAction(context =>
                {
                    ISymbol symbol = context.Symbol;

                    // If it's a static symbol, nothing to see here.
                    if (symbol.IsStatic)
                    {
                        return;
                    }

                    // If it's an auto-prop, find its backing field if there is one.
                    // If it's a field, it's the symbol we'll check.
                    if (!symbol.IsPropertyWithBackingField(out IFieldSymbol? fieldSymbol))
                    {
                        fieldSymbol = symbol as IFieldSymbol;
                    }

                    // Once we have the field, see if it's attributed with [ThreadStatic].
                    if (fieldSymbol?.HasAnyAttribute(threadStaticAttributeType) == true)
                    {
                        context.ReportDiagnostic(symbol.CreateDiagnostic(AttributeHasNoEffectRule, "ThreadStatic", symbol.Name));
                    }

                    if (fieldSymbol?.HasAnyAttribute(contextStaticAttributeType) == true)
                    {
                        context.ReportDiagnostic(symbol.CreateDiagnostic(AttributeHasNoEffectRule, "ContextStatic", symbol.Name));
                    }

                    if (fieldSymbol?.HasAnyAttribute(fixedAddressValueTypeAttributeType) == true)
                    {
                        context.ReportDiagnostic(symbol.CreateDiagnostic(AttributeHasNoEffectRule, "FixedAddressValueType", symbol.Name));
                    }
                }, SymbolKind.Field, SymbolKind.Property);

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
                        if (method.HasAnyAttribute(suppressGCTransitionAttributeType) &&
                            method.GetDllImportData() is null &&
                            !method.HasAnyAttribute(libraryImportAttributeType) &&
                            !(method is { IsStatic: false, IsAbstract: true } && method.ContainingType.HasAnyAttribute(generatedComInterfaceAttributeType)))
                        {
                            reportDiagnostic(method.CreateDiagnostic(AttributeHasNoEffectRule, "SuppressGCTransition", method.MethodKind == MethodKind.AnonymousFunction ? method.ToDisplayString() : method.Name));
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