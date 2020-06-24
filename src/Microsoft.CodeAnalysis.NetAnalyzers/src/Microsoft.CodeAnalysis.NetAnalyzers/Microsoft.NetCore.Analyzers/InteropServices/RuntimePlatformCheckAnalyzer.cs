// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
#pragma warning disable RS1001 // Missing diagnostic analyzer attribute - TODO: fix and enable analyzer.
    public sealed partial class RuntimePlatformCheckAnalyzer : DiagnosticAnalyzer
#pragma warning restore RS1001 // Missing diagnostic analyzer attribute.
    {
        internal const string RuleId = "CA1416";
        private static readonly ImmutableArray<string> s_platformCheckMethods = ImmutableArray.Create("IsOSPlatformOrLater", "IsOSPlatformEarlierThan");

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.RuntimePlatformCheckTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.RuntimePlatformCheckMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.RuntimePlatformCheckMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableMessage,
                                                                                      DiagnosticCategory.Interoperability,
                                                                                      RuleLevel.IdeSuggestion,
                                                                                      description: s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);
        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                // TODO: Remove the below temporary hack once new APIs are available.
                var typeName = WellKnownTypeNames.SystemRuntimeInteropServicesRuntimeInformation + "Helper";

                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(typeName, out var runtimeInformationType) ||
                    !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesOSPlatform, out var osPlatformType))
                {
                    return;
                }

                var platformCheckMethods = GetPlatformCheckMethods(runtimeInformationType, osPlatformType);
                if (platformCheckMethods.IsEmpty)
                {
                    return;
                }

                context.RegisterOperationBlockStartAction(context => AnalyzerOperationBlock(context, platformCheckMethods, osPlatformType));
                return;

                static ImmutableArray<IMethodSymbol> GetPlatformCheckMethods(INamedTypeSymbol runtimeInformationType, INamedTypeSymbol osPlatformType)
                {
                    using var builder = ArrayBuilder<IMethodSymbol>.GetInstance();
                    var methods = runtimeInformationType.GetMembers().OfType<IMethodSymbol>();
                    foreach (var method in methods)
                    {
                        if (s_platformCheckMethods.Contains(method.Name) &&
                            method.Parameters.Length >= 1 &&
                            method.Parameters[0].Type.Equals(osPlatformType) &&
                            method.Parameters.Skip(1).All(p => p.Type.SpecialType == SpecialType.System_Int32))
                        {
                            builder.Add(method);
                        }
                    }

                    return builder.ToImmutable();
                }
            });
        }

        private static void AnalyzerOperationBlock(
            OperationBlockStartAnalysisContext context,
            ImmutableArray<IMethodSymbol> platformCheckMethods,
            INamedTypeSymbol osPlatformType)
        {
#pragma warning disable CA2000 // Dispose objects before losing scope - disposed in OperationBlockEndAction.
            var platformSpecificOperations = PooledConcurrentSet<IInvocationOperation>.GetInstance();
#pragma warning restore CA2000 // Dispose objects before losing scope
            var needsValueContentAnalysis = false;

            context.RegisterOperationAction(context =>
            {
                var invocation = (IInvocationOperation)context.Operation;
                if (platformCheckMethods.Contains(invocation.TargetMethod))
                {
                    needsValueContentAnalysis = needsValueContentAnalysis || ComputeNeedsValueContentAnalysis(invocation);
                }
                else
                {
                    // TODO: Add real platform specific operations that need runtime OS platform validation.
                    platformSpecificOperations.Add(invocation);
                }
            }, OperationKind.Invocation);

            context.RegisterOperationBlockEndAction(context =>
            {
                try
                {
                    if (platformSpecificOperations.IsEmpty ||
                        !(context.OperationBlocks.GetControlFlowGraph() is { } cfg))
                    {
                        return;
                    }

                    var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);
                    var analysisResult = GlobalFlowStateAnalysis.TryGetOrComputeResult(
                        cfg, context.OwningSymbol, CreateOperationVisitor,
                        wellKnownTypeProvider, context.Options, Rule, performPointsToAnalysis: needsValueContentAnalysis,
                        performValueContentAnalysis: needsValueContentAnalysis, context.CancellationToken,
                        out var pointsToAnalysisResult, out var valueContentAnalysisResult);
                    if (analysisResult == null)
                    {
                        return;
                    }

                    Debug.Assert(valueContentAnalysisResult == null || needsValueContentAnalysis);
                    Debug.Assert(pointsToAnalysisResult == null || needsValueContentAnalysis);

                    foreach (var platformSpecificOperation in platformSpecificOperations)
                    {
                        var value = analysisResult[platformSpecificOperation.Kind, platformSpecificOperation.Syntax];
                        if (value.Kind == GlobalFlowStateAnalysisValueSetKind.Unknown)
                        {
                            continue;
                        }

                        // TODO: Add real checks.

                        // TODO Platform checks:'{0}'
                        context.ReportDiagnostic(platformSpecificOperation.CreateDiagnostic(Rule, value));
                    }
                }
                finally
                {
                    platformSpecificOperations.Free();
                }

                return;

                OperationVisitor CreateOperationVisitor(GlobalFlowStateAnalysisContext context)
                    => new OperationVisitor(platformCheckMethods, osPlatformType, context);
            });
        }

        private static bool ComputeNeedsValueContentAnalysis(IInvocationOperation invocation)
        {
            Debug.Assert(invocation.Arguments.Length > 0);
            foreach (var argument in invocation.Arguments.Skip(1))
            {
                if (!argument.Value.ConstantValue.HasValue)
                {
                    return true;
                }
            }

            return false;
        }
    }
}