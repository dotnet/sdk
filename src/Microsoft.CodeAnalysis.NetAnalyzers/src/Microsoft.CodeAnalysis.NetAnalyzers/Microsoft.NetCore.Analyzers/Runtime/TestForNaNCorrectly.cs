// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2242: <inheritdoc cref="TestForNaNCorrectlyTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class TestForNaNCorrectlyAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2242";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(TestForNaNCorrectlyTitle)),
            CreateLocalizableResourceString(nameof(TestForNaNCorrectlyMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.BuildWarningCandidate,
            description: CreateLocalizableResourceString(nameof(TestForNaNCorrectlyDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        private readonly BinaryOperatorKind[] _comparisonOperators = new[]
        {
            BinaryOperatorKind.Equals,
            BinaryOperatorKind.GreaterThan,
            BinaryOperatorKind.GreaterThanOrEqual,
            BinaryOperatorKind.LessThan,
            BinaryOperatorKind.LessThanOrEqual,
            BinaryOperatorKind.NotEquals
        };

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterOperationAction(
                operationAnalysisContext =>
                {
                    var binaryOperatorExpression = (IBinaryOperation)operationAnalysisContext.Operation;
                    if (!_comparisonOperators.Contains(binaryOperatorExpression.OperatorKind))
                    {
                        return;
                    }

                    if (IsNan(binaryOperatorExpression.LeftOperand) || IsNan(binaryOperatorExpression.RightOperand))
                    {
                        operationAnalysisContext.ReportDiagnostic(
                            binaryOperatorExpression.Syntax.CreateDiagnostic(Rule));
                    }
                },
                OperationKind.BinaryOperator);
        }

        private static bool IsNan(IOperation expr)
        {
            if (expr == null ||
                !expr.ConstantValue.HasValue)
            {
                return false;
            }

            object? value = expr.ConstantValue.Value;
            if (value is float single)
            {
                return float.IsNaN(single);
            }

            if (value is double @double)
            {
                return double.IsNaN(@double);
            }

            return false;
        }
    }
}