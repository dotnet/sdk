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
    /// <summary>
    /// CA1060 - Move P/Invokes to native methods class
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class MovePInvokesToNativeMethodsClassAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1060";
        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.MovePInvokesToNativeMethodsClassTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.MovePInvokesToNativeMethodsClassMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.MovePInvokesToNativeMethodsClassDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                         s_localizableTitle,
                                                                         s_localizableMessage,
                                                                         DiagnosticCategory.Design,
                                                                         RuleLevel.Disabled,
                                                                         description: s_localizableDescription,
                                                                         isPortedFxCopRule: true,
                                                                         isDataflowRule: false,
                                                                         isEnabledByDefaultInAggressiveMode: false);

        private const string NativeMethodsText = "NativeMethods";
        private const string SafeNativeMethodsText = "SafeNativeMethods";
        private const string UnsafeNativeMethodsText = "UnsafeNativeMethods";

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(symbolContext =>
            {
                AnalyzeSymbol((INamedTypeSymbol)symbolContext.Symbol, symbolContext.ReportDiagnostic);
            }, SymbolKind.NamedType);
        }

        private static void AnalyzeSymbol(INamedTypeSymbol symbol, Action<Diagnostic> addDiagnostic)
        {
            if (symbol.GetMembers().Any(member => IsDllImport(member)) && !IsTypeNamedCorrectly(symbol.Name))
            {
                addDiagnostic(symbol.CreateDiagnostic(Rule));
            }
        }

        private static bool IsDllImport(ISymbol symbol)
        {
            return symbol.Kind == SymbolKind.Method && ((IMethodSymbol)symbol).GetDllImportData() != null;
        }

        private static bool IsTypeNamedCorrectly(string name)
        {
            return string.Compare(name, NativeMethodsText, StringComparison.Ordinal) == 0 ||
                string.Compare(name, SafeNativeMethodsText, StringComparison.Ordinal) == 0 ||
                string.Compare(name, UnsafeNativeMethodsText, StringComparison.Ordinal) == 0;
        }
    }
}
