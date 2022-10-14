// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Security
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA5358: <inheritdoc cref="ApprovedCipherMode"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class ApprovedCipherModeAnalyzer : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5358";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            DiagnosticId,
            CreateLocalizableResourceString(nameof(ApprovedCipherMode)),
            CreateLocalizableResourceString(nameof(ApprovedCipherModeMessage)),
            DiagnosticCategory.Security,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(ApprovedCipherModeDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        internal static ImmutableHashSet<string> UnsafeCipherModes = ImmutableHashSet.Create(
                StringComparer.Ordinal,
                "ECB",
                "OFB",
                "CFB");

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartAnalysisContext) =>
                {
                    INamedTypeSymbol? cipherModeTypeSymbol =
                        compilationStartAnalysisContext.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSecurityCryptographyCipherMode);

                    if (cipherModeTypeSymbol == null)
                    {
                        return;
                    }

                    compilationStartAnalysisContext.RegisterOperationAction(
                        (OperationAnalysisContext operationAnalysisContext) =>
                        {
                            IFieldReferenceOperation fieldReferenceOperation =
                                (IFieldReferenceOperation)operationAnalysisContext.Operation;
                            IFieldSymbol fieldSymbol = fieldReferenceOperation.Field;

                            if (Equals(fieldSymbol.ContainingType, cipherModeTypeSymbol)
                                && UnsafeCipherModes.Contains(fieldSymbol.MetadataName))
                            {
                                operationAnalysisContext.ReportDiagnostic(
                                    Diagnostic.Create(
                                        Rule,
                                        fieldReferenceOperation.Syntax.GetLocation(),
                                        fieldSymbol.MetadataName));
                            }
                        },
                        OperationKind.FieldReference);
                });
        }
    }
}
