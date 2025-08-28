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
    /// CA5373: <inheritdoc cref="DoNotUseObsoleteKDFAlgorithm"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseObsoleteKDFAlgorithm : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5373";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            DiagnosticId,
            CreateLocalizableResourceString(nameof(DoNotUseObsoleteKDFAlgorithm)),
            CreateLocalizableResourceString(nameof(DoNotUseObsoleteKDFAlgorithmMessage)),
            DiagnosticCategory.Security,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: CreateLocalizableResourceString(nameof(DoNotUseObsoleteKDFAlgorithmDescription)),
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
                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartAnalysisContext.Compilation);

                wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                            WellKnownTypeNames.SystemSecurityCryptographyPasswordDeriveBytes,
                            out INamedTypeSymbol? passwordDeriveBytesTypeSymbol);
                wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                            WellKnownTypeNames.SystemSecurityCryptographyRfc2898DeriveBytes,
                            out INamedTypeSymbol? rfc2898DeriveBytesTypeSymbol);

                if (passwordDeriveBytesTypeSymbol == null && rfc2898DeriveBytesTypeSymbol == null)
                {
                    return;
                }

                compilationStartAnalysisContext.RegisterOperationAction(operationAnalysisContext =>
                {
                    var invocationOperation = (IInvocationOperation)operationAnalysisContext.Operation;
                    var methodSymbol = invocationOperation.TargetMethod;

                    if (methodSymbol.MethodKind == MethodKind.Constructor)
                    {
                        return;
                    }

                    var typeSymbol = methodSymbol.ContainingType;

                    if (typeSymbol == null)
                    {
                        return;
                    }

                    var methodName = methodSymbol.Name;

                    if (typeSymbol.Equals(passwordDeriveBytesTypeSymbol) ||
                        typeSymbol.Equals(rfc2898DeriveBytesTypeSymbol) &&
                        methodName == "CryptDeriveKey")
                    {
                        operationAnalysisContext.ReportDiagnostic(
                            invocationOperation.CreateDiagnostic(
                                Rule,
                                typeSymbol.Name,
                                methodName));
                    }
                }, OperationKind.Invocation);
            });
        }
    }
}
