// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Analyzer.Utilities;
using System.Linq;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    public abstract class AbstractRemoveEmptyFinalizersAnalyzer : DiagnosticAnalyzer
    {
        public const string RuleId = "CA1821";
        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.RemoveEmptyFinalizers), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.RemoveEmptyFinalizers), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.RemoveEmptyFinalizersDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                         s_localizableTitle,
                                                                         s_localizableMessage,
                                                                         DiagnosticCategory.Performance,
                                                                         RuleLevel.BuildWarningCandidate,
                                                                         description: s_localizableDescription,
                                                                         isPortedFxCopRule: true,
                                                                         isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterCodeBlockAction(codeBlockContext =>
            {
                if (codeBlockContext.OwningSymbol.Kind != SymbolKind.Method)
                {
                    return;
                }

                var methodSymbol = (IMethodSymbol)codeBlockContext.OwningSymbol;
                if (!methodSymbol.IsDestructor())
                {
                    return;
                }

                var methodBody = methodSymbol.DeclaringSyntaxReferences.First().GetSyntax();

                if (IsEmptyFinalizer(methodBody, codeBlockContext))
                {
                    codeBlockContext.ReportDiagnostic(methodSymbol.CreateDiagnostic(Rule));
                }
            });
        }

        protected static bool InvocationIsConditional(IMethodSymbol methodSymbol, INamedTypeSymbol? conditionalAttributeSymbol) =>
            methodSymbol.HasAttribute(conditionalAttributeSymbol);

        protected abstract bool IsEmptyFinalizer(SyntaxNode methodBody, CodeBlockAnalysisContext analysisContext);
    }
}
