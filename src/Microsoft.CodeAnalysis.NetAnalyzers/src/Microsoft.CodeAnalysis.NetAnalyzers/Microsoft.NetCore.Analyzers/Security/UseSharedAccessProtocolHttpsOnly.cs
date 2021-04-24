// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseSharedAccessProtocolHttpsOnly : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5376";
        private static readonly LocalizableString s_Title = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.UseSharedAccessProtocolHttpsOnly),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Message = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.UseSharedAccessProtocolHttpsOnlyMessage),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Description = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.UseSharedAccessProtocolHttpsOnlyDescription),
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
                isDataflowRule: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        /// <summary>
        /// SharedAccessProtocol.HttpsOnly = 1, SharedAccessProtocol.HttpsOrHttp = 2.
        /// </summary>
        private const int SharedAccessProtocolHttpsOnly = 1;

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
            {
                var microsoftWindowsAzureStorageNamespaceSymbol = compilationStartAnalysisContext
                                                                    .Compilation
                                                                    .GlobalNamespace
                                                                    .GetMembers("Microsoft")
                                                                    .FirstOrDefault()
                                                                    ?.GetMembers("WindowsAzure")
                                                                    .OfType<INamespaceSymbol>()
                                                                    .FirstOrDefault()
                                                                    ?.GetMembers("Storage")
                                                                    .OfType<INamespaceSymbol>()
                                                                    .FirstOrDefault();

                if (microsoftWindowsAzureStorageNamespaceSymbol == null)
                {
                    return;
                }

                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartAnalysisContext.Compilation);

                wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.MicrosoftWindowsAzureStorageCloudStorageAccount,
                    out INamedTypeSymbol? cloudStorageAccountTypeSymbol);

                if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNullable1, out INamedTypeSymbol? nullableTypeSymbol) ||
                    !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.MicrosoftWindowsAzureStorageSharedAccessProtocol, out INamedTypeSymbol? sharedAccessProtocolTypeSymbol))
                {
                    return;
                }

                compilationStartAnalysisContext.RegisterOperationBlockStartAction(operationBlockStartContext =>
                {
                    var owningSymbol = operationBlockStartContext.OwningSymbol;
                    if (operationBlockStartContext.Options.IsConfiguredToSkipAnalysis(Rule, owningSymbol,
                            operationBlockStartContext.Compilation))
                    {
                        return;
                    }

                    operationBlockStartContext.RegisterOperationAction(operationAnalysisContext =>
                    {
                        var invocationOperation = (IInvocationOperation)operationAnalysisContext.Operation;
                        var methodSymbol = invocationOperation.TargetMethod;

                        if (methodSymbol.Name != "GetSharedAccessSignature")
                        {
                            return;
                        }

                        var namespaceSymbol = methodSymbol.ContainingNamespace;

                        while (namespaceSymbol != null)
                        {
                            if (namespaceSymbol.Equals(microsoftWindowsAzureStorageNamespaceSymbol))
                            {
                                break;
                            }

                            namespaceSymbol = namespaceSymbol.ContainingNamespace;
                        }

                        if (namespaceSymbol == null)
                        {
                            return;
                        }

                        var typeSymbol = methodSymbol.ContainingType;

                        if (!typeSymbol.Equals(cloudStorageAccountTypeSymbol))
                        {
                            var protocolsArgumentOperation = invocationOperation.Arguments.FirstOrDefault(s => s.Parameter.Name == "protocols" &&
                                                                                                                s.Parameter.Type is INamedTypeSymbol namedTypeSymbol &&
                                                                                                                namedTypeSymbol.IsGenericType &&
                                                                                                                namedTypeSymbol.ConstructedFrom.Equals(nullableTypeSymbol) &&
                                                                                                                namedTypeSymbol.TypeArguments.Length == 1 &&
                                                                                                                namedTypeSymbol.TypeArguments.Contains(sharedAccessProtocolTypeSymbol));

                            if (protocolsArgumentOperation != null)
                            {
                                if (invocationOperation.TryGetEnclosingControlFlowGraph(out var cfg))
                                {
                                    var interproceduralAnalysisConfig = InterproceduralAnalysisConfiguration.Create(
                                                                        operationAnalysisContext.Options,
                                                                        SupportedDiagnostics,
                                                                        protocolsArgumentOperation,
                                                                        operationAnalysisContext.Compilation,
                                                                        defaultInterproceduralAnalysisKind: InterproceduralAnalysisKind.None,
                                                                        defaultMaxInterproceduralMethodCallChain: 1);
                                    var valueContentAnalysisResult = ValueContentAnalysis.TryGetOrComputeResult(
                                                                                                cfg,
                                                                                                owningSymbol,
                                                                                                operationAnalysisContext.Options,
                                                                                                wellKnownTypeProvider,
                                                                                                PointsToAnalysisKind.Complete,
                                                                                                interproceduralAnalysisConfig,
                                                                                                out var copyAnalysisResult,
                                                                                                out var pointsToAnalysisResult);
                                    if (valueContentAnalysisResult == null)
                                    {
                                        return;
                                    }

                                    var protocolsArgument = valueContentAnalysisResult[protocolsArgumentOperation.Kind, protocolsArgumentOperation.Syntax];

                                    if (protocolsArgument.IsLiteralState &&
                                        !protocolsArgument.LiteralValues.Contains(SharedAccessProtocolHttpsOnly))
                                    {
                                        operationAnalysisContext.ReportDiagnostic(
                                            invocationOperation.CreateDiagnostic(
                                                Rule));
                                    }
                                }
                            }
                        }
                    }, OperationKind.Invocation);
                });
            });
        }
    }
}
