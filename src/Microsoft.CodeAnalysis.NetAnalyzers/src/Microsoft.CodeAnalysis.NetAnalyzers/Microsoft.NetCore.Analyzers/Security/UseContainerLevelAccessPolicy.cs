// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseContainerLevelAccessPolicy : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5377";
        private static readonly LocalizableString s_Title = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.UseContainerLevelAccessPolicy),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Message = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.UseContainerLevelAccessPolicyMessage),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Description = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.UseContainerLevelAccessPolicyDescription),
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
                isDataflowRule: true,
                isEnabledByDefaultInFxCopAnalyzers: false);

        internal static ImmutableArray<(string nspace, string policyIdentifierName)> NamespaceAndPolicyIdentifierNamePairs = ImmutableArray.Create(
                                                                                                    ("Blob", "groupPolicyIdentifier"),
                                                                                                    ("File", "groupPolicyIdentifier"),
                                                                                                    ("Queue", "accessPolicyIdentifier"),
                                                                                                    ("Table", "accessPolicyIdentifier"));


        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

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

                var namespaceTypeSymbolAndPolicyIdentifierNamePairs = new Dictionary<INamespaceSymbol, string>();

                foreach (var (nspace, policyIdentifierName) in NamespaceAndPolicyIdentifierNamePairs)
                {
                    var nspaceTypeSymbol = microsoftWindowsAzureStorageNamespaceSymbol
                                                .GetMembers(nspace)
                                                .OfType<INamespaceSymbol>()
                                                .FirstOrDefault();

                    if (nspaceTypeSymbol == null)
                    {
                        continue;
                    }

                    namespaceTypeSymbolAndPolicyIdentifierNamePairs.Add(nspaceTypeSymbol, policyIdentifierName);
                }

                if (!namespaceTypeSymbolAndPolicyIdentifierNamePairs.Any())
                {
                    return;
                }

                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartAnalysisContext.Compilation);

                compilationStartAnalysisContext.RegisterOperationBlockStartAction(operationBlockStartContext =>
                {
                    var owningSymbol = operationBlockStartContext.OwningSymbol;
                    if (owningSymbol.IsConfiguredToSkipAnalysis(operationBlockStartContext.Options,
                            Rule, operationBlockStartContext.Compilation, operationBlockStartContext.CancellationToken))
                    {
                        return;
                    }

                    operationBlockStartContext.RegisterOperationAction(operationAnalysisContext =>
                    {
                        var invocationOperation = (IInvocationOperation)operationAnalysisContext.Operation;
                        var methodSymbol = invocationOperation.TargetMethod;
                        var methodName = methodSymbol.Name;

                        if (methodName != "GetSharedAccessSignature")
                        {
                            return;
                        }

                        var namespaceSymbol = methodSymbol.ContainingNamespace;

                        if (namespaceSymbol == null)
                        {
                            return;
                        }

                        foreach (var (nspaceTypeSymbol, policyIdentifierName) in namespaceTypeSymbolAndPolicyIdentifierNamePairs)
                        {
                            if (namespaceSymbol.Equals(nspaceTypeSymbol))
                            {
                                var argumentOperation = invocationOperation.Arguments.FirstOrDefault(
                                                            s => s.Parameter.Name == policyIdentifierName &&
                                                            s.Parameter.Type.SpecialType == SpecialType.System_String);

                                if (argumentOperation != null)
                                {
                                    var cfg = invocationOperation.GetTopmostParentBlock()?.GetEnclosingControlFlowGraph();
                                    if (cfg == null)
                                    {
                                        return;
                                    }

                                    var interproceduralAnalysisConfig = InterproceduralAnalysisConfiguration.Create(
                                                                            operationBlockStartContext.Options,
                                                                            SupportedDiagnostics,
                                                                            defaultInterproceduralAnalysisKind: InterproceduralAnalysisKind.None,
                                                                            cancellationToken: operationBlockStartContext.CancellationToken,
                                                                            defaultMaxInterproceduralMethodCallChain: 1);
                                    var pointsToAnalysisResult = PointsToAnalysis.TryGetOrComputeResult(
                                                                    cfg,
                                                                    owningSymbol,
                                                                    operationBlockStartContext.Options,
                                                                    wellKnownTypeProvider,
                                                                    PointsToAnalysisKind.Complete,
                                                                    interproceduralAnalysisConfig,
                                                                    interproceduralAnalysisPredicateOpt: null,
                                                                    false);
                                    if (pointsToAnalysisResult == null)
                                    {
                                        return;
                                    }

                                    var pointsToAbstractValue = pointsToAnalysisResult[argumentOperation.Kind, argumentOperation.Syntax];

                                    if (pointsToAbstractValue.NullState != NullAbstractValue.Null)
                                    {
                                        return;
                                    }
                                }

                                operationAnalysisContext.ReportDiagnostic(
                                            invocationOperation.CreateDiagnostic(
                                                Rule));
                            }
                        }
                    }, OperationKind.Invocation);
                });
            });
        }
    }
}
