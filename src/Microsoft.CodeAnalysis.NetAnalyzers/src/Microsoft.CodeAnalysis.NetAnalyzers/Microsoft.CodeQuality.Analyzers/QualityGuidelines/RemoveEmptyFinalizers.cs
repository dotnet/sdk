// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class RemoveEmptyFinalizersAnalyzer : DiagnosticAnalyzer
    {
        public const string RuleId = "CA1821";
        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.RemoveEmptyFinalizers), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.RemoveEmptyFinalizers), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.RemoveEmptyFinalizersDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = new DiagnosticDescriptor(RuleId,
                                                                         s_localizableTitle,
                                                                         s_localizableMessage,
                                                                         DiagnosticCategory.Performance,
                                                                         DiagnosticHelpers.DefaultDiagnosticSeverity,
                                                                         isEnabledByDefault: DiagnosticHelpers.EnabledByDefaultForVsixAndNuget,
                                                                         description: s_localizableDescription,
                                                                         helpLinkUri: "https://docs.microsoft.com/visualstudio/code-quality/ca1821-remove-empty-finalizers",
                                                                         customTags: FxCopWellKnownDiagnosticTags.PortedFxCopRule);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext analysisContext)
        {
            analysisContext.EnableConcurrentExecution();
            analysisContext.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            analysisContext.RegisterOperationBlockStartAction(obsac =>
            {
                if (!obsac.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsConditionalAttribute, out var conditionalAttributeType) ||
                    obsac.OperationBlocks.Length != 1 ||
                    !(obsac.OperationBlocks[0] is IBlockOperation blockOperation) ||
                    !(obsac.OwningSymbol is IMethodSymbol methodSymbol) ||
                    !methodSymbol.IsDestructor())
                {
                    return;
                }

                const int IS_EMPTY = 0;
                const int IS_NOT_EMPTY = 1;
                var state = IS_EMPTY;

                obsac.RegisterOperationAction(oac =>
                {
                    if (Interlocked.CompareExchange(ref state, IS_EMPTY, IS_EMPTY) == IS_EMPTY &&
                    !((IInvocationOperation)oac.Operation).TargetMethod.HasAttribute(conditionalAttributeType))
                    {
                        Interlocked.Exchange(ref state, IS_NOT_EMPTY);
                    }
                }, OperationKind.Invocation);

                obsac.RegisterOperationAction(oac => Interlocked.Exchange(ref state, IS_NOT_EMPTY),
                    OperationKind.SimpleAssignment,
                    OperationKind.VariableDeclaration,
                    OperationKind.Loop,
                    OperationKind.Conditional,
                    OperationKind.Invalid);

                obsac.RegisterOperationBlockEndAction(obac =>
                {
                    if (state == IS_EMPTY)
                    {
                        obac.ReportDiagnostic(methodSymbol.CreateDiagnostic(Rule));
                    }
                });
            });
        }
    }
}
