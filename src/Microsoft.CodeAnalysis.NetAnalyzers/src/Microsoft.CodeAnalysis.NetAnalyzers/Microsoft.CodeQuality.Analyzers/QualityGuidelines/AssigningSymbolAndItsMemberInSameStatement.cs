// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
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
    /// CA2246: <inheritdoc cref="AssigningSymbolAndItsMemberInSameStatementTitle"/>
    /// </summary>
#pragma warning disable RS1004 // Recommend adding language support to diagnostic analyzer - Construct not valid in VB.NET
    [DiagnosticAnalyzer(LanguageNames.CSharp)]
#pragma warning restore RS1004 // Recommend adding language support to diagnostic analyzer
    public sealed class AssigningSymbolAndItsMemberInSameStatement : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2246";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(AssigningSymbolAndItsMemberInSameStatementTitle)),
            CreateLocalizableResourceString(nameof(AssigningSymbolAndItsMemberInSameStatementMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.IdeSuggestion,
            description: CreateLocalizableResourceString(nameof(AssigningSymbolAndItsMemberInSameStatementDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterOperationAction(AnalyzeAssignment, OperationKind.SimpleAssignment);
        }

        private void AnalyzeAssignment(OperationAnalysisContext context)
        {
            var assignmentOperation = (ISimpleAssignmentOperation)context.Operation;

            // Check if there are more then one assignment in a statement
            if (assignmentOperation.Target is not IMemberReferenceOperation operationTarget)
            {
                return;
            }

            // This analyzer makes sense only for reference type objects
            if (operationTarget.Instance?.Type?.IsReferenceType != true)
            {
                return;
            }

            bool isViolationFound = operationTarget.Instance switch
            {
                ILocalReferenceOperation localInstance =>
                    AnalyzeAssignmentToMember(assignmentOperation, localInstance, (a, b) => a.Local.Equals(b.Local)),
                IMemberReferenceOperation memberInstance =>
                    AnalyzeAssignmentToMember(assignmentOperation, memberInstance, (a, b) => a.Member.Equals(b.Member) && a.Instance?.Syntax.ToString() == b.Instance?.Syntax.ToString()),
                IParameterReferenceOperation parameterInstance =>
                    AnalyzeAssignmentToMember(assignmentOperation, parameterInstance, (a, b) => a.Parameter.Equals(b.Parameter)),
                _ => false,
            };

            if (isViolationFound)
            {
                var diagnostic = operationTarget.CreateDiagnostic(Rule, operationTarget.Instance.Syntax, operationTarget.Member.Name);
                context.ReportDiagnostic(diagnostic);
            }
        }

        private static bool AnalyzeAssignmentToMember<T>(ISimpleAssignmentOperation assignmentOperation, T instance, Func<T, T, bool> equalityComparer) where T : class, IOperation
        {
            // Check every simple assignments target in a statement for equality to `instance`
            while (assignmentOperation.Value.Kind == OperationKind.SimpleAssignment)
            {
                assignmentOperation = (ISimpleAssignmentOperation)assignmentOperation.Value;

                if (assignmentOperation.Target is T operationValue &&
                    equalityComparer(instance, operationValue))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
