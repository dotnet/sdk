﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
    /// CA2011: <inheritdoc cref="AvoidInfiniteRecursionTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class AvoidInfiniteRecursion : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2011";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
            CreateLocalizableResourceString(nameof(AvoidInfiniteRecursionTitle)),
            CreateLocalizableResourceString(nameof(AvoidInfiniteRecursionMessageSure)),
            DiagnosticCategory.Reliability,
            RuleLevel.IdeSuggestion,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor MaybeRule = DiagnosticDescriptorHelper.Create(RuleId,
            CreateLocalizableResourceString(nameof(AvoidInfiniteRecursionTitle)),
            CreateLocalizableResourceString(nameof(AvoidInfiniteRecursionMessageMaybe)),
            DiagnosticCategory.Reliability,
            RuleLevel.IdeSuggestion,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule, MaybeRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterOperationBlockStartAction(operationBlockStartContext =>
            {
                if (operationBlockStartContext.OwningSymbol is not IMethodSymbol methodSymbol ||
                    methodSymbol.MethodKind != MethodKind.PropertySet)
                {
                    return;
                }

                operationBlockStartContext.RegisterOperationAction(operationContext =>
                {
                    var assignmentOperation = (IAssignmentOperation)operationContext.Operation;

                    if (assignmentOperation.Target is not IPropertyReferenceOperation operationTarget ||
                        operationTarget.Instance is not IInstanceReferenceOperation targetInstanceReference ||
                        targetInstanceReference.ReferenceKind != InstanceReferenceKind.ContainingTypeInstance ||
                        !operationTarget.Member.Equals(methodSymbol.AssociatedSymbol))
                    {
                        return;
                    }

                    IOperation? ancestor = assignmentOperation;
                    do
                    {
                        ancestor = ancestor.Parent;
                    } while (ancestor != null &&
                        ancestor.Kind != OperationKind.AnonymousFunction &&
                        ancestor.Kind != OperationKind.LocalFunction &&
                        ancestor.Kind != OperationKind.Conditional);

                    operationContext.ReportDiagnostic(
                        assignmentOperation.CreateDiagnostic(ancestor != null ? MaybeRule : Rule, operationTarget.Property.Name));
                }, OperationKind.SimpleAssignment);
            });
        }
    }
}
