// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
    /// CA5375: <inheritdoc cref="DoNotUseAccountSAS"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseAccountSAS : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5375";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            DiagnosticId,
            CreateLocalizableResourceString(nameof(DoNotUseAccountSAS)),
            CreateLocalizableResourceString(nameof(DoNotUseAccountSASMessage)),
            DiagnosticCategory.Security,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(DoNotUseAccountSASDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
            {
                if (!compilationStartAnalysisContext.Compilation.TryGetOrCreateTypeByMetadataName(
                                                WellKnownTypeNames.MicrosoftWindowsAzureStorageCloudStorageAccount,
                                                out INamedTypeSymbol? cloudStorageAccountTypeSymbol))
                {
                    return;
                }

                compilationStartAnalysisContext.RegisterOperationAction(operationAnalysisContext =>
                {
                    var invocationOperation = (IInvocationOperation)operationAnalysisContext.Operation;
                    var methodSymbol = invocationOperation.TargetMethod;

                    if (methodSymbol.Name == "GetSharedAccessSignature" &&
                        methodSymbol.ContainingType.Equals(cloudStorageAccountTypeSymbol))
                    {
                        operationAnalysisContext.ReportDiagnostic(
                                invocationOperation.CreateDiagnostic(
                                    Rule));
                    }
                }, OperationKind.Invocation);
            });
        }
    }
}
