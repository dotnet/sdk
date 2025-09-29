// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA2219: <inheritdoc cref="DoNotRaiseExceptionsInExceptionClausesTitle"/>
    /// </summary>
    /// <remarks>
    /// The original FxCop implementation of this rule finds violations of this rule inside 
    /// filter and fault blocks. However in both C# and VB there's no way to throw an exception
    /// inside a filter block and there is no language representation for fault blocks in either language.
    /// So this analyzer just checks for throw statements inside finally blocks.
    /// </remarks>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotRaiseExceptionsInExceptionClausesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2219";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(DoNotRaiseExceptionsInExceptionClausesTitle)),
            CreateLocalizableResourceString(nameof(DoNotRaiseExceptionsInExceptionClausesMessageFinally)),
            DiagnosticCategory.Usage,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(DoNotRaiseExceptionsInExceptionClausesDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterOperationBlockAction(operationBlockContext =>
            {
                foreach (var block in operationBlockContext.OperationBlocks)
                {
                    var walker = new ThrowInsideFinallyWalker();
                    walker.Visit(block);

                    foreach (var throwStatement in walker.ThrowExpressions)
                    {
                        operationBlockContext.ReportDiagnostic(throwStatement.Syntax.CreateDiagnostic(Rule));
                    }
                }
            });
        }

        /// <summary>
        /// Walks an IOperation tree to find throw expressions inside finally blocks.
        /// </summary>
        private class ThrowInsideFinallyWalker : OperationWalker
        {
            private int _finallyBlockNestingDepth;

            public List<IThrowOperation> ThrowExpressions { get; private set; } = new List<IThrowOperation>();

            public override void VisitTry(ITryOperation operation)
            {
                Visit(operation.Body);
                foreach (var catchClause in operation.Catches)
                {
                    Visit(catchClause);
                }

                _finallyBlockNestingDepth++;
                Visit(operation.Finally);
                _finallyBlockNestingDepth--;
            }

            public override void VisitThrow(IThrowOperation operation)
            {
                if (_finallyBlockNestingDepth > 0)
                {
                    ThrowExpressions.Add(operation);
                }

                base.VisitThrow(operation);
            }
        }
    }
}