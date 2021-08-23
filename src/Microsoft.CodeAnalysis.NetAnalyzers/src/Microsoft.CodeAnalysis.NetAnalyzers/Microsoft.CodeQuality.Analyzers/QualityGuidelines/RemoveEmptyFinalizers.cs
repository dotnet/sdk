// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
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

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                         s_localizableTitle,
                                                                         s_localizableMessage,
                                                                         DiagnosticCategory.Performance,
                                                                         RuleLevel.BuildWarningCandidate,
                                                                         description: s_localizableDescription,
                                                                         isPortedFxCopRule: true,
                                                                         isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDiagnosticsConditionalAttribute, out var conditionalAttributeType))
                {
                    return;
                }

                context.RegisterOperationBlockAction(context =>
                {
                    if (context.OperationBlocks.Length != 1 ||
                        context.OperationBlocks[0] is not IBlockOperation blockOperation ||
                        context.OwningSymbol is not IMethodSymbol methodSymbol ||
                        !methodSymbol.IsDestructor())
                    {
                        return;
                    }

                    var isMethodSurroundedWithDirective = blockOperation.Syntax.Parent.ContainsDirectives;

                    if (!blockOperation.HasAnyExplicitDescendant(op => CanDescendIntoOperation(op, conditionalAttributeType, isMethodSurroundedWithDirective)))
                    {
                        context.ReportDiagnostic(methodSymbol.CreateDiagnostic(Rule));
                    }
                });
            });
        }

        private static bool CanDescendIntoOperation(IOperation operation, INamedTypeSymbol conditionalAttributeType, bool isMethodSurroundedWithDirective)
        {
            if (operation.Kind == OperationKind.Throw)
            {
                return false;
            }

            if (operation.Kind == OperationKind.Invocation)
            {
                return isMethodSurroundedWithDirective
                    || !((IInvocationOperation)operation).TargetMethod.HasAttribute(conditionalAttributeType);
            }

            return true;
        }
    }
}
