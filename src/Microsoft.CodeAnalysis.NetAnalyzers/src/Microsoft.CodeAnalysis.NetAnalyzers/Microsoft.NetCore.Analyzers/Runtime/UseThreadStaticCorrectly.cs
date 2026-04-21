// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
    /// CA2259: <inheritdoc cref="ThreadStaticOnNonStaticFieldTitle"/>
    /// CA2019: <inheritdoc cref="ThreadStaticInitializedInlineTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseThreadStaticCorrectly : DiagnosticAnalyzer
    {
        internal const string ThreadStaticNonStaticFieldRuleId = "CA2259";
        internal const string ThreadStaticInitializedInlineRuleId = "CA2019";

        // [ThreadStatic]
        // private object t_nonStaticField;
        internal static readonly DiagnosticDescriptor ThreadStaticOnNonStaticFieldRule = DiagnosticDescriptorHelper.Create(ThreadStaticNonStaticFieldRuleId,
            CreateLocalizableResourceString(nameof(ThreadStaticOnNonStaticFieldTitle)),
            CreateLocalizableResourceString(nameof(ThreadStaticOnNonStaticFieldMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.BuildWarning,
            CreateLocalizableResourceString(nameof(ThreadStaticOnNonStaticFieldDescription)),
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

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(ThreadStaticOnNonStaticFieldRule, ThreadStaticInitializedInlineRule);

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

                // Warn on any [ThreadStatic] instance field.
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
                        context.ReportDiagnostic(symbol.CreateDiagnostic(ThreadStaticOnNonStaticFieldRule));
                    }
                }, SymbolKind.Field, SymbolKind.Property);

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