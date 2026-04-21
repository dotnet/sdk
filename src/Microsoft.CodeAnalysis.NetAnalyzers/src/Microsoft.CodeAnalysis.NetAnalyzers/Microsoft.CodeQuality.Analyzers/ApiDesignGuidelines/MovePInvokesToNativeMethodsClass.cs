// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1060: <inheritdoc cref="MovePInvokesToNativeMethodsClassTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class MovePInvokesToNativeMethodsClassAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1060";
        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(MovePInvokesToNativeMethodsClassTitle)),
            CreateLocalizableResourceString(nameof(MovePInvokesToNativeMethodsClassMessage)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(MovePInvokesToNativeMethodsClassDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false,
            isEnabledByDefaultInAggressiveMode: false);

        private const string NativeMethodsText = "NativeMethods";
        private const string SafeNativeMethodsText = "SafeNativeMethods";
        private const string UnsafeNativeMethodsText = "UnsafeNativeMethods";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(symbolContext =>
            {
                AnalyzeSymbol(
                    (INamedTypeSymbol)symbolContext.Symbol,
                    static (context, diagnostic) => context.ReportDiagnostic(diagnostic),
                    symbolContext);
            }, SymbolKind.NamedType);
        }

        private static void AnalyzeSymbol<TContext>(INamedTypeSymbol symbol, Action<TContext, Diagnostic> addDiagnostic, TContext context)
        {
            if (symbol.GetMembers().Any(IsDllImport) && !IsTypeNamedCorrectly(symbol.Name))
            {
                addDiagnostic(context, symbol.CreateDiagnostic(Rule));
            }
        }

        private static bool IsDllImport(ISymbol symbol)
        {
            return symbol.Kind == SymbolKind.Method && ((IMethodSymbol)symbol).GetDllImportData() != null;
        }

        private static bool IsTypeNamedCorrectly(string name)
        {
            return string.Equals(name, NativeMethodsText, StringComparison.Ordinal) ||
                string.Equals(name, SafeNativeMethodsText, StringComparison.Ordinal) ||
                string.Equals(name, UnsafeNativeMethodsText, StringComparison.Ordinal);
        }
    }
}
