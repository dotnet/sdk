// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AvoidOutParameters : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1021";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidOutParametersTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidOutParametersMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.AvoidOutParametersDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Design,
                                                                             DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                             isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultIfNotBuildingVSIX,
                                                                             description: s_localizableDescription,
                                                                             helpLinkUri: "https://docs.microsoft.com/en-us/visualstudio/code-quality/ca1021",
                                                                             customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCompilationStartAction(csac =>
            {
                var outAttributeType = csac.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesOutAttribute);

                csac.RegisterSymbolAction(sac =>
                {
                    var methodSymbol = (IMethodSymbol)sac.Symbol;

                    var numberOfOutParams = methodSymbol.Parameters.Count(p => IsOutParameter(p, outAttributeType));

                    if (numberOfOutParams >= 1 && !IsTryPatternMethod(methodSymbol, outAttributeType, numberOfOutParams))
                    {
                        sac.ReportDiagnostic(methodSymbol.CreateDiagnostic(Rule));
                    }
                }, SymbolKind.Method);
            });
        }

        private static bool IsOutParameter(IParameterSymbol parameterSymbol, INamedTypeSymbol? outAttributeSymbol) =>
            parameterSymbol.RefKind == RefKind.Out ||
            // Handle VB.NET special case for out parameters
            (parameterSymbol.RefKind == RefKind.Ref && parameterSymbol.HasAttribute(outAttributeSymbol));

        private static bool IsTryPatternMethod(IMethodSymbol methodSymbol, INamedTypeSymbol? outAttributeSymbol, int numberOfOutParams) =>
            methodSymbol.Name.StartsWith("Try", StringComparison.Ordinal) &&
            methodSymbol.ReturnType.SpecialType == SpecialType.System_Boolean &&
            methodSymbol.Parameters.Length >= 1 &&
            IsOutParameter(methodSymbol.Parameters[methodSymbol.Parameters.Length - 1], outAttributeSymbol) &&
            numberOfOutParams == 1;
    }
}