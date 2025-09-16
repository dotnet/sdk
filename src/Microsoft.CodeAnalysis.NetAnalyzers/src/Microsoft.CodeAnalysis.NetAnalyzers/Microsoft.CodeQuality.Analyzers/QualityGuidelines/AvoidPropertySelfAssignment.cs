// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
    /// CA2245: <inheritdoc cref="AvoidPropertySelfAssignmentTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]

    public sealed class AvoidPropertySelfAssignment : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2245";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
            CreateLocalizableResourceString(nameof(AvoidPropertySelfAssignmentTitle)),
            CreateLocalizableResourceString(nameof(AvoidPropertySelfAssignmentMessage)),
            DiagnosticCategory.Usage,
            RuleLevel.BuildWarningCandidate,
            description: null,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterOperationAction(operationContext =>
            {
                var assignmentOperation = (IAssignmentOperation)operationContext.Operation;

                if (assignmentOperation.Target is not IPropertyReferenceOperation operationTarget)
                {
                    return;
                }

                if (assignmentOperation.Value is not IPropertyReferenceOperation operationValue)
                {
                    return;
                }

                if (!Equals(operationTarget.Property, operationValue.Property) ||
                    operationTarget.Arguments.Length != operationValue.Arguments.Length)
                {
                    return;
                }

                if (!operationTarget.Arguments.IsEmpty)
                {
                    // Indexers - compare if all the arguments are identical.
                    for (int i = 0; i < operationTarget.Arguments.Length; i++)
                    {
                        if (!IsArgumentValueEqual(operationTarget.Arguments[i].Value, operationValue.Arguments[i].Value))
                        {
                            return;
                        }
                    }
                }

                if (operationTarget.Instance is IInstanceReferenceOperation targetInstanceReference &&
                    targetInstanceReference.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance &&
                    operationValue.Instance is IInstanceReferenceOperation valueInstanceReference &&
                    valueInstanceReference.ReferenceKind == InstanceReferenceKind.ContainingTypeInstance)
                {
                    var diagnostic = valueInstanceReference.CreateDiagnostic(Rule, operationTarget.Property.Name);
                    operationContext.ReportDiagnostic(diagnostic);
                }
            }, OperationKind.SimpleAssignment);

            return;

            // Local functions
            static bool IsArgumentValueEqual(IOperation targetArg, IOperation valueArg)
            {
                // Check if arguments are identical constant/local/parameter reference operations.
                //   1. Not identical: 'this[i] = this[j]'
                //   2. Identical: 'this[i] = this[i]', 'this[0] = this[0]'
                if (targetArg.Kind != valueArg.Kind)
                {
                    return false;
                }

                if (targetArg.ConstantValue.HasValue != valueArg.ConstantValue.HasValue)
                {
                    return false;
                }

                if (targetArg.ConstantValue.HasValue)
                {
                    return Equals(targetArg.ConstantValue.Value, valueArg.ConstantValue.Value);
                }

                return targetArg switch
                {
                    ILocalReferenceOperation targetLocalReference =>
                        Equals(targetLocalReference.Local, ((ILocalReferenceOperation)valueArg).Local),
                    IParameterReferenceOperation targetParameterReference =>
                        Equals(targetParameterReference.Parameter, ((IParameterReferenceOperation)valueArg).Parameter),
                    _ => false,
                };
            }
        }
    }
}
