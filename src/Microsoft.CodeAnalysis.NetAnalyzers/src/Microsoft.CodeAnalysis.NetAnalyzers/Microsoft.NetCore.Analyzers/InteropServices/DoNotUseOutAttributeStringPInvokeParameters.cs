// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1417: <inheritdoc cref="DoNotUseOutAttributeStringPInvokeParametersTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseOutAttributeStringPInvokeParametersAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1417";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotUseOutAttributeStringPInvokeParametersTitle)),
            CreateLocalizableResourceString(nameof(DoNotUseOutAttributeStringPInvokeParametersMessage)),
            DiagnosticCategory.Interoperability,
            RuleLevel.BuildWarning,
            description: CreateLocalizableResourceString(nameof(DoNotUseOutAttributeStringPInvokeParametersDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(
                compilationContext =>
                {
                    if (compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesOutAttribute, out var outAttributeType))
                    {
                        compilationContext.RegisterSymbolAction(symbolContext => AnalyzeSymbol(symbolContext, outAttributeType), SymbolKind.Method);
                    }
                });
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol outAttributeType)
        {
            var methodSymbol = (IMethodSymbol)context.Symbol;

            // Only check P/Invokes
            DllImportData? dllImportData = methodSymbol.GetDllImportData();
            if (dllImportData == null)
            {
                return;
            }

            // Warn on string parameters passed by value with [Out] attribute
            foreach (IParameterSymbol parameter in methodSymbol.Parameters)
            {
                if (parameter.Type.SpecialType == SpecialType.System_String
                    && parameter.RefKind == RefKind.None
                    && parameter.HasAnyAttribute(outAttributeType))
                {
                    context.ReportDiagnostic(parameter.CreateDiagnostic(Rule, parameter.Name));
                }
            }
        }
    }
}
