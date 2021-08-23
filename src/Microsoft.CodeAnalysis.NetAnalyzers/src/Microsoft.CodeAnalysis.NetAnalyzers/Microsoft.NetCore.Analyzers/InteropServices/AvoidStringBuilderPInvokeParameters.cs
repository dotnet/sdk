// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    /// <summary>
    /// CA1838: Avoid StringBuilder parameters for P/Invokes
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AvoidStringBuilderPInvokeParametersAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1838";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.AvoidStringBuilderPInvokeParametersTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.AvoidStringBuilderPInvokeParametersMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.AvoidStringBuilderPInvokeParametersDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
                                                        RuleId,
                                                        s_localizableTitle,
                                                        s_localizableMessage,
                                                        DiagnosticCategory.Performance,
                                                        RuleLevel.IdeHidden_BulkConfigurable, // Only for users explicitly targeting performance - addressing violation is non-trivial
                                                        description: s_localizableDescription,
                                                        isPortedFxCopRule: false,
                                                        isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

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
            DllImportData dllImportData = methodSymbol.GetDllImportData();
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
