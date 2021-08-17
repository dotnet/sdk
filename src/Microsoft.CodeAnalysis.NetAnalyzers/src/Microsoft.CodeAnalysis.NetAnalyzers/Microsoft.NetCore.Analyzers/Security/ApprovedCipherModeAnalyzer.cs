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
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class ApprovedCipherModeAnalyzer : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5358";
        private static readonly LocalizableString s_Title = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.ApprovedCipherMode),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Message = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.ApprovedCipherModeMessage),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Description = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.ApprovedCipherModeDescription),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
                DiagnosticId,
                s_Title,
                s_Message,
                DiagnosticCategory.Security,
                RuleLevel.Disabled,
                description: s_Description,
                isPortedFxCopRule: false,
                isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

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
