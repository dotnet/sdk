// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Text;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.FlightEnabledAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
#pragma warning disable RS1001 // Missing diagnostic analyzer attribute - TODO: fix and enable analyzer.
    public sealed class RuntimePlatformCheckAnalyzer : DiagnosticAnalyzer
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
                    var analysisResult = FlightEnabledAnalysis.TryGetOrComputeResult(
                        cfg, context.OwningSymbol, platformCheckMethods, c => GetValueForFlightEnablingMethodInvocation(c, osPlatformType),
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
                        if (value.Kind == FlightEnabledAbstractValueKind.Unknown)
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

                // local functions
                static FlightEnabledAbstractValue GetValueForFlightEnablingMethodInvocation(FlightEnabledAnalysisCallbackContext context, INamedTypeSymbol osPlatformType)
                {
                    Debug.Assert(context.Arguments.Length > 0);

                    if (!TryDecodeOSPlatform(context.Arguments, osPlatformType, out var osPlatformProperty) ||
                        !TryDecodeOSVersion(context, out var osVersion))
                    {
                        // Bail out
                        return FlightEnabledAbstractValue.Unknown;
                    }

                    var enabledFlight = $"{context.InvokedMethod.Name};{osPlatformProperty.Name};{osVersion}";
                    return new FlightEnabledAbstractValue(enabledFlight);
                }
            });
        }

        private static bool TryDecodeOSPlatform(
            ImmutableArray<IArgumentOperation> arguments,
            INamedTypeSymbol osPlatformType,
            [NotNullWhen(returnValue: true)] out IPropertySymbol? osPlatformProperty)
        {
            Debug.Assert(!arguments.IsEmpty);
            return TryDecodeOSPlatform(arguments[0].Value, osPlatformType, out osPlatformProperty);
        }

        private static bool TryDecodeOSPlatform(
            IOperation argumentValue,
            INamedTypeSymbol osPlatformType,
            [NotNullWhen(returnValue: true)] out IPropertySymbol? osPlatformProperty)
        {
            if ((argumentValue is IPropertyReferenceOperation propertyReference) &&
                propertyReference.Property.ContainingType.Equals(osPlatformType))
            {
                osPlatformProperty = propertyReference.Property;
                return true;
            }

            osPlatformProperty = null;
            return false;
        }

        private static bool TryDecodeOSVersion(FlightEnabledAnalysisCallbackContext context, [NotNullWhen(returnValue: true)] out string? osVersion)
        {
            var builder = new StringBuilder();
            var first = true;
            foreach (var argument in context.Arguments.Skip(1))
            {
                if (!TryDecodeOSVersionPart(argument, context, out var osVersionPart))
                {
                    osVersion = null;
                    return false;
                }

                if (!first)
                {
                    builder.Append(".");
                }

                builder.Append(osVersionPart);
                first = false;
            }

            osVersion = builder.ToString();
            return osVersion.Length > 0;

            static bool TryDecodeOSVersionPart(IArgumentOperation argument, FlightEnabledAnalysisCallbackContext context, out int osVersionPart)
            {
                if (argument.Value.ConstantValue.HasValue &&
                    argument.Value.ConstantValue.Value is int versionPart)
                {
                    osVersionPart = versionPart;
                    return true;
                }

                if (context.ValueContentAnalysisResult != null)
                {
                    var valueContentValue = context.ValueContentAnalysisResult[argument.Value];
                    if (valueContentValue.IsLiteralState &&
                        valueContentValue.LiteralValues.Count == 1 &&
                        valueContentValue.LiteralValues.Single() is int part)
                    {
                        osVersionPart = part;
                        return true;
                    }
                }

                osVersionPart = default;
                return false;
            }
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