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
    /// CA1838: <inheritdoc cref="AvoidStringBuilderPInvokeParametersTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AvoidStringBuilderPInvokeParametersAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1838";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(AvoidStringBuilderPInvokeParametersTitle)),
            CreateLocalizableResourceString(nameof(AvoidStringBuilderPInvokeParametersMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeHidden_BulkConfigurable, // Only for users explicitly targeting performance - addressing violation is non-trivial
            description: CreateLocalizableResourceString(nameof(AvoidStringBuilderPInvokeParametersDescription)),
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
                    if (compilationContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemTextStringBuilder, out var stringBuilderType))
                    {
                        compilationContext.RegisterSymbolAction(symbolContext => AnalyzeSymbol(symbolContext, stringBuilderType), SymbolKind.Method);
                    }
                });
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol stringBuilderType)
        {
            var methodSymbol = (IMethodSymbol)context.Symbol;

            // Only check P/Invokes
            DllImportData? dllImportData = methodSymbol.GetDllImportData();
            if (dllImportData == null)
            {
                return;
            }

            // Warn on StringBuilder parameters
            foreach (IParameterSymbol parameter in methodSymbol.Parameters)
            {
                if (parameter.Type.Equals(stringBuilderType))
                {
                    context.ReportDiagnostic(parameter.CreateDiagnostic(Rule, parameter.Name));
                }
            }
        }
    }
}
