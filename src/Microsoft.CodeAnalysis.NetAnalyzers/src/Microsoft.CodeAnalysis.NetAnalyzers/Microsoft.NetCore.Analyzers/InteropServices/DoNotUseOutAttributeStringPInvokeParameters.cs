// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    /// <summary>
    /// CA1417: Do not use [Out] string parameters for P/Invokes
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseOutAttributeStringPInvokeParametersAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1417";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseOutAttributeStringPInvokeParametersTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseOutAttributeStringPInvokeParametersMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseOutAttributeStringPInvokeParametersDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
                                                        RuleId,
                                                        s_localizableTitle,
                                                        s_localizableMessage,
                                                        DiagnosticCategory.Interoperability,
                                                        RuleLevel.BuildWarning,
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
            DllImportData dllImportData = methodSymbol.GetDllImportData();
            if (dllImportData == null)
            {
                return;
            }

            // Warn on string parameters passed by value with [Out] attribute
            foreach (IParameterSymbol parameter in methodSymbol.Parameters)
            {
                if (parameter.Type.SpecialType == SpecialType.System_String
                    && parameter.RefKind == RefKind.None
                    && parameter.HasAttribute(outAttributeType))
                {
                    context.ReportDiagnostic(parameter.CreateDiagnostic(Rule, parameter.Name));
                }
            }
        }
    }
}
