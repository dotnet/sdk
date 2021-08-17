// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class SslProtocolsAnalyzer : DiagnosticAnalyzer
    {
        internal static DiagnosticDescriptor DeprecatedRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5397",
            nameof(MicrosoftNetCoreAnalyzersResources.DeprecatedSslProtocolsTitle),
            nameof(MicrosoftNetCoreAnalyzersResources.DeprecatedSslProtocolsMessage),
            RuleLevel.IdeHidden_BulkConfigurable,
            isPortedFxCopRule: false,
            isDataflowRule: false,
            isReportedAtCompilationEnd: false,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.DeprecatedSslProtocolsDescription));
        internal static DiagnosticDescriptor HardcodedRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5398",
            nameof(MicrosoftNetCoreAnalyzersResources.HardcodedSslProtocolsTitle),
            nameof(MicrosoftNetCoreAnalyzersResources.HardcodedSslProtocolsMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: false,
            isReportedAtCompilationEnd: false,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.HardcodedSslProtocolsDescription));

        private readonly ImmutableHashSet<string> HardcodedSslProtocolsMetadataNames = ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "Tls12",
            "Tls13");

        private const int UnsafeBits = 12 | 48 | 192 | 768;    // SslProtocols Ssl2 Ssl3 Tls Tls11

        private const int HardcodedBits = 3072 | 12288;    // SslProtocols Tls12 Tls13

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DeprecatedRule, HardcodedRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartAnalysisContext) =>
                {
                    if (!compilationStartAnalysisContext.Compilation.TryGetOrCreateTypeByMetadataName(
                            WellKnownTypeNames.SystemSecurityAuthenticationSslProtocols,
                            out INamedTypeSymbol? sslProtocolsSymbol))
                    {
                        return;
                    }

                    compilationStartAnalysisContext.RegisterOperationAction(
                        (OperationAnalysisContext operationAnalysisContext) =>
                        {
                            IFieldReferenceOperation fieldReferenceOperation = (IFieldReferenceOperation)operationAnalysisContext.Operation;
                            if (IsReferencingSslProtocols(
                                    fieldReferenceOperation,
                                    out bool isDeprecatedProtocol,
                                    out bool isHardcodedOkayProtocol))
                            {
                                if (isDeprecatedProtocol)
                                {
                                    operationAnalysisContext.ReportDiagnostic(
                                        fieldReferenceOperation.CreateDiagnostic(
                                            DeprecatedRule,
                                            fieldReferenceOperation.Field.Name));
                                }
                                else if (isHardcodedOkayProtocol)
                                {
                                    operationAnalysisContext.ReportDiagnostic(
                                        fieldReferenceOperation.CreateDiagnostic(
                                            HardcodedRule,
                                            fieldReferenceOperation.Field.Name));
                                }
                            }
                        },
                        OperationKind.FieldReference);

                    compilationStartAnalysisContext.RegisterOperationAction(
                        (OperationAnalysisContext operationAnalysisContext) =>
                        {
                            IOperation? valueOperation;
                            switch (operationAnalysisContext.Operation)
                            {
                                case IAssignmentOperation assignmentOperation:
                                    // Make sure this is an assignment operation for a SslProtocols value.
                                    if (!sslProtocolsSymbol.Equals(assignmentOperation.Target.Type))
                                    {
                                        return;
                                    }

                                    valueOperation = assignmentOperation.Value;
                                    break;

                                case IArgumentOperation argumentOperation:
                                    if (!sslProtocolsSymbol.Equals(argumentOperation.Type))
                                    {
                                        return;
                                    }

                                    valueOperation = argumentOperation.Value;
                                    break;

                                case IReturnOperation returnOperation:
                                    if (returnOperation.ReturnedValue == null
                                        || !sslProtocolsSymbol.Equals(returnOperation.ReturnedValue.Type))
                                    {
                                        return;
                                    }

                                    valueOperation = returnOperation.ReturnedValue;
                                    break;

                                case IVariableInitializerOperation variableInitializerOperation:
                                    if (variableInitializerOperation.Value == null
                                        || !sslProtocolsSymbol.Equals(variableInitializerOperation.Value.Type))
                                    {
                                        return;
                                    }

                                    valueOperation = variableInitializerOperation.Value;
                                    break;

                                default:
                                    Debug.Fail("Unhandled IOperation " + operationAnalysisContext.Operation.Kind);
                                    return;
                            }

                            // Find the topmost operation with a bad bit set, unless we find an operation that would've been
                            // flagged by the FieldReference callback above.
                            IOperation? foundDeprecatedOperation = null;
                            bool foundDeprecatedReference = false;
                            IOperation? foundHardcodedOperation = null;
                            bool foundHardcodedReference = false;
                            foreach (IOperation childOperation in valueOperation.DescendantsAndSelf())
                            {
                                if (childOperation is IFieldReferenceOperation fieldReferenceOperation
                                    && IsReferencingSslProtocols(
                                        fieldReferenceOperation,
                                        out var isDeprecatedProtocol,
                                        out var isHardcodedOkayProtocol))
                                {
                                    if (isDeprecatedProtocol)
                                    {
                                        foundDeprecatedReference = true;
                                    }
                                    else if (isHardcodedOkayProtocol)
                                    {
                                        foundHardcodedReference = true;
                                    }

                                    if (foundDeprecatedReference && foundHardcodedReference)
                                    {
                                        return;
                                    }
                                }

                                if (childOperation.ConstantValue.HasValue
                                    && childOperation.ConstantValue.Value is int integerValue)
                                {
                                    if (foundDeprecatedOperation == null    // Only want the first.
                                        && (integerValue & UnsafeBits) != 0)
                                    {
                                        foundDeprecatedOperation = childOperation;
                                    }

                                    if (foundHardcodedOperation == null    // Only want the first.
                                        && (integerValue & HardcodedBits) != 0)
                                    {
                                        foundHardcodedOperation = childOperation;
                                    }
                                }
                            }

                            if (foundDeprecatedOperation != null && !foundDeprecatedReference)
                            {
                                operationAnalysisContext.ReportDiagnostic(
                                    foundDeprecatedOperation.CreateDiagnostic(
                                        DeprecatedRule,
                                        foundDeprecatedOperation.ConstantValue));
                            }

                            if (foundHardcodedOperation != null && !foundHardcodedReference)
                            {
                                operationAnalysisContext.ReportDiagnostic(
                                    foundHardcodedOperation.CreateDiagnostic(
                                        HardcodedRule,
                                        foundHardcodedOperation.ConstantValue));
                            }
                        },
                        OperationKind.SimpleAssignment,
                        OperationKind.CompoundAssignment,
                        OperationKind.Argument,
                        OperationKind.Return,
                        OperationKind.VariableInitializer);

                    return;

                    // Local function(s).
                    bool IsReferencingSslProtocols(
                        IFieldReferenceOperation fieldReferenceOperation,
                        out bool isDeprecatedProtocol,
                        out bool isHardcodedOkayProtocol)
                    {
                        RoslynDebug.Assert(sslProtocolsSymbol != null);

                        if (sslProtocolsSymbol.Equals(fieldReferenceOperation.Field.ContainingType))
                        {
                            if (HardcodedSslProtocolsMetadataNames.Contains(fieldReferenceOperation.Field.Name))
                            {
                                isHardcodedOkayProtocol = true;
                                isDeprecatedProtocol = false;
                            }
                            else if (fieldReferenceOperation.Field.Name == "None")
                            {
                                isHardcodedOkayProtocol = false;
                                isDeprecatedProtocol = false;
                            }
                            else
                            {
                                isDeprecatedProtocol = true;
                                isHardcodedOkayProtocol = false;
                            }

                            return true;
                        }
                        else
                        {
                            isHardcodedOkayProtocol = false;
                            isDeprecatedProtocol = false;
                            return false;
                        }
                    }
                });
        }
    }
}
