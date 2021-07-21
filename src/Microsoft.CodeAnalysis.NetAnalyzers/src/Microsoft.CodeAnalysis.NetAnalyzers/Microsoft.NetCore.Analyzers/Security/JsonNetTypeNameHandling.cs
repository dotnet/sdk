// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    /// <summary>
    /// For using a <see cref="T:Newtonsoft.Json.TypeNameHandling"/> values other than None.
    /// </summary>
    [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    internal class JsonNetTypeNameHandling : DiagnosticAnalyzer
    {
        internal static readonly DiagnosticDescriptor Rule =
            SecurityHelpers.CreateDiagnosticDescriptor(
                "CA2326",
                nameof(MicrosoftNetCoreAnalyzersResources.JsonNetTypeNameHandlingTitle),
                nameof(MicrosoftNetCoreAnalyzersResources.JsonNetTypeNameHandlingMessage),
                RuleLevel.Disabled,
                isPortedFxCopRule: false,
                isDataflowRule: false,
                isReportedAtCompilationEnd: false,
                descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.JsonNetTypeNameHandlingDescription));

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create<DiagnosticDescriptor>(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.EnableConcurrentExecution();

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartAnalysisContext) =>
                {
                    if (!compilationStartAnalysisContext.Compilation.TryGetOrCreateTypeByMetadataName(
                            WellKnownTypeNames.NewtonsoftJsonTypeNameHandling,
                            out INamedTypeSymbol? typeNameHandlingSymbol))
                    {
                        return;
                    }

                    compilationStartAnalysisContext.RegisterOperationAction(
                        (OperationAnalysisContext operationAnalysisContext) =>
                        {
                            IFieldReferenceOperation fieldReferenceOperation =
                                (IFieldReferenceOperation)operationAnalysisContext.Operation;
                            if (IsOtherThanNone(fieldReferenceOperation))
                            {
                                operationAnalysisContext.ReportDiagnostic(
                                    fieldReferenceOperation.CreateDiagnostic(Rule));
                            }
                        },
                        OperationKind.FieldReference);

                    compilationStartAnalysisContext.RegisterOperationAction(
                        (OperationAnalysisContext operationAnalysisContext) =>
                        {
                            IAssignmentOperation assignmentOperation = (IAssignmentOperation)operationAnalysisContext.Operation;
                            if (!typeNameHandlingSymbol.Equals(assignmentOperation.Target.Type))
                            {
                                return;
                            }

                            // Find the topmost operation with non-zero (not None), unless we find an operation that would've
                            // been flagged by the FieldReference callback above.
                            foreach (IOperation childOperation in assignmentOperation.Value.DescendantsAndSelf())
                            {
                                if (childOperation is IFieldReferenceOperation fieldReferenceOperation
                                    && IsOtherThanNone(fieldReferenceOperation))
                                {
                                    return;
                                }

                                if (childOperation.ConstantValue.HasValue
                                    && childOperation.ConstantValue.Value is int integerValue
                                    && integerValue != 0)
                                {
                                    operationAnalysisContext.ReportDiagnostic(childOperation.CreateDiagnostic(Rule));
                                    return;
                                }
                            }
                        },
                        OperationKind.SimpleAssignment,
                        OperationKind.CompoundAssignment);

                    return;

                    bool IsOtherThanNone(IFieldReferenceOperation fieldReferenceOperation)
                    {
                        RoslynDebug.Assert(typeNameHandlingSymbol != null);
                        if (!typeNameHandlingSymbol.Equals(fieldReferenceOperation.Field.ContainingType))
                        {
                            return false;
                        }

                        return fieldReferenceOperation.Field.Name != "None";
                    };
                });
        }
    }
}
