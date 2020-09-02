// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.GlobalFlowStateAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    /// <summary>
    /// CA1416: Analyzer that informs developers when they use platform-specific APIs from call sites where the API might not be available
    /// 
    /// It finds usage of platform-specific or unsupported APIs and diagnoses if the 
    /// API is guarded by platform check or if it is annotated with corresponding platform specific attribute.
    /// If using the platform-specific API is not safe it reports diagnostics.
    ///
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed partial class PlatformCompatabilityAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1416";
        private static readonly ImmutableArray<string> s_osPlatformAttributes = ImmutableArray.Create(SupportedOSPlatformAttribute, UnsupportedOSPlatformAttribute);

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PlatformCompatabilityCheckTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableSupportedOsMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityCheckSupportedOsMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableSupportedOsVersionMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PlatformCompatibilityCheckSupportedOsVersionMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableUnsupportedOsMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PlatformCompatabilityCheckUnsupportedOsMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableUnsupportedOsVersionMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PlatformCompatabilityCheckUnsupportedOsVersionMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.PlatformCompatabilityCheckDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        // We are adding the new attributes into older versions of .Net 5.0, so there could be multiple referenced assemblies each with their own 
        // version of internal attribute type which will cause ambiguity, to avoid that we are comparing the attributes by their name
        private const string SupportedOSPlatformAttribute = nameof(SupportedOSPlatformAttribute);
        private const string UnsupportedOSPlatformAttribute = nameof(UnsupportedOSPlatformAttribute);

        // Platform guard method name, prefix, suffix
        private const string IsOSPlatform = nameof(IsOSPlatform);
        private const string IsPrefix = "Is";
        private const string OptionalSuffix = "VersionAtLeast";

        internal static DiagnosticDescriptor SupportedOsVersionRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableSupportedOsVersionMessage,
                                                                                      DiagnosticCategory.Interoperability,
                                                                                      RuleLevel.BuildWarning,
                                                                                      description: s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);

        internal static DiagnosticDescriptor SupportedOsRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableSupportedOsMessage,
                                                                                      DiagnosticCategory.Interoperability,
                                                                                      RuleLevel.BuildWarning,
                                                                                      description: s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);

        internal static DiagnosticDescriptor UnsupportedOsRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableUnsupportedOsMessage,
                                                                                      DiagnosticCategory.Interoperability,
                                                                                      RuleLevel.BuildWarning,
                                                                                      description: s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);

        internal static DiagnosticDescriptor UnsupportedOsVersionRule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableUnsupportedOsVersionMessage,
                                                                                      DiagnosticCategory.Interoperability,
                                                                                      RuleLevel.BuildWarning,
                                                                                      description: s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
            SupportedOsRule, SupportedOsVersionRule, UnsupportedOsRule, UnsupportedOsVersionRule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(context =>
            {
                var typeName = WellKnownTypeNames.SystemOperatingSystem;

                // TODO: remove 'typeName + "Helper"' after tests able to consume the real new APIs
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(typeName + "Helper", out var operatingSystemType))
                {
                    if (!context.Compilation.TryGetOrCreateTypeByMetadataName(typeName, out operatingSystemType))
                    {
                        return;
                    }
                }
                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesOSPlatform, out var osPlatformType) ||
                    !context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesRuntimeInformation, out var runtimeInformationType))
                {
                    return;
                }

                var stringType = context.Compilation.GetSpecialType(SpecialType.System_String);
                if (stringType == null)
                {
                    return;
                }

                var msBuildPlatforms = GetSupportedPlatforms(context.Options, context.Compilation, context.CancellationToken);
                var runtimeIsOSPlatformMethod = runtimeInformationType.GetMembers().OfType<IMethodSymbol>().Where(m =>
                    IsOSPlatform == m.Name &&
                    m.IsStatic &&
                    m.ReturnType.SpecialType == SpecialType.System_Boolean &&
                    m.Parameters.Length == 1 &&
                    m.Parameters[0].Type.Equals(osPlatformType)).FirstOrDefault();

                var guardMethods = GetOperatingSystemGuardMethods(runtimeIsOSPlatformMethod, operatingSystemType!);
                var platformSpecificMembers = new ConcurrentDictionary<ISymbol, SmallDictionary<string, PlatformAttributes>?>();
                var osPlatformTypeArray = ImmutableArray.Create(osPlatformType);
                var osPlatformCreateMethod = osPlatformType.GetMembers("Create").OfType<IMethodSymbol>().Where(m =>
                    m.IsStatic &&
                    m.ReturnType.Equals(osPlatformType) &&
                    m.Parameters.Length == 1 &&
                    m.Parameters[0].Type.SpecialType == SpecialType.System_String).FirstOrDefault();

                context.RegisterOperationBlockStartAction(
                    context => AnalyzeOperationBlock(context, guardMethods, runtimeIsOSPlatformMethod, osPlatformCreateMethod,
                                    osPlatformTypeArray, stringType, platformSpecificMembers, msBuildPlatforms));
            });

            static ImmutableArray<IMethodSymbol> GetOperatingSystemGuardMethods(IMethodSymbol? runtimeIsOSPlatformMethod, INamedTypeSymbol operatingSystemType)
            {
                var methods = operatingSystemType.GetMembers().OfType<IMethodSymbol>().Where(m =>
                    m.IsStatic &&
                    m.ReturnType.SpecialType == SpecialType.System_Boolean &&
                    (IsOSPlatform == m.Name) || NameAndParametersValid(m)).
                    ToImmutableArray();

                if (runtimeIsOSPlatformMethod != null)
                {
                    return methods.Add(runtimeIsOSPlatformMethod);
                }
                return methods;
            }

            static ImmutableArray<string> GetSupportedPlatforms(AnalyzerOptions options, Compilation compilation, CancellationToken cancellationToken) =>
                options.GetMSBuildItemMetadataValues(MSBuildItemOptionNames.SupportedPlatform, compilation, cancellationToken);

            static bool NameAndParametersValid(IMethodSymbol method) => method.Name.StartsWith(IsPrefix, StringComparison.Ordinal) &&
                    (method.Parameters.Length == 0 || method.Name.EndsWith(OptionalSuffix, StringComparison.Ordinal));
        }

        private void AnalyzeOperationBlock(
            OperationBlockStartAnalysisContext context,
            ImmutableArray<IMethodSymbol> guardMethods,
            IMethodSymbol? runtimeIsOSPlatformMethod,
            IMethodSymbol? osPlatformCreateMethod,
            ImmutableArray<INamedTypeSymbol> osPlatformTypeArray,
            INamedTypeSymbol stringType,
            ConcurrentDictionary<ISymbol, SmallDictionary<string, PlatformAttributes>?> platformSpecificMembers,
            ImmutableArray<string> msBuildPlatforms)
        {
            var osPlatformType = osPlatformTypeArray[0];
            var platformSpecificOperations = PooledConcurrentDictionary<IOperation, SmallDictionary<string, PlatformAttributes>>.GetInstance();

            context.RegisterOperationAction(context =>
            {
                AnalyzeOperation(context.Operation, context, platformSpecificOperations, platformSpecificMembers, msBuildPlatforms);
            },
            OperationKind.MethodReference,
            OperationKind.EventReference,
            OperationKind.FieldReference,
            OperationKind.Invocation,
            OperationKind.ObjectCreation,
            OperationKind.PropertyReference);

            context.RegisterOperationBlockEndAction(context =>
            {
                try
                {
                    if (platformSpecificOperations.IsEmpty)
                    {
                        return;
                    }

                    if (guardMethods.IsEmpty || !(context.OperationBlocks.GetControlFlowGraph() is { } cfg))
                    {
                        ReportDiagnosticsForAll(platformSpecificOperations, context);
                        return;
                    }

                    var performValueContentAnalysis = ComputeNeedsValueContentAnalysis(cfg.OriginalOperation, guardMethods, runtimeIsOSPlatformMethod, osPlatformType);
                    var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);
                    var analysisResult = GlobalFlowStateAnalysis.TryGetOrComputeResult(
                        cfg, context.OwningSymbol, CreateOperationVisitor, wellKnownTypeProvider,
                        context.Options, SupportedOsRule, performValueContentAnalysis,
                        context.CancellationToken, out var valueContentAnalysisResult,
                        additionalSupportedValueTypes: osPlatformTypeArray,
                        getValueContentValueForAdditionalSupportedValueTypeOperation: GetValueContentValue);

                    if (analysisResult == null)
                    {
                        return;
                    }

                    foreach (var (platformSpecificOperation, attributes) in platformSpecificOperations)
                    {
                        var value = analysisResult[platformSpecificOperation.Kind, platformSpecificOperation.Syntax];

                        if ((value.Kind == GlobalFlowStateAnalysisValueSetKind.Known && IsKnownValueGuarded(attributes, value)) ||
                           (value.Kind == GlobalFlowStateAnalysisValueSetKind.Unknown && HasGuardedLambdaOrLocalFunctionResult(platformSpecificOperation, attributes, analysisResult)))
                        {
                            continue;
                        }

                        ReportDiagnostics(platformSpecificOperation, attributes, context);
                    }
                }
                finally
                {
                    // Workaround for https://github.com/dotnet/roslyn/issues/46859
                    // Do not free in presence of cancellation.
                    if (!context.CancellationToken.IsCancellationRequested)
                    {
                        platformSpecificOperations.Free(context.CancellationToken);
                    }
                }

                return;

                OperationVisitor CreateOperationVisitor(GlobalFlowStateAnalysisContext context) => new OperationVisitor(guardMethods, osPlatformTypeArray[0], context);

                ValueContentAbstractValue GetValueContentValue(IOperation operation)
                {
                    Debug.Assert(operation.Type.Equals(osPlatformType));
                    if (operation is IInvocationOperation invocation &&
                        invocation.TargetMethod.Equals(osPlatformCreateMethod) &&
                        invocation.Arguments.Length == 1 &&
                        invocation.Arguments[0].Value is { } argument &&
                        argument.ConstantValue.HasValue &&
                        argument.ConstantValue.Value is string platformName &&
                        platformName.Length > 0)
                    {
                        return ValueContentAbstractValue.Create(platformName, stringType);
                    }

                    return ValueContentAbstractValue.MayBeContainsNonLiteralState;
                }
            });
        }

        private static bool HasGuardedLambdaOrLocalFunctionResult(IOperation platformSpecificOperation, SmallDictionary<string, PlatformAttributes> attributes,
            DataFlowAnalysisResult<GlobalFlowStateBlockAnalysisResult, GlobalFlowStateAnalysisValueSet> analysisResult)
        {
            if (!platformSpecificOperation.IsWithinLambdaOrLocalFunction(out var containingLambdaOrLocalFunctionOperation))
            {
                return false;
            }

            var results = analysisResult.TryGetLambdaOrLocalFunctionResults(containingLambdaOrLocalFunctionOperation);
            Debug.Assert(results.Any(), "Expected at least one analysis result for lambda/local function");

            foreach (var localResult in results)
            {
                Debug.Assert(localResult.ControlFlowGraph.OriginalOperation == containingLambdaOrLocalFunctionOperation);

                var localValue = localResult[platformSpecificOperation.Kind, platformSpecificOperation.Syntax];

                // Value must be known and guarded in all analysis contexts.
                // NOTE: IsKnownValueGuarded mutates the input values, so we pass in cloned values
                // to ensure that evaluation of each result is independent of evaluation of other parts.
                if (localValue.Kind != GlobalFlowStateAnalysisValueSetKind.Known ||
                    !IsKnownValueGuarded(CopyAttributes(attributes), localValue))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ComputeNeedsValueContentAnalysis(IOperation operationBlock, ImmutableArray<IMethodSymbol> guardMethods, IMethodSymbol? runtimeIsOSPlatformMethod, INamedTypeSymbol osPlatformType)
        {
            Debug.Assert(runtimeIsOSPlatformMethod == null || guardMethods.Contains(runtimeIsOSPlatformMethod));

            foreach (var operation in operationBlock.Descendants())
            {
                if (operation is IInvocationOperation invocation)
                {
                    if (invocation.TargetMethod.Equals(runtimeIsOSPlatformMethod))
                    {
                        if (invocation.Arguments.Length == 1 &&
                            invocation.Arguments[0].Value is IPropertyReferenceOperation propertyReference &&
                            propertyReference.Property.ContainingType.Equals(osPlatformType))
                        {
                            // "OSPlatform.Platform" property reference does not need value content analysis.
                            continue;
                        }

                        return true;
                    }
                    else if (guardMethods.Contains(invocation.TargetMethod))
                    {
                        // Check if any integral parameter to guard method invocation has non-constant value.
                        foreach (var argument in invocation.Arguments)
                        {
                            if (argument.Parameter.Type.SpecialType == SpecialType.System_Int32 &&
                                !argument.Value.ConstantValue.HasValue)
                            {
                                return true;
                            }
                        }
                    }
                }
            }

            return false;
        }

        private static bool IsKnownValueGuarded(SmallDictionary<string, PlatformAttributes> attributes, GlobalFlowStateAnalysisValueSet value)
        {
            using var capturedVersions = PooledDictionary<string, Version>.GetInstance(StringComparer.OrdinalIgnoreCase);
            return IsKnownValueGuarded(attributes, value, capturedVersions);

            static bool IsKnownValueGuarded(
                SmallDictionary<string, PlatformAttributes> attributes,
                GlobalFlowStateAnalysisValueSet value,
                PooledDictionary<string, Version> capturedVersions)
            {
                // 'GlobalFlowStateAnalysisValueSet.AnalysisValues' represent the && of values.
                foreach (var analysisValue in value.AnalysisValues)
                {
                    if (analysisValue is PlatformMethodValue info)
                    {
                        if (attributes.TryGetValue(info.PlatformName, out var attribute))
                        {
                            if (info.Negated)
                            {
                                if (attribute.UnsupportedFirst != null)
                                {
                                    if (attribute.UnsupportedFirst >= info.Version)
                                    {
                                        if (DenyList(attribute))
                                        {
                                            attribute.SupportedFirst = null;
                                            attribute.SupportedSecond = null;
                                            attribute.UnsupportedSecond = null;
                                        }
                                        attribute.UnsupportedFirst = null;
                                    }
                                }

                                if (attribute.UnsupportedSecond != null)
                                {
                                    if (attribute.UnsupportedSecond <= info.Version)
                                    {
                                        attribute.UnsupportedSecond = null;
                                    }
                                }

                                if (!IsEmptyVersion(info.Version))
                                {
                                    capturedVersions[info.PlatformName] = info.Version;
                                }
                            }
                            else
                            {
                                if (capturedVersions.Any())
                                {
                                    if (attribute.UnsupportedFirst != null &&
                                        capturedVersions.TryGetValue(info.PlatformName, out var version) &&
                                        attribute.UnsupportedFirst >= version)
                                    {
                                        attribute.UnsupportedFirst = null;
                                    }

                                    if (attribute.UnsupportedSecond != null &&
                                        capturedVersions.TryGetValue(info.PlatformName, out version) &&
                                        attribute.UnsupportedSecond <= version)
                                    {
                                        attribute.UnsupportedSecond = null;
                                    }
                                }

                                if (attribute.SupportedFirst != null &&
                                    attribute.SupportedFirst <= info.Version)
                                {
                                    attribute.SupportedFirst = null;
                                    RemoveUnsupportedWithLessVersion(info.Version, attribute);
                                    RemoveOtherSupportsOnDifferentPlatforms(attributes, info.PlatformName);
                                }

                                if (attribute.SupportedSecond != null &&
                                    attribute.SupportedSecond <= info.Version)
                                {
                                    attribute.SupportedSecond = null;
                                    RemoveUnsupportedWithLessVersion(info.Version, attribute);
                                    RemoveOtherSupportsOnDifferentPlatforms(attributes, info.PlatformName);
                                }

                                RemoveUnsupportsOnDifferentPlatforms(attributes, info.PlatformName);
                            }
                        }
                        else
                        {
                            if (!info.Negated)
                            {
                                // it is checking one exact platform, other unsupported should be suppressed
                                RemoveUnsupportsOnDifferentPlatforms(attributes, info.PlatformName);
                            }
                        }
                    }
                }

                if (value.Parents.IsEmpty)
                {
                    foreach (var attribute in attributes)
                    {
                        // if any of the attributes is not suppressed
                        if (attribute.Value.HasAttribute())
                        {
                            return false;
                        }
                    }
                }
                else
                {
                    // 'GlobalFlowStateAnalysisValueSet.Parents' represent || of values on different flow paths.
                    // We are guarded only if values are guarded on *all flow paths**.
                    foreach (var parent in value.Parents)
                    {
                        // NOTE: IsKnownValueGuarded mutates the input values, so we pass in cloned values
                        // to ensure that evaluation of each part of || is independent of evaluation of other parts.
                        var parentAttributes = CopyAttributes(attributes);
                        using var parentCapturedVersions = PooledDictionary<string, Version>.GetInstance(capturedVersions);

                        if (!IsKnownValueGuarded(parentAttributes, parent, parentCapturedVersions))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }

            static void RemoveUnsupportsOnDifferentPlatforms(SmallDictionary<string, PlatformAttributes> attributes, string platformName)
            {
                foreach (var (name, attribute) in attributes)
                {
                    if (!name.Equals(platformName, StringComparison.OrdinalIgnoreCase) &&
                        DenyList(attribute))
                    {
                        attribute.UnsupportedFirst = null;
                        attribute.UnsupportedSecond = null;
                        attribute.SupportedFirst = null;
                        attribute.SupportedSecond = null;
                    }
                }
            }

            static void RemoveUnsupportedWithLessVersion(Version supportedVersion, PlatformAttributes attribute)
            {
                if (attribute.UnsupportedFirst != null &&
                    attribute.UnsupportedFirst <= supportedVersion)
                {
                    attribute.UnsupportedFirst = null;
                }
            }

            static void RemoveOtherSupportsOnDifferentPlatforms(SmallDictionary<string, PlatformAttributes> attributes, string platformName)
            {
                foreach (var (name, attribute) in attributes)
                {
                    if (!name.Equals(platformName, StringComparison.OrdinalIgnoreCase))
                    {
                        attribute.SupportedFirst = null;
                        attribute.SupportedSecond = null;
                    }
                }
            }
        }

        private static bool IsEmptyVersion(Version version) => version.Major == 0 && version.Minor == 0;

        private static void ReportDiagnosticsForAll(PooledConcurrentDictionary<IOperation,
            SmallDictionary<string, PlatformAttributes>> platformSpecificOperations, OperationBlockAnalysisContext context)
        {
            foreach (var platformSpecificOperation in platformSpecificOperations)
            {
                ReportDiagnostics(platformSpecificOperation.Key, platformSpecificOperation.Value, context);
            }
        }

        private static void ReportDiagnostics(IOperation operation, SmallDictionary<string, PlatformAttributes> attributes, OperationBlockAnalysisContext context)
        {
            var symbol = operation is IObjectCreationOperation creation ? creation.Constructor.ContainingType : GetOperationSymbol(operation);

            if (symbol == null)
            {
                return;
            }

            var operationName = symbol.ToDisplayString(SymbolDisplayFormat.CSharpShortErrorMessageFormat);

            foreach (var platformName in attributes.Keys)
            {
                var attribute = attributes[platformName];

                if (attribute.SupportedSecond != null)
                {
                    ReportSupportedDiagnostic(operation, context, operationName, platformName, VersionToString(attribute.SupportedSecond));
                }
                else if (attribute.SupportedFirst != null)
                {
                    ReportSupportedDiagnostic(operation, context, operationName, platformName, VersionToString(attribute.SupportedFirst));
                }

                if (attribute.UnsupportedFirst != null)
                {
                    ReportUnsupportedDiagnostic(operation, context, operationName, platformName, VersionToString(attribute.UnsupportedFirst));
                }
                else if (attribute.UnsupportedSecond != null)
                {
                    ReportUnsupportedDiagnostic(operation, context, operationName, platformName, VersionToString(attribute.UnsupportedSecond));
                }
            }

            static void ReportSupportedDiagnostic(IOperation operation, OperationBlockAnalysisContext context, string name, string platformName, string? version = null) =>
            context.ReportDiagnostic(version == null ? operation.CreateDiagnostic(SupportedOsRule, name, platformName) :
                operation.CreateDiagnostic(SupportedOsVersionRule, name, platformName, version));

            static void ReportUnsupportedDiagnostic(IOperation operation, OperationBlockAnalysisContext context, string name, string platformName, string? version = null) =>
            context.ReportDiagnostic(version == null ? operation.CreateDiagnostic(UnsupportedOsRule, name, platformName) :
                operation.CreateDiagnostic(UnsupportedOsVersionRule, name, platformName, version));
        }

        private static string? VersionToString(Version version) => IsEmptyVersion(version) ? null : version.ToString();

        private static ISymbol? GetOperationSymbol(IOperation operation)
            => operation switch
            {
                IInvocationOperation iOperation => iOperation.TargetMethod,
                IObjectCreationOperation cOperation => cOperation.Constructor,
                IFieldReferenceOperation fOperation => IsWithinConditionalOperation(fOperation) ? null : fOperation.Field,
                IMemberReferenceOperation mOperation => mOperation.Member,
                _ => null,
            };

        private static void AnalyzeOperation(IOperation operation, OperationAnalysisContext context,
            PooledConcurrentDictionary<IOperation, SmallDictionary<string, PlatformAttributes>> platformSpecificOperations,
            ConcurrentDictionary<ISymbol, SmallDictionary<string, PlatformAttributes>?> platformSpecificMembers, ImmutableArray<string> msBuildPlatforms)
        {
            var symbol = GetOperationSymbol(operation);

            if (symbol == null)
            {
                return;
            }

            if (TryGetOrCreatePlatformAttributes(symbol, platformSpecificMembers, out var operationAttributes))
            {
                if (TryGetOrCreatePlatformAttributes(context.ContainingSymbol, platformSpecificMembers, out var callSiteAttributes))
                {
                    if (IsNotSuppressedByCallSite(operationAttributes, callSiteAttributes, msBuildPlatforms, out var notSuppressedAttributes))
                    {
                        platformSpecificOperations.TryAdd(operation, notSuppressedAttributes);
                    }
                }
                else
                {
                    if (TryCopyAttributesNotSuppressedByMsBuild(operationAttributes, msBuildPlatforms, out var copiedAttributes))
                    {
                        platformSpecificOperations.TryAdd(operation, copiedAttributes);
                    }
                }
            }
        }

        private static bool TryCopyAttributesNotSuppressedByMsBuild(SmallDictionary<string, PlatformAttributes> operationAttributes,
            ImmutableArray<string> msBuildPlatforms, out SmallDictionary<string, PlatformAttributes> copiedAttributes)
        {
            copiedAttributes = new SmallDictionary<string, PlatformAttributes>(StringComparer.OrdinalIgnoreCase);
            foreach (var (platformName, attributes) in operationAttributes)
            {
                if (AllowList(attributes) || msBuildPlatforms.IndexOf(platformName, 0, StringComparer.OrdinalIgnoreCase) != -1)
                {
                    copiedAttributes.Add(platformName, CopyAllAttributes(new PlatformAttributes(), attributes));
                }
            }

            return copiedAttributes.Any();
        }

        private static SmallDictionary<string, PlatformAttributes> CopyAttributes(SmallDictionary<string, PlatformAttributes> copyAttributes)
        {
            var copy = new SmallDictionary<string, PlatformAttributes>(StringComparer.OrdinalIgnoreCase);
            foreach (var (platformName, attributes) in copyAttributes)
            {
                copy.Add(platformName, CopyAllAttributes(new PlatformAttributes(), attributes));
            }

            return copy;
        }

        /// <summary>
        /// The semantics of the platform specific attributes are :
        ///    - An API that doesn't have any of these attributes is considered supported by all platforms.
        ///    - If either [SupportedOSPlatform] or [UnsupportedOSPlatform] attributes are present, we group all attributes by OS platform identifier:
        ///        - Allow list.If the lowest version for each OS platform is a [SupportedOSPlatform] attribute, the API is considered to only be supported by the listed platforms and unsupported by all other platforms.
        ///        - Deny list. If the lowest version for each OS platform is a [UnsupportedOSPlatform] attribute, then the API is considered to only be unsupported by the listed platforms and supported by all other platforms.
        ///        - Inconsistent list. If for some platforms the lowest version attribute is [SupportedOSPlatform] while for others it is [UnsupportedOSPlatform], the analyzer will produce a warning on the API definition because the API is attributed inconsistently.
        ///    - Both attributes can be instantiated without version numbers. This means the version number is assumed to be 0.0. This simplifies guard clauses, see examples below for more details.
        /// </summary>
        /// <param name="operationAttributes">Platform specific attributes applied to the invoked member</param>
        /// <param name="callSiteAttributes">Platform specific attributes applied to the call site where the member invoked</param>
        /// <returns>true if all attributes applied to the operation is suppressed, false otherwise</returns>

        private static bool IsNotSuppressedByCallSite(SmallDictionary<string, PlatformAttributes> operationAttributes,
            SmallDictionary<string, PlatformAttributes> callSiteAttributes, ImmutableArray<string> msBuildPlatforms,
            out SmallDictionary<string, PlatformAttributes> notSuppressedAttributes)
        {
            notSuppressedAttributes = new SmallDictionary<string, PlatformAttributes>(StringComparer.OrdinalIgnoreCase);
            bool? supportedOnlyList = null;
            bool mandatoryMatchFound = false;
            using var supportedOnlyPlatforms = PooledHashSet<string>.GetInstance(StringComparer.OrdinalIgnoreCase);
            foreach (var (platformName, attribute) in operationAttributes)
            {
                var diagnosticAttribute = new PlatformAttributes();

                if (attribute.SupportedFirst != null)
                {
                    if (attribute.UnsupportedFirst == null || attribute.UnsupportedFirst > attribute.SupportedFirst)
                    {
                        // If only supported for current platform
                        if (supportedOnlyList.HasValue && !supportedOnlyList.Value)
                        {
                            return true; // invalid state, do not need to add this API to the list
                        }

                        supportedOnlyPlatforms.Add(platformName);
                        supportedOnlyList = true;

                        if (callSiteAttributes.TryGetValue(platformName, out var callSiteAttribute))
                        {
                            var attributeToCheck = attribute.SupportedSecond ?? attribute.SupportedFirst;
                            if (MandatoryOsVersionsSuppressed(callSiteAttribute, attributeToCheck))
                            {
                                mandatoryMatchFound = true;
                            }
                            else
                            {
                                diagnosticAttribute.SupportedSecond = (Version)attributeToCheck.Clone();
                            }

                            if (attribute.UnsupportedFirst != null &&
                                !(SuppressedByCallSiteSupported(attribute, callSiteAttribute.SupportedFirst) ||
                                  SuppressedByCallSiteUnsupported(callSiteAttribute, attribute.UnsupportedFirst)))
                            {
                                diagnosticAttribute.UnsupportedFirst = (Version)attribute.UnsupportedFirst.Clone();
                            }
                        }
                    }
                    else if (attribute.UnsupportedFirst != null) // also means Unsupported <= Supported, optional list
                    {
                        if (supportedOnlyList.HasValue && supportedOnlyList.Value)
                        {
                            return true; // do not need to add this API to the list
                        }

                        supportedOnlyList = false;

                        if (callSiteAttributes.TryGetValue(platformName, out var callSiteAttribute))
                        {
                            if (callSiteAttribute.SupportedFirst != null)
                            {
                                if (!OptionalOsSupportSuppressed(callSiteAttribute, attribute))
                                {
                                    diagnosticAttribute.SupportedFirst = (Version)attribute.SupportedFirst.Clone();
                                }

                                if (!UnsupportedFirstSuppressed(attribute, callSiteAttribute))
                                {
                                    diagnosticAttribute.UnsupportedFirst = (Version)attribute.UnsupportedFirst.Clone();
                                }

                                if (attribute.UnsupportedSecond != null &&
                                    !UnsupportedSecondSuppressed(attribute, callSiteAttribute))
                                {
                                    diagnosticAttribute.UnsupportedSecond = (Version)attribute.UnsupportedSecond.Clone();
                                }
                            }
                        }
                        else
                        {
                            // Call site has no attributes for this platform, check if MsBuild list has it, 
                            // then if call site has deny list, it should support its later support
                            if (msBuildPlatforms.Contains(platformName) &&
                                callSiteAttributes.Any(ca => DenyList(ca.Value)))
                            {
                                diagnosticAttribute.SupportedFirst = (Version)attribute.SupportedFirst.Clone();
                            }
                        }
                    }
                }
                else
                {
                    if (supportedOnlyList.HasValue && supportedOnlyList.Value)
                    {
                        return true; // do not need to add this API to the list
                    }

                    supportedOnlyList = false;

                    if (attribute.UnsupportedFirst != null) // Unsupported for this but supported all other
                    {
                        if (callSiteAttributes.TryGetValue(platformName, out var callSiteAttribute))
                        {
                            if (callSiteAttribute.SupportedFirst != null)
                            {
                                if (!SuppressedByCallSiteUnsupported(callSiteAttribute, attribute.UnsupportedFirst))
                                {
                                    diagnosticAttribute.UnsupportedFirst = (Version)attribute.UnsupportedFirst.Clone();
                                }

                                if (attribute.UnsupportedSecond != null &&
                                    !SuppressedByCallSiteUnsupported(callSiteAttribute, attribute.UnsupportedSecond))
                                {
                                    diagnosticAttribute.UnsupportedSecond = (Version)attribute.UnsupportedSecond.Clone();
                                }
                            }
                        }
                        else if (msBuildPlatforms.Contains(platformName) &&
                            !callSiteAttributes.Values.Any(v => v.SupportedFirst != null))
                        {
                            // if MsBuild list contain the platform and call site has no any other supported attribute it means global, so need to warn
                            diagnosticAttribute.UnsupportedFirst = (Version)attribute.UnsupportedFirst.Clone();
                        }
                    }
                }

                if (diagnosticAttribute.HasAttribute())
                {
                    notSuppressedAttributes[platformName] = diagnosticAttribute;
                }
            }

            if (supportedOnlyList.HasValue && supportedOnlyList.Value)
            {
                if (!mandatoryMatchFound)
                {
                    foreach (var (name, attributes) in operationAttributes)
                    {
                        if (attributes.SupportedFirst != null)
                        {
                            if (!notSuppressedAttributes.TryGetValue(name, out var diagnosticAttribute))
                            {
                                diagnosticAttribute = new PlatformAttributes();
                            }
                            CopyAllAttributes(diagnosticAttribute, attributes);
                            notSuppressedAttributes[name] = diagnosticAttribute;
                        }
                    }
                }

                // if supportedOnlyList then call site should not have any platform not listed in the support list
                foreach (var (platform, csAttributes) in callSiteAttributes)
                {
                    if (csAttributes.SupportedFirst != null &&
                        !supportedOnlyPlatforms.Contains(platform))
                    {
                        foreach (var (name, version) in operationAttributes)
                        {
                            AddOrUpdatedDiagnostic(operationAttributes[name], notSuppressedAttributes, name);
                        }
                    }
                }
            }
            return notSuppressedAttributes.Any();

            static void AddOrUpdatedDiagnostic(PlatformAttributes operationAttributes,
                SmallDictionary<string, PlatformAttributes> notSuppressedAttributes, string name)
            {
                if (operationAttributes.SupportedFirst != null)
                {
                    if (!notSuppressedAttributes.TryGetValue(name, out var diagnosticAttribute))
                    {
                        diagnosticAttribute = new PlatformAttributes();
                    }
                    diagnosticAttribute.SupportedFirst = (Version)operationAttributes.SupportedFirst.Clone();
                    notSuppressedAttributes[name] = diagnosticAttribute;
                }
            }

            static bool UnsupportedSecondSuppressed(PlatformAttributes attribute, PlatformAttributes callSiteAttribute) =>
                SuppressedByCallSiteSupported(attribute, callSiteAttribute.SupportedFirst) ||
                SuppressedByCallSiteUnsupported(callSiteAttribute, attribute.UnsupportedSecond!);

            static bool SuppressedByCallSiteUnsupported(PlatformAttributes callSiteAttribute, Version unsupporteAttribute) =>
                callSiteAttribute.UnsupportedFirst != null && unsupporteAttribute >= callSiteAttribute.UnsupportedFirst ||
                callSiteAttribute.UnsupportedSecond != null && unsupporteAttribute >= callSiteAttribute.UnsupportedSecond;

            static bool SuppressedByCallSiteSupported(PlatformAttributes attribute, Version? callSiteSupportedFirst) =>
                callSiteSupportedFirst != null && callSiteSupportedFirst >= attribute.SupportedFirst! &&
                attribute.SupportedSecond != null && callSiteSupportedFirst >= attribute.SupportedSecond;

            static bool UnsupportedFirstSuppressed(PlatformAttributes attribute, PlatformAttributes callSiteAttribute) =>
                callSiteAttribute.SupportedFirst != null && callSiteAttribute.SupportedFirst >= attribute.SupportedFirst ||
                SuppressedByCallSiteUnsupported(callSiteAttribute, attribute.UnsupportedFirst!);

            // As optianal if call site supports that platform, their versions should match
            static bool OptionalOsSupportSuppressed(PlatformAttributes callSiteAttribute, PlatformAttributes attribute) =>
                (callSiteAttribute.SupportedFirst == null || attribute.SupportedFirst <= callSiteAttribute.SupportedFirst) &&
                (callSiteAttribute.SupportedSecond == null || attribute.SupportedFirst <= callSiteAttribute.SupportedSecond);

            static bool MandatoryOsVersionsSuppressed(PlatformAttributes callSitePlatforms, Version checkingVersion) =>
                callSitePlatforms.SupportedFirst != null && checkingVersion <= callSitePlatforms.SupportedFirst ||
                callSitePlatforms.SupportedSecond != null && checkingVersion <= callSitePlatforms.SupportedSecond;
        }

        private static PlatformAttributes CopyAllAttributes(PlatformAttributes copyTo, PlatformAttributes copyFrom)
        {
            copyTo.SupportedFirst = (Version?)copyFrom.SupportedFirst?.Clone();
            copyTo.SupportedSecond = (Version?)copyFrom.SupportedSecond?.Clone();
            copyTo.UnsupportedFirst = (Version?)copyFrom.UnsupportedFirst?.Clone();
            copyTo.UnsupportedSecond = (Version?)copyFrom.UnsupportedSecond?.Clone();
            return copyTo;
        }

        // Do not warn if platform specific enum/field value is used in conditional check, like: 'if (value == FooEnum.WindowsOnlyValue)'
        private static bool IsWithinConditionalOperation(IFieldReferenceOperation pOperation) =>
            pOperation.ConstantValue.HasValue &&
            pOperation.Parent is IBinaryOperation bo &&
            (bo.OperatorKind == BinaryOperatorKind.Equals ||
            bo.OperatorKind == BinaryOperatorKind.NotEquals ||
            bo.OperatorKind == BinaryOperatorKind.GreaterThan ||
            bo.OperatorKind == BinaryOperatorKind.LessThan ||
            bo.OperatorKind == BinaryOperatorKind.GreaterThanOrEqual ||
            bo.OperatorKind == BinaryOperatorKind.LessThanOrEqual);

        private static bool TryGetOrCreatePlatformAttributes(
            ISymbol symbol,
            ConcurrentDictionary<ISymbol, SmallDictionary<string, PlatformAttributes>?> platformSpecificMembers,
            [NotNullWhen(true)] out SmallDictionary<string, PlatformAttributes>? attributes)
        {
            if (!platformSpecificMembers.TryGetValue(symbol, out attributes))
            {
                var container = symbol.ContainingSymbol;

                // Namespaces do not have attributes
                while (container is INamespaceSymbol)
                {
                    container = container.ContainingSymbol;
                }

                if (container != null &&
                    TryGetOrCreatePlatformAttributes(container, platformSpecificMembers, out var containerAttributes))
                {
                    attributes = CopyAttributes(containerAttributes);
                }

                AddPlatformAttributes(symbol.GetAttributes(), ref attributes);

                attributes = platformSpecificMembers.GetOrAdd(symbol, attributes);
            }

            return attributes != null;

            static bool AddPlatformAttributes(ImmutableArray<AttributeData> immediateAttributes, [NotNullWhen(true)] ref SmallDictionary<string, PlatformAttributes>? attributes)
            {
                foreach (AttributeData attribute in immediateAttributes)
                {
                    if (s_osPlatformAttributes.Contains(attribute.AttributeClass.Name))
                    {
                        TryAddValidAttribute(ref attributes, attribute);
                    }
                }
                return attributes != null;
            }
        }

        private static bool TryAddValidAttribute([NotNullWhen(true)] ref SmallDictionary<string, PlatformAttributes>? attributes, AttributeData attribute)
        {
            if (!attribute.ConstructorArguments.IsEmpty &&
                                attribute.ConstructorArguments[0] is { } argument &&
                                argument.Kind == TypedConstantKind.Primitive &&
                                argument.Type.SpecialType == SpecialType.System_String &&
                                !argument.IsNull &&
                                !argument.Value.Equals(string.Empty) &&
                                TryParsePlatformNameAndVersion(argument.Value.ToString(), out string platformName, out Version? version))
            {
                attributes ??= new SmallDictionary<string, PlatformAttributes>(StringComparer.OrdinalIgnoreCase);

                if (!attributes.TryGetValue(platformName, out var _))
                {
                    attributes[platformName] = new PlatformAttributes();
                }

                AddAttribute(attribute.AttributeClass.Name, version, attributes, platformName);
                return true;
            }

            return false;
        }

        private static bool TryParsePlatformNameAndVersion(string osString, out string osPlatformName, [NotNullWhen(true)] out Version? version)
        {
            version = null;
            osPlatformName = string.Empty;
            for (int i = 0; i < osString.Length; i++)
            {
                if (char.IsDigit(osString[i]))
                {
                    if (i > 0 && Version.TryParse(osString[i..], out Version? parsedVersion))
                    {
                        osPlatformName = osString.Substring(0, i);
                        version = parsedVersion;
                        return true;
                    }

                    return false;
                }
            }

            osPlatformName = osString;
            version = new Version(0, 0);
            return true;
        }

        private static void AddAttribute(string name, Version version, SmallDictionary<string, PlatformAttributes> existingAttributes, string platformName)
        {
            if (name == SupportedOSPlatformAttribute)
            {
                AddOrUpdateSupportedAttribute(existingAttributes[platformName], version);
            }
            else
            {
                Debug.Assert(name == UnsupportedOSPlatformAttribute);
                AddOrUpdateUnsupportedAttribute(platformName, existingAttributes[platformName], version, existingAttributes);
            }

            static void AddOrUpdateUnsupportedAttribute(string name, PlatformAttributes attributes,
                Version version, SmallDictionary<string, PlatformAttributes> existingAttributes)
            {
                if (attributes.UnsupportedFirst != null)
                {
                    if (attributes.UnsupportedFirst > version)
                    {
                        if (attributes.UnsupportedSecond != null)
                        {
                            if (attributes.UnsupportedSecond > attributes.UnsupportedFirst)
                            {
                                attributes.UnsupportedSecond = attributes.UnsupportedFirst;
                            }
                        }
                        else
                        {
                            attributes.UnsupportedSecond = attributes.UnsupportedFirst;
                        }

                        attributes.UnsupportedFirst = version;
                    }
                    else
                    {
                        if (attributes.UnsupportedSecond != null)
                        {
                            if (attributes.UnsupportedSecond > version)
                            {
                                attributes.UnsupportedSecond = version;
                            }
                        }
                        else
                        {
                            if (attributes.SupportedFirst != null && attributes.SupportedFirst < version)
                            {
                                // We should ignore second attribute in case like [UnsupportedOSPlatform(""windows""), 
                                // [UnsupportedOSPlatform(""windows11.0"")] which doesn't have supported in between
                                attributes.UnsupportedSecond = version;
                            }
                        }
                    }
                }
                else
                {
                    if (attributes.SupportedFirst != null && attributes.SupportedFirst >= version)
                    {
                        // Override needed
                        if (attributes.SupportedSecond != null)
                        {
                            attributes.SupportedFirst = attributes.SupportedSecond;
                            attributes.SupportedSecond = null;
                        }
                        else
                        {
                            attributes.SupportedFirst = null;
                        }
                        if (!HasAnySupportedOnlyAttribute(name, existingAttributes))
                        {
                            attributes.UnsupportedFirst = version;
                        }
                    }
                    else
                    {
                        attributes.UnsupportedFirst = version;
                    }
                }

                static bool HasAnySupportedOnlyAttribute(string name, SmallDictionary<string, PlatformAttributes> existingAttributes) =>
                    existingAttributes.Any(a => !a.Key.Equals(name, StringComparison.OrdinalIgnoreCase) &&
                    AllowList(a.Value));
            }

            static void AddOrUpdateSupportedAttribute(PlatformAttributes attributes, Version version)
            {
                if (attributes.SupportedFirst != null)
                {
                    if (attributes.SupportedFirst > version)
                    {
                        if (attributes.SupportedSecond != null)
                        {
                            if (attributes.SupportedSecond < attributes.SupportedFirst)
                            {
                                attributes.SupportedSecond = attributes.SupportedFirst;
                            }
                        }
                        else
                        {
                            attributes.SupportedSecond = attributes.SupportedFirst;
                        }

                        attributes.SupportedFirst = version;
                    }
                    else
                    {
                        if (attributes.SupportedSecond != null)
                        {
                            if (attributes.SupportedSecond < version)
                            {
                                attributes.SupportedSecond = version;
                            }
                        }
                        else
                        {
                            attributes.SupportedSecond = version;
                        }
                    }
                }
                else
                {
                    attributes.SupportedFirst = version;
                }
            }
        }

        /// <summary>
        /// Determines if the attributes supported only for the platform (allow list)
        /// </summary>
        /// <param name="attributes">PlatformAttributes being checked</param>
        /// <returns>true if it is allow list</returns>
        private static bool AllowList(PlatformAttributes attributes) =>
            attributes.SupportedFirst != null &&
            (attributes.UnsupportedFirst == null || attributes.SupportedFirst < attributes.UnsupportedFirst);

        /// <summary>
        /// Determines if the attributes unsupported only for the platform (deny list)
        /// </summary>
        /// <param name="attributes">PlatformAttributes being checked</param>
        /// <returns>true if it is deny list</returns>
        private static bool DenyList(PlatformAttributes attributes) =>
            attributes.UnsupportedFirst != null &&
            (attributes.SupportedFirst == null || attributes.UnsupportedFirst <= attributes.SupportedFirst);
    }
}