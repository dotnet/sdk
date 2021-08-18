// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseDeprecatedSecurityProtocols : DiagnosticAnalyzer
    {
        internal static DiagnosticDescriptor DeprecatedRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5364",
            typeof(MicrosoftNetCoreAnalyzersResources),
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseDeprecatedSecurityProtocols),
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseDeprecatedSecurityProtocolsMessage),
            RuleLevel.IdeHidden_BulkConfigurable,
            isPortedFxCopRule: false,
            isDataflowRule: false,
            isReportedAtCompilationEnd: false,
            descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.DoNotUseDeprecatedSecurityProtocolsDescription));
        internal static DiagnosticDescriptor HardCodedRule = SecurityHelpers.CreateDiagnosticDescriptor(
            "CA5386",
            typeof(MicrosoftNetCoreAnalyzersResources),
            nameof(MicrosoftNetCoreAnalyzersResources.HardCodedSecurityProtocolTitle),
            nameof(MicrosoftNetCoreAnalyzersResources.HardCodedSecurityProtocolMessage),
            RuleLevel.Disabled,
            isPortedFxCopRule: false,
            isDataflowRule: false,
            isReportedAtCompilationEnd: false);

        private readonly ImmutableHashSet<string> HardCodedSafeProtocolMetadataNames = ImmutableHashSet.Create(
            StringComparer.Ordinal,
            "Tls12",
            "Tls13");
        private const string SystemDefaultName = "SystemDefault";

        private const int UnsafeBits = 48 | 192 | 768;    // SecurityProtocolType Ssl3 Tls10 Tls11

        private const int HardCodedBits = 3072 | 12288;    // SecurityProtocolType Tls12 Tls13

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(DeprecatedRule, HardCodedRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartAnalysisContext) =>
                {
                    var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartAnalysisContext.Compilation);
                    if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                            WellKnownTypeNames.SystemNetSecurityProtocolType,
                            out var securityProtocolTypeTypeSymbol)
                        || !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                                WellKnownTypeNames.SystemNetServicePointManager,
                                out var servicePointManagerTypeSymbol))
                    {
                        return;
                    }

                    compilationStartAnalysisContext.RegisterOperationAction(
                        (OperationAnalysisContext operationAnalysisContext) =>
                        {
                            var fieldReferenceOperation = (IFieldReferenceOperation)operationAnalysisContext.Operation;

                            // Make sure we're not inside an &= assignment like:
                            //   ServicePointManager.SecurityProtocol &= ~(SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11)
                            // cuz &= is at worst, disabling protocol versions.
                            if (IsReferencingSecurityProtocolType(
                                    fieldReferenceOperation,
                                    out var isDeprecatedProtocol,
                                    out var isHardCodedOkayProtocol)
                                && null == fieldReferenceOperation.GetAncestor<ICompoundAssignmentOperation>(
                                      OperationKind.CompoundAssignment,
                                      IsAndEqualsServicePointManagerAssignment))
                            {
                                if (isDeprecatedProtocol)
                                {
                                    operationAnalysisContext.ReportDiagnostic(
                                        fieldReferenceOperation.CreateDiagnostic(
                                            DeprecatedRule,
                                            fieldReferenceOperation.Field.Name));
                                }
                                else if (isHardCodedOkayProtocol)
                                {
                                    operationAnalysisContext.ReportDiagnostic(
                                        fieldReferenceOperation.CreateDiagnostic(
                                            HardCodedRule,
                                            fieldReferenceOperation.Field.Name));
                                }
                            }
                        }, OperationKind.FieldReference);

                    compilationStartAnalysisContext.RegisterOperationAction(
                        (OperationAnalysisContext operationAnalysisContext) =>
                        {
                            var assignmentOperation = (IAssignmentOperation)operationAnalysisContext.Operation;

                            // Make sure this is an assignment operation for a SecurityProtocolType, and not
                            // an assignment like:
                            //   ServicePointManager.SecurityProtocol &= ~(SecurityProtocolType.Ssl3 | SecurityProtocolType.Tls | SecurityProtocolType.Tls11)
                            // cuz &= is at worst, disabling protocol versions.
                            if (!securityProtocolTypeTypeSymbol.Equals(assignmentOperation.Target.Type)
                                || (assignmentOperation is ICompoundAssignmentOperation compoundAssignmentOperation
                                    && IsAndEqualsServicePointManagerAssignment(compoundAssignmentOperation)))
                            {
                                return;
                            }

                            // Find the topmost operation with a bad bit set, unless we find an operation that would've been
                            // flagged by the FieldReference callback above.
                            IOperation? foundDeprecatedOperation = null;
                            bool foundDeprecatedReference = false;
                            IOperation? foundHardCodedOperation = null;
                            bool foundHardCodedReference = false;
                            foreach (IOperation childOperation in assignmentOperation.Value.DescendantsAndSelf())
                            {
                                if (childOperation is IFieldReferenceOperation fieldReferenceOperation
                                    && IsReferencingSecurityProtocolType(
                                        fieldReferenceOperation,
                                        out var isDeprecatedProtocol,
                                        out var isHardCodedOkayProtocol))
                                {
                                    if (isDeprecatedProtocol)
                                    {
                                        foundDeprecatedReference = true;
                                    }
                                    else if (isHardCodedOkayProtocol)
                                    {
                                        foundHardCodedReference = true;
                                    }

                                    if (foundDeprecatedReference && foundHardCodedReference)
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

                                    if (foundHardCodedOperation == null    // Only want the first.
                                        && (integerValue & HardCodedBits) != 0)
                                    {
                                        foundHardCodedOperation = childOperation;
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

                            if (foundHardCodedOperation != null && !foundHardCodedReference)
                            {
                                operationAnalysisContext.ReportDiagnostic(
                                    foundHardCodedOperation.CreateDiagnostic(
                                        HardCodedRule,
                                        foundHardCodedOperation.ConstantValue));
                            }
                        },
                        OperationKind.SimpleAssignment,
                        OperationKind.CompoundAssignment);

                    return;

                    // Local function(s).
                    bool IsReferencingSecurityProtocolType(
                        IFieldReferenceOperation fieldReferenceOperation,
                        out bool isDeprecatedProtocol,
                        out bool isHardCodedOkayProtocol)
                    {
                        RoslynDebug.Assert(securityProtocolTypeTypeSymbol != null);

                        if (securityProtocolTypeTypeSymbol.Equals(fieldReferenceOperation.Field.ContainingType))
                        {
                            if (HardCodedSafeProtocolMetadataNames.Contains(fieldReferenceOperation.Field.Name))
                            {
                                isHardCodedOkayProtocol = true;
                                isDeprecatedProtocol = false;
                            }
                            else if (fieldReferenceOperation.Field.Name == SystemDefaultName)
                            {
                                isHardCodedOkayProtocol = false;
                                isDeprecatedProtocol = false;
                            }
                            else
                            {
                                isDeprecatedProtocol = true;
                                isHardCodedOkayProtocol = false;
                            }

                            return true;
                        }
                        else
                        {
                            isHardCodedOkayProtocol = false;
                            isDeprecatedProtocol = false;
                            return false;
                        }
                    }

                    bool IsAndEqualsServicePointManagerAssignment(ICompoundAssignmentOperation compoundAssignmentOperation)
                    {
                        RoslynDebug.Assert(servicePointManagerTypeSymbol != null);

                        return compoundAssignmentOperation.OperatorKind == BinaryOperatorKind.And
                            && compoundAssignmentOperation.Target is IPropertyReferenceOperation targetPropertyReference
                            && targetPropertyReference.Instance == null
                            && servicePointManagerTypeSymbol.Equals(targetPropertyReference.Property.ContainingType)
                            && targetPropertyReference.Property.MetadataName == "SecurityProtocol";
                    }
                });
        }
    }
}
