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
    /// CA5394: <inheritdoc cref="DoNotUseInsecureRandomness"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotUseInsecureRandomness : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5394";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            DiagnosticId,
            CreateLocalizableResourceString(nameof(DoNotUseInsecureRandomness)),
            CreateLocalizableResourceString(nameof(DoNotUseInsecureRandomnessMessage)),
            DiagnosticCategory.Security,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(DoNotUseInsecureRandomnessDescription)),
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
                if (!compilationStartAnalysisContext.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRandom, out var randomTypeSymbol))
                {
                    return;
                }

                compilationStartAnalysisContext.RegisterOperationAction(operationAnalysisContext =>
                {
                    var invocationOperation = (IInvocationOperation)operationAnalysisContext.Operation;
                    var typeSymbol = invocationOperation.TargetMethod.ContainingType;

                    if (randomTypeSymbol.Equals(typeSymbol))
                    {
                        operationAnalysisContext.ReportDiagnostic(
                            invocationOperation.CreateDiagnostic(
                                Rule,
                                typeSymbol.Name));
                    }
                }, OperationKind.Invocation);
            });
        }
    }
}
