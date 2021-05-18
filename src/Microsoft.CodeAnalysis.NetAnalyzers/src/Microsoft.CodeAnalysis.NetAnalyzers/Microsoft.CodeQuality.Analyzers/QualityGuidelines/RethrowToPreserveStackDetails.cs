// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class RethrowToPreserveStackDetailsAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2200";
        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.RethrowToPreserveStackDetailsTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.RethrowToPreserveStackDetailsMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                         s_localizableTitle,
                                                                         s_localizableMessage,
                                                                         DiagnosticCategory.Usage,
                                                                         RuleLevel.BuildWarning,
                                                                         description: null,
                                                                         isPortedFxCopRule: true,
                                                                         isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterOperationAction(context =>
            {
                var throwOperation = (IThrowOperation)context.Operation;

                if (throwOperation.Exception is not ILocalReferenceOperation localReference)
                {
                    return;
                }

                IOperation ancestor = throwOperation;
                while (ancestor != null &&
                    ancestor.Kind != OperationKind.AnonymousFunction &&
                    ancestor.Kind != OperationKind.LocalFunction)
                {
                    if (ancestor.Kind == OperationKind.CatchClause &&
                        ancestor is ICatchClauseOperation catchClause)
                    {
                        if (catchClause.ExceptionDeclarationOrExpression is IVariableDeclaratorOperation variableDeclaratorOperation &&
                            SymbolEqualityComparer.Default.Equals(variableDeclaratorOperation.Symbol, localReference.Local) &&
                            !IsReassignedInCatch(catchClause, localReference))
                        {
                            context.ReportDiagnostic(throwOperation.CreateDiagnostic(Rule));
                        }

                        return;
                    }

                    ancestor = ancestor.Parent;
                }
            }, OperationKind.Throw);
        }

        private static bool IsReassignedInCatch(ICatchClauseOperation catchClause, ILocalReferenceOperation localReference)
        {
            var dataflow = catchClause.Language == LanguageNames.CSharp
                ? catchClause.SemanticModel.AnalyzeDataFlow(catchClause.Handler.Syntax)
                : catchClause.SemanticModel.AnalyzeDataFlow(catchClause.Handler.Operations[0].Syntax, catchClause.Handler.Operations[^1].Syntax);

            return dataflow.WrittenInside.Contains(localReference.Local);
        }
    }
}
