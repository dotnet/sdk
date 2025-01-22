// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Diagnostics.CodeAnalysis;
using System.Globalization;
using System.Linq;
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
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1416: <inheritdoc cref="PlatformCompatibilityTitle"/>
    /// CA1422: <inheritdoc cref="PlatformCompatibilityTitle"/>
    ///
    /// It finds usage of platform-specific or unsupported APIs and diagnoses if the
    /// API is guarded by platform check or if it is annotated with corresponding platform specific attribute.
    /// If using the platform-specific API is not safe it reports diagnostics.
    ///
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed partial class PlatformCompatibilityAnalyzer : DiagnosticAnalyzer
    {
        internal const string SupportRuleId = "CA1416";
        internal const string ObsoletedRuleId = "CA1422";
        private static readonly ImmutableArray<string> s_osPlatformAttributes = ImmutableArray.Create(SupportedOSPlatformAttribute, UnsupportedOSPlatformAttribute, ObsoletedOSPlatformAttribute);

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(PlatformCompatibilityTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(PlatformCompatibilityDescription));

        // We are adding the new attributes into older versions of .Net 5.0, so there could be multiple referenced assemblies each with their own
        // version of internal attribute type which will cause ambiguity, to avoid that we are comparing the attributes by their name
        private const string ObsoletedOSPlatformAttribute = nameof(ObsoletedOSPlatformAttribute);
        private const string SupportedOSPlatformAttribute = nameof(SupportedOSPlatformAttribute);
        private const string UnsupportedOSPlatformAttribute = nameof(UnsupportedOSPlatformAttribute);
        private const string UnsupportedOSPlatformGuardAttribute = nameof(UnsupportedOSPlatformGuardAttribute);
        private const string SupportedOSPlatformGuardAttribute = nameof(SupportedOSPlatformGuardAttribute);

        // Platform guard method name, prefix, suffix
        private const string IsOSPlatform = nameof(IsOSPlatform);
        private const string IsPrefix = "Is";
        private const string OptionalSuffix = "VersionAtLeast";
        private const string NetCoreAppIdentifier = ".NETCoreApp";
        private const string macOS = nameof(macOS);
        private const string OSX = nameof(OSX);
        private const string MacSlashOSX = "macOS/OSX";
        private static readonly Version EmptyVersion = new(0, 0);

        internal static readonly DiagnosticDescriptor OnlySupportedCsReachable = DiagnosticDescriptorHelper.Create(
            SupportRuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(PlatformCompatibilityOnlySupportedCsReachableMessage)),
            DiagnosticCategory.Interoperability,
            RuleLevel.BuildWarning,
            description: s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor OnlySupportedCsUnreachable = DiagnosticDescriptorHelper.Create(
            SupportRuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(PlatformCompatibilityOnlySupportedCsUnreachableMessage)),
            DiagnosticCategory.Interoperability,
            RuleLevel.BuildWarning,
            description: s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor OnlySupportedCsAllPlatforms = DiagnosticDescriptorHelper.Create(
            SupportRuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(PlatformCompatibilityOnlySupportedCsAllPlatformMessage)),
            DiagnosticCategory.Interoperability,
            RuleLevel.BuildWarning,
            description: s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor SupportedCsAllPlatforms = DiagnosticDescriptorHelper.Create(
            SupportRuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(PlatformCompatibilitySupportedCsAllPlatformMessage)),
            DiagnosticCategory.Interoperability,
            RuleLevel.BuildWarning,
            description: s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor SupportedCsReachable = DiagnosticDescriptorHelper.Create(
            SupportRuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(PlatformCompatibilitySupportedCsReachableMessage)),
            DiagnosticCategory.Interoperability,
            RuleLevel.BuildWarning,
            description: s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor UnsupportedCsAllPlatforms = DiagnosticDescriptorHelper.Create(
            SupportRuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(PlatformCompatibilityUnsupportedCsAllPlatformMessage)),
            DiagnosticCategory.Interoperability,
            RuleLevel.BuildWarning,
            description: s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor UnsupportedCsReachable = DiagnosticDescriptorHelper.Create(
            SupportRuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(PlatformCompatibilityUnsupportedCsReachableMessage)),
            DiagnosticCategory.Interoperability,
            RuleLevel.BuildWarning,
            description: s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor ObsoletedCsAllPlatforms = DiagnosticDescriptorHelper.Create(
            ObsoletedRuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(PlatformCompatibilityObsoletedCsAllPlatformMessage)),
            DiagnosticCategory.Interoperability,
            RuleLevel.BuildWarning,
            description: s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor ObsoletedCsReachable = DiagnosticDescriptorHelper.Create(
            ObsoletedRuleId,
            s_localizableTitle,
            CreateLocalizableResourceString(nameof(PlatformCompatibilityObsoletedCsReachableMessage)),
            DiagnosticCategory.Interoperability,
            RuleLevel.BuildWarning,
            description: s_localizableDescription,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(OnlySupportedCsReachable, OnlySupportedCsUnreachable,
            OnlySupportedCsAllPlatforms, SupportedCsAllPlatforms, SupportedCsReachable, UnsupportedCsAllPlatforms, UnsupportedCsReachable, ObsoletedCsAllPlatforms, ObsoletedCsReachable);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                if (!PlatformAnalysisAllowed(context.Options, context.Compilation))
                {
                    return;
                }

                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemOperatingSystem, out var operatingSystemType))
                {
                    return;
                }

                var osPlatformType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesOSPlatform);
                var runtimeInformationType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesRuntimeInformation);

                var stringType = context.Compilation.GetSpecialType(SpecialType.System_String);
                if (stringType == null)
                {
                    return;
                }

                var msBuildPlatforms = GetSupportedPlatforms(context.Options, context.Compilation);
                var runtimeIsOSPlatformMethod = runtimeInformationType?.GetMembers().OfType<IMethodSymbol>().FirstOrDefault(m =>
                        IsOSPlatform == m.Name &&
                        m.IsStatic &&
                        m.ReturnType.SpecialType == SpecialType.System_Boolean &&
                        m.Parameters.Length == 1 &&
                        m.Parameters[0].Type.Equals(osPlatformType));

                var guardMethods = GetOperatingSystemGuardMethods(runtimeIsOSPlatformMethod, operatingSystemType, out var relatedPlatforms);
                var platformSpecificMembers = new ConcurrentDictionary<ISymbol, PlatformAttributes>();
                var osPlatformCreateMethod = osPlatformType?.GetMembers("Create").OfType<IMethodSymbol>().FirstOrDefault(m =>
                    m.IsStatic &&
                    m.ReturnType.Equals(osPlatformType) &&
                    m.Parameters.Length == 1 &&
                    m.Parameters[0].Type.SpecialType == SpecialType.System_String);
                var notSupportedExceptionType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNotSupportedException);
                var crossPlatform = HasCrossPlatformProperty(context.Options, context.Compilation);

                context.RegisterOperationBlockStartAction(
                    context => AnalyzeOperationBlock(context, guardMethods.ToImmutableArray(), runtimeIsOSPlatformMethod, osPlatformCreateMethod, crossPlatform,
                                    osPlatformType, stringType, platformSpecificMembers, msBuildPlatforms, notSupportedExceptionType, relatedPlatforms));
            });

            static List<IMethodSymbol> GetOperatingSystemGuardMethods(IMethodSymbol? runtimeIsOSPlatformMethod,
                INamedTypeSymbol operatingSystemType, out SmallDictionary<string, (string relatedPlatform, bool isSubset)> relatedPlatforms)
            {
                relatedPlatforms = new SmallDictionary<string, (string relatedPlatform, bool isSubset)>(StringComparer.OrdinalIgnoreCase);

                var methods = FilterPlatformCheckMethods(operatingSystemType, relatedPlatforms).ToList();

                if (runtimeIsOSPlatformMethod != null)
                {
                    methods.Add(runtimeIsOSPlatformMethod);
                }

                return methods;
            }

            static IEnumerable<IMethodSymbol> FilterPlatformCheckMethods(INamedTypeSymbol symbol,
                SmallDictionary<string, (string relatedPlatform, bool isSubset)> relatedPlatforms)
            {
                return symbol.GetMembers().OfType<IMethodSymbol>().Where(m =>
                {
                    if (m.IsStatic &&
                        m.ReturnType.SpecialType == SpecialType.System_Boolean &&
                        (IsOSPlatform == m.Name || NameAndParametersValid(m)))
                    {
                        CheckDependentPlatforms(m, relatedPlatforms);
                        return true;
                    }

                    return false;
                });
            }

            static void CheckDependentPlatforms(IMethodSymbol method, SmallDictionary<string, (string relatedPlatform, bool isSubset)> relatedPlatforms)
            {
                if (TryExtractPlatformName(method.Name, out var name))
                {
                    foreach (AttributeData attribute in method.GetAttributes())
                    {
                        if (attribute.AttributeClass?.Name is SupportedOSPlatformGuardAttribute &&
                            TryParsePlatformNameAndVersion(attribute, out var platformName, out var _))
                        {
                            relatedPlatforms[name] = (platformName, true);
                            relatedPlatforms[platformName] = (name, false);
                        }
                    }
                }
            }

            static ImmutableArray<string> GetSupportedPlatforms(AnalyzerOptions options, Compilation compilation) =>
                options.GetMSBuildItemMetadataValues(MSBuildItemOptionNames.SupportedPlatform, compilation);

            static bool NameAndParametersValid(IMethodSymbol method) => method.Name.StartsWith(IsPrefix, StringComparison.Ordinal) &&
                    (method.Parameters.Length == 0 || method.Name.EndsWith(OptionalSuffix, StringComparison.Ordinal));

            static bool HasCrossPlatformProperty(AnalyzerOptions options, Compilation compilation)
            {
                return options.GetMSBuildPropertyValue(MSBuildPropertyOptionNames.PlatformNeutralAssembly, compilation) is not null;
            }
        }

        private static bool TryExtractPlatformName(string methodName, [NotNullWhen(true)] out string? platformName)
        {
            if (!methodName.StartsWith(IsPrefix, StringComparison.Ordinal))
            {
                platformName = null;
                return false;
            }

            if (methodName.EndsWith(OptionalSuffix, StringComparison.Ordinal))
            {
                platformName = methodName.Substring(2, methodName.Length - 2 - OptionalSuffix.Length);
                return true;
            }

            platformName = methodName[2..];
            return true;
        }

        private static bool PlatformAnalysisAllowed(AnalyzerOptions options, Compilation compilation)
        {
            string tfmIdentifier = options.GetMSBuildPropertyValue(MSBuildPropertyOptionNames.TargetFrameworkIdentifier, compilation) ?? "";
            string tfmVersion = options.GetMSBuildPropertyValue(MSBuildPropertyOptionNames.TargetFrameworkVersion, compilation) ?? "";

            if (tfmIdentifier.Equals(NetCoreAppIdentifier, StringComparison.OrdinalIgnoreCase) &&
                tfmVersion.StartsWith("v", StringComparison.OrdinalIgnoreCase) &&
                Version.TryParse(tfmVersion[1..], out var version) &&
                version.Major >= 5)
            {
                // We want to only support cases we know are well-formed by default
                return true;
            }
            else
            {
                // We want to fallback to allowing force enablement regardless of recognizing the Identifier/Version
                return LowerTargetsEnabled(options, compilation);
            }
        }

        private static bool LowerTargetsEnabled(AnalyzerOptions options, Compilation compilation) =>
            compilation.SyntaxTrees.FirstOrDefault() is { } tree &&
            options.GetBoolOptionValue(EditorConfigOptionNames.EnablePlatformAnalyzerOnPreNet5Target, SupportedCsAllPlatforms, tree, compilation, false);

        private void AnalyzeOperationBlock(
            OperationBlockStartAnalysisContext context,
            ImmutableArray<IMethodSymbol> guardMethods,
            IMethodSymbol? runtimeIsOSPlatformMethod,
            IMethodSymbol? osPlatformCreateMethod,
            bool crossPlatform,
            INamedTypeSymbol? osPlatformType,
            INamedTypeSymbol stringType,
            ConcurrentDictionary<ISymbol, PlatformAttributes> platformSpecificMembers,
            ImmutableArray<string> msBuildPlatforms,
            ITypeSymbol? notSupportedExceptionType,
            SmallDictionary<string, (string relatedPlatform, bool isSubset)> relatedPlatforms)
        {
            if (context.IsMethodNotImplementedOrSupported(checkPlatformNotSupported: true))
            {
                return;
            }

            var osPlatformTypeArray = osPlatformType != null ? ImmutableArray.Create(osPlatformType) : ImmutableArray<INamedTypeSymbol>.Empty;
            var platformSpecificOperations = PooledConcurrentDictionary<KeyValuePair<IOperation, ISymbol>,
                (SmallDictionary<string, Versions> attributes, SmallDictionary<string, Versions>? csAttributes)>.GetInstance();

            context.RegisterOperationAction(context =>
            {
                AnalyzeOperation(context.Operation, context, platformSpecificOperations, platformSpecificMembers,
                    msBuildPlatforms, notSupportedExceptionType, crossPlatform, relatedPlatforms);
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
                    if (platformSpecificOperations.IsEmpty ||
                        guardMethods.IsEmpty ||
                        !(context.OperationBlocks.GetControlFlowGraph() is { } cfg))
                    {
                        return;
                    }

                    var performValueContentAnalysis = ComputeNeedsValueContentAnalysis(cfg.OriginalOperation, guardMethods, runtimeIsOSPlatformMethod, osPlatformType);
                    var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(context.Compilation);
                    var analysisResult = GlobalFlowStateAnalysis.TryGetOrComputeResult(
                        cfg, context.OwningSymbol, CreateOperationVisitor, wellKnownTypeProvider,
                        context.Options, SupportedCsAllPlatforms, performValueContentAnalysis,
                        pessimisticAnalysis: false,
                        valueContentAnalysisResult: out var valueContentAnalysisResult, additionalSupportedValueTypes: osPlatformTypeArray,
                        getValueContentValueForAdditionalSupportedValueTypeOperation: osPlatformTypeArray.IsEmpty ? null : GetValueContentValue);

                    if (analysisResult == null)
                    {
                        return;
                    }

                    foreach (var (platformSpecificOperation, pair) in platformSpecificOperations)
                    {
                        var value = analysisResult[platformSpecificOperation.Key.Kind, platformSpecificOperation.Key.Syntax];
                        var csAttributes = pair.csAttributes != null ? CopyAttributes(pair.csAttributes) : null;

                        if ((value.Kind == GlobalFlowStateAnalysisValueSetKind.Known && IsKnownValueGuarded(pair.attributes, ref csAttributes, value, pair.csAttributes)) ||
                           (value.Kind == GlobalFlowStateAnalysisValueSetKind.Unknown && HasGuardedLambdaOrLocalFunctionResult(platformSpecificOperation.Key,
                           pair.attributes, ref csAttributes, analysisResult, pair.csAttributes)))
                        {
                            continue;
                        }

                        ReportDiagnostics(platformSpecificOperation, pair.attributes, csAttributes, context, platformSpecificMembers);
                    }
                }
                finally
                {
                    platformSpecificOperations.Free(context.CancellationToken);
                }

                return;

                OperationVisitor CreateOperationVisitor(GlobalFlowStateAnalysisContext context) => new(guardMethods, osPlatformType, relatedPlatforms, context);

                ValueContentAbstractValue GetValueContentValue(IOperation operation)
                {
                    Debug.Assert(operation.Type!.Equals(osPlatformType));
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

        private static bool HasGuardedLambdaOrLocalFunctionResult(IOperation platformSpecificOperation, SmallDictionary<string, Versions> attributes,
            ref SmallDictionary<string, Versions>? csAttributes, DataFlowAnalysisResult<GlobalFlowStateBlockAnalysisResult,
                GlobalFlowStateAnalysisValueSet> analysisResult, SmallDictionary<string, Versions>? originalCsAttributes)
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
                    !IsKnownValueGuarded(CopyAttributes(attributes), ref csAttributes, localValue, originalCsAttributes))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool ComputeNeedsValueContentAnalysis(IOperation operationBlock, ImmutableArray<IMethodSymbol> guardMethods, IMethodSymbol? runtimeIsOSPlatformMethod, INamedTypeSymbol? osPlatformType)
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
                            if (argument.Parameter?.Type.SpecialType == SpecialType.System_Int32 &&
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

        private static bool IsKnownValueGuarded(SmallDictionary<string, Versions> attributes,
                ref SmallDictionary<string, Versions>? csAttributes, GlobalFlowStateAnalysisValueSet value, SmallDictionary<string, Versions>? originalCsAttributes)
        {
            using var capturedVersions = PooledDictionary<string, Version>.GetInstance(StringComparer.OrdinalIgnoreCase);
            return IsKnownValueGuarded(attributes, ref csAttributes, value, capturedVersions, originalCsAttributes);

            static bool IsKnownValueGuarded(
                SmallDictionary<string, Versions> attributes,
                ref SmallDictionary<string, Versions>? csAttributes,
                GlobalFlowStateAnalysisValueSet value,
                PooledDictionary<string, Version> capturedVersions,
                SmallDictionary<string, Versions>? originalCsAttributes)
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
                                if (attribute.UnsupportedFirst != null &&
                                    attribute.UnsupportedFirst.IsGreaterThanOrEqualTo(info.Version))
                                {
                                    if (DenyList(attribute))
                                    {
                                        attribute.SupportedFirst = null;
                                        attribute.SupportedSecond = null;
                                        attribute.UnsupportedSecond = null;
                                    }

                                    attribute.UnsupportedFirst = null;
                                    attribute.UnsupportedMessage = null;
                                }
                                else
                                {
                                    if (originalCsAttributes != null &&
                                        AllowList(attribute) &&
                                        IsOnlySupportNeedsGuard(info.PlatformName, attributes, originalCsAttributes))
                                    {
                                        attribute.SupportedFirst = null;
                                        attribute.SupportedSecond = null;
                                        attribute.UnsupportedSecond = null;
                                        attribute.UnsupportedFirst = null;
                                        attribute.UnsupportedMessage = null;
                                    }
                                    else if (value.AnalysisValues.Contains(new PlatformMethodValue(info.PlatformName, EmptyVersion, false)))
                                    {
                                        csAttributes = SetCallSiteUnsupportedAttribute(csAttributes, info);
                                    }
                                }

                                if (attribute.UnsupportedSecond != null &&
                                    attribute.UnsupportedSecond.IsGreaterThanOrEqualTo(info.Version))
                                {
                                    attribute.UnsupportedSecond = null;
                                }

                                if (attribute.Obsoleted != null &&
                                    attribute.Obsoleted.IsGreaterThanOrEqualTo(info.Version))
                                {
                                    attribute.Obsoleted = null;
                                    attribute.ObsoletedMessage = null;
                                    attribute.ObsoletedUrl = null;
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
                                        attribute.UnsupportedFirst.IsGreaterThanOrEqualTo(version))
                                    {
                                        attribute.UnsupportedFirst = null;
                                        attribute.UnsupportedMessage = null;
                                    }

                                    if (attribute.UnsupportedSecond != null &&
                                        capturedVersions.TryGetValue(info.PlatformName, out version) &&
                                        version.IsGreaterThanOrEqualTo(attribute.UnsupportedSecond))
                                    {
                                        attribute.UnsupportedSecond = null;
                                        attribute.UnsupportedMessage = null;
                                    }
                                }

                                var checkVersion = attribute.SupportedSecond ?? attribute.SupportedFirst;

                                if (checkVersion != null &&
                                    info.Version.IsGreaterThanOrEqualTo(checkVersion))
                                {
                                    attribute.SupportedFirst = null;
                                    attribute.SupportedSecond = null;
                                    RemoveUnsupportedWithLessVersion(info.Version, attribute);
                                    RemoveOtherSupportsOnDifferentPlatforms(attributes, info.PlatformName);
                                }
                                else
                                {
                                    capturedVersions.TryGetValue(info.PlatformName, out var unsupportedVersion);
                                    csAttributes = SetCallSiteSupportedAttribute(csAttributes, info, unsupportedVersion);
                                }

                                RemoveUnsupportsOnDifferentPlatforms(attributes, info.PlatformName);
                            }
                        }
                        else if (!info.Negated)
                        {
                            // it is checking one exact platform, other unsupported should be suppressed
                            RemoveUnsupportsOnDifferentPlatforms(attributes, info.PlatformName);
                            csAttributes = SetCallSiteSupportedAttribute(csAttributes, info, null);
                        }
                    }
                }

                if (value.Parents.IsEmpty)
                {
                    foreach (var attribute in attributes)
                    {
                        // if any of the attributes is not suppressed
                        if (attribute.Value.IsSet())
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
                        var parentCsAttributes = csAttributes == null ? null : CopyAttributes(csAttributes);
                        using var parentCapturedVersions = PooledDictionary<string, Version>.GetInstance(capturedVersions);

                        if (parent.AnalysisValues.Count > 0)
                        {
                            if (parentCsAttributes != null && parentCsAttributes.Any() &&
                                IsNegationOfCallsiteAttributes(parentCsAttributes, parent.AnalysisValues))
                            {
                                continue;
                            }

                            if (value.AnalysisValues.Count == parent.AnalysisValues.Count &&
                                IsNegationOfParentValues(value, parent.AnalysisValues.GetEnumerator()))
                            {
                                continue;
                            }
                        }

                        if (!IsKnownValueGuarded(parentAttributes, ref parentCsAttributes, parent, parentCapturedVersions, originalCsAttributes))
                        {
                            csAttributes = parentCsAttributes;
                            return false;
                        }
                    }
                }

                return true;
            }

            static bool IsOnlySupportNeedsGuard(string platformName, SmallDictionary<string, Versions> attributes, SmallDictionary<string, Versions> csAttributes)
                 => csAttributes.TryGetValue(platformName, out var versions) &&
                    AllowList(versions) &&
                    attributes.Count() == 1 &&
                    csAttributes.Any(cs => !cs.Key.Equals(platformName, StringComparison.OrdinalIgnoreCase));

            static bool IsNegationOfCallsiteAttributes(SmallDictionary<string, Versions> csAttributes, ImmutableHashSet<IAbstractAnalysisValue> parentValues)
            {
                bool allowList = AllowList(csAttributes.First().Value);

                foreach (var value in parentValues)
                {
                    if (value is PlatformMethodValue info)
                    {
                        if (csAttributes.TryGetValue(info.PlatformName, out var version))
                        {
                            if (info.Negated)
                            {
                                if (version.SupportedFirst != info.Version)
                                    return false;
                            }
                            else
                            {
                                if (version.UnsupportedFirst != info.Version)
                                    return false;
                            }

                            continue;
                        }
                        else if (allowList) // If callsite is supported only list then no need to worry about other platform guard
                        {
                            continue;
                        }
                    }

                    return false;
                }

                return true;
            }

            static bool IsNegationOfParentValues(GlobalFlowStateAnalysisValueSet value, ImmutableHashSet<IAbstractAnalysisValue>.Enumerator parentEnumerator)
            {
                foreach (var val in value.AnalysisValues)
                {
                    if (parentEnumerator.MoveNext() && !parentEnumerator.Current.Equals(val.GetNegatedValue()))
                    {
                        return false;
                    }
                }

                return true;
            }

            static SmallDictionary<string, Versions> SetCallSiteSupportedAttribute(SmallDictionary<string, Versions>? csAttributes,
                PlatformMethodValue info, Version? unsupportedVersion)
            {
                csAttributes ??= new SmallDictionary<string, Versions>(StringComparer.OrdinalIgnoreCase);

                if (csAttributes.TryGetValue(info.PlatformName, out var attributes))
                {
                    if (attributes.SupportedFirst == null)
                    {
                        attributes.SupportedFirst = info.Version;
                    }
                    else
                    {
                        attributes.SupportedSecond = info.Version;
                    }

                    attributes.UnsupportedFirst = unsupportedVersion;
                }
                else
                {
                    csAttributes.Add(info.PlatformName, new Versions() { SupportedFirst = info.Version, UnsupportedFirst = unsupportedVersion });
                }

                return csAttributes;
            }

            static SmallDictionary<string, Versions> SetCallSiteUnsupportedAttribute(SmallDictionary<string, Versions>? csAttributes, PlatformMethodValue info)
            {
                csAttributes ??= new SmallDictionary<string, Versions>(StringComparer.OrdinalIgnoreCase);

                if (csAttributes.TryGetValue(info.PlatformName, out var attributes))
                {
                    if (attributes.UnsupportedFirst == null)
                    {
                        attributes.UnsupportedFirst = info.Version;
                    }
                    else
                    {
                        attributes.UnsupportedSecond = info.Version;
                    }
                }
                else
                {
                    csAttributes.Add(info.PlatformName, new Versions() { UnsupportedFirst = info.Version });
                }

                return csAttributes;
            }

            static void RemoveUnsupportsOnDifferentPlatforms(SmallDictionary<string, Versions> attributes, string platformName)
            {
                foreach (var (name, attribute) in attributes)
                {
                    if (!name.Equals(platformName, StringComparison.OrdinalIgnoreCase))
                    {
                        if (DenyList(attribute))
                        {
                            attribute.UnsupportedFirst = null;
                            attribute.UnsupportedSecond = null;
                            attribute.SupportedFirst = null;
                            attribute.SupportedSecond = null;
                            attribute.UnsupportedMessage = null;
                        }

                        attribute.Obsoleted = null;
                        attribute.ObsoletedMessage = null;
                        attribute.ObsoletedUrl = null;
                    }
                }
            }

            static void RemoveUnsupportedWithLessVersion(Version supportedVersion, Versions attribute)
            {
                if (supportedVersion.IsGreaterThanOrEqualTo(attribute.UnsupportedFirst))
                {
                    attribute.UnsupportedFirst = null;
                }
            }

            static void RemoveOtherSupportsOnDifferentPlatforms(SmallDictionary<string, Versions> attributes, string platformName)
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

        private static void ReportDiagnostics(KeyValuePair<IOperation, ISymbol> operationToSymbol, SmallDictionary<string, Versions> attributes,
            SmallDictionary<string, Versions>? csAttributes, OperationBlockAnalysisContext context,
            ConcurrentDictionary<ISymbol, PlatformAttributes> platformSpecificMembers)
        {
            var symbol = operationToSymbol.Value is IMethodSymbol method && method.IsConstructor() ? operationToSymbol.Value.ContainingType : operationToSymbol.Value;
            var operationName = symbol.ToDisplayString(GetLanguageSpecificFormat(operationToSymbol.Key));

            var originalAttributes = platformSpecificMembers[symbol].Platforms ?? attributes;

            foreach (var attribute in originalAttributes.Values)
            {
                if (AllowList(attribute))
                {
                    ReportSupportedDiagnostic(operationToSymbol.Key, context, operationName, attributes, csAttributes);
                }
                else
                {
                    ReportUnsupportedDiagnostic(operationToSymbol.Key, context, operationName, attributes, csAttributes);
                }

                break;
            }

            static void ReportSupportedDiagnostic(IOperation operation, OperationBlockAnalysisContext context, string operationName,
                 SmallDictionary<string, Versions> attributes, SmallDictionary<string, Versions>? callsiteAttributes)
            {
                var supportedRule = GetSupportedPlatforms(attributes, callsiteAttributes, out var platformNames, out var obsoletedPlatforms);
                var csPlatformNames = JoinNames(GetCallsitePlatforms(attributes, callsiteAttributes, out var callsite, supported: supportedRule));

                if (callsite == Callsite.Reachable && IsDenyList(callsiteAttributes))
                {
                    csPlatformNames = string.Join(CommaSeparator, csPlatformNames, PlatformCompatibilityAllPlatforms);
                }

                if (!platformNames.IsEmpty)
                {
                    var rule = supportedRule ? SwitchSupportedRule(callsite) : SwitchRule(callsite, true);
                    context.ReportDiagnostic(operation.CreateDiagnostic(rule, operationName, JoinNames(platformNames), csPlatformNames));
                }

                if (!obsoletedPlatforms.IsEmpty)
                {
                    context.ReportDiagnostic(operation.CreateDiagnostic(SwitchObsoletedRule(callsite), operationName, JoinNames(obsoletedPlatforms), csPlatformNames));
                }

                static DiagnosticDescriptor SwitchSupportedRule(Callsite callsite)
                    => callsite switch
                    {
                        Callsite.AllPlatforms => OnlySupportedCsAllPlatforms,
                        Callsite.Reachable => OnlySupportedCsReachable,
                        Callsite.Unreachable => OnlySupportedCsUnreachable,
                        _ => throw new NotImplementedException()
                    };

                static bool IsDenyList(SmallDictionary<string, Versions>? callsiteAttributes) =>
                    callsiteAttributes != null && callsiteAttributes.Any(csa => DenyList(csa.Value));

                static bool GetSupportedPlatforms(SmallDictionary<string, Versions> attributes, SmallDictionary<string, Versions>? csAttributes,
                    out ImmutableArray<string> platformNames, out ImmutableArray<string> obsoletedPlatforms)
                {
                    using var obsoletedBuilder = ArrayBuilder<string>.GetInstance();
                    bool? supportedRule = null;
                    using var platformsBuilder = ArrayBuilder<string>.GetInstance();
                    foreach (var (pName, pAttribute) in attributes)
                    {
                        if (pAttribute.SupportedFirst != null && supportedRule.GetValueOrDefault(true))
                        {
                            supportedRule = true;
                            var supportedVersion = pAttribute.SupportedSecond ?? pAttribute.SupportedFirst;
                            if (pAttribute.UnsupportedFirst != null && !IsEmptyVersion(pAttribute.UnsupportedFirst))
                            {
                                if (IsEmptyVersion(supportedVersion))
                                {
                                    platformsBuilder.Add(AppendMessage(pAttribute,
                                        GetFormattedString(PlatformCompatibilityVersionAndBefore, pName, pAttribute.UnsupportedFirst)));
                                }
                                else
                                {
                                    platformsBuilder.Add(AppendMessage(pAttribute,
                                        GetFormattedString(PlatformCompatibilityFromVersionToVersion, pName, supportedVersion, pAttribute.UnsupportedFirst)));
                                }
                            }
                            else if (IsEmptyVersion(supportedVersion))
                            {
                                if (csAttributes != null && HasSameVersionedPlatformSupport(csAttributes, pName, checkSupport: false))
                                {
                                    platformsBuilder.Add(GetFormattedString(PlatformCompatibilityAllVersions, pName));
                                    continue;
                                }

                                platformsBuilder.Add(EncloseWithQuotes(pName));
                            }
                            else
                            {
                                platformsBuilder.Add(GetFormattedString(PlatformCompatibilityVersionAndLater, pName, supportedVersion));
                            }
                        }
                        else if (pAttribute.UnsupportedFirst != null)
                        {
                            if (supportedRule.GetValueOrDefault())
                            {
                                platformsBuilder.Clear();
                            }

                            supportedRule = false;
                            if (IsEmptyVersion(pAttribute.UnsupportedFirst))
                            {
                                if (csAttributes != null && HasSameVersionedPlatformSupport(csAttributes, pName, checkSupport: true))
                                {
                                    platformsBuilder.Add(AppendMessage(pAttribute, GetFormattedString(PlatformCompatibilityAllVersions, pName)));
                                    continue;
                                }

                                platformsBuilder.Add(AppendMessage(pAttribute, EncloseWithQuotes(pName)));
                            }
                            else
                            {
                                platformsBuilder.Add(AppendMessage(pAttribute,
                                    GetFormattedString(PlatformCompatibilityVersionAndLater, pName, pAttribute.UnsupportedFirst)));
                            }
                        }

                        AddObsoleted(csAttributes, obsoletedBuilder, pName, pAttribute);
                    }

                    obsoletedPlatforms = obsoletedBuilder.ToImmutable();
                    platformNames = platformsBuilder.ToImmutable();
                    return supportedRule.GetValueOrDefault(true);
                }
            }

            static DiagnosticDescriptor SwitchObsoletedRule(Callsite callsite)
            {
                return callsite switch
                {
                    Callsite.AllPlatforms => ObsoletedCsAllPlatforms,
                    Callsite.Reachable => ObsoletedCsReachable,
                    _ => throw new NotImplementedException()
                };
            }

            static DiagnosticDescriptor SwitchRule(Callsite callsite, bool unsupported)
            {
                if (unsupported)
                {
                    return callsite switch
                    {
                        Callsite.AllPlatforms => UnsupportedCsAllPlatforms,
                        Callsite.Reachable => UnsupportedCsReachable,
                        _ => throw new NotImplementedException()
                    };
                }
                else
                {
                    return callsite switch
                    {
                        Callsite.AllPlatforms => SupportedCsAllPlatforms,
                        Callsite.Reachable => SupportedCsReachable,
                        _ => throw new NotImplementedException()
                    };
                }
            }

            static string AppendMessage(Versions attribute, string message)
            {
                if (attribute.UnsupportedMessage is not null)
                {
                    message += string.Format(CultureInfo.InvariantCulture, ParenthesisWithPlaceHolder, attribute.UnsupportedMessage);
                }

                return message;
            }

            static string AppendMessageAndUrl(Versions attribute, string message)
            {
                if (attribute.ObsoletedMessage is not null)
                {
                    string customMessge = attribute.ObsoletedMessage;
                    if (attribute.ObsoletedUrl is not null)
                    {
                        customMessge = $"{customMessge} {attribute.ObsoletedUrl}";
                    }

                    message += string.Format(CultureInfo.InvariantCulture, ParenthesisWithPlaceHolder, customMessge);
                }
                else if (attribute.ObsoletedUrl is not null)
                {
                    message += string.Format(CultureInfo.InvariantCulture, ParenthesisWithPlaceHolder, attribute.ObsoletedUrl);
                }

                return message;
            }

            static void ReportUnsupportedDiagnostic(IOperation operation, OperationBlockAnalysisContext context, string operationName,
                SmallDictionary<string, Versions> attributes, SmallDictionary<string, Versions>? callsiteAttributes)
            {
                var unsupportedRule = GetPlatformNames(attributes, callsiteAttributes, out var platformNames, out var obsoletedPlatforms);
                var csPlatformNames = JoinNames(GetCallsitePlatforms(attributes, callsiteAttributes, out var callsite, supported: !unsupportedRule));

                if (!platformNames.IsEmpty)
                {
                    context.ReportDiagnostic(operation.CreateDiagnostic(SwitchRule(callsite, unsupportedRule), operationName, JoinNames(platformNames), csPlatformNames));
                }

                if (!obsoletedPlatforms.IsEmpty)
                {
                    context.ReportDiagnostic(operation.CreateDiagnostic(SwitchObsoletedRule(callsite), operationName, JoinNames(obsoletedPlatforms), csPlatformNames));
                }

                static bool GetPlatformNames(SmallDictionary<string, Versions> attributes, SmallDictionary<string, Versions>? csAttributes,
                    out ImmutableArray<string> platformNames, out ImmutableArray<string> obsoletedPlatforms)
                {
                    using var obsoletedBuilder = ArrayBuilder<string>.GetInstance();
                    using var platformsBuilder = ArrayBuilder<string>.GetInstance();
                    var unsupportedRule = true;
                    foreach (var (pName, pAttribute) in attributes)
                    {
                        var unsupportedVersion = pAttribute.UnsupportedSecond ?? pAttribute.UnsupportedFirst;
                        var supportedVersion = pAttribute.SupportedSecond ?? pAttribute.SupportedFirst;

                        if (unsupportedVersion != null)
                        {
                            if (supportedVersion != null)
                            {
                                unsupportedRule = false;

                                if (supportedVersion > unsupportedVersion)
                                {
                                    if (csAttributes == null || (csAttributes.TryGetValue(pName, out var csAttribute) &&
                                        csAttribute.UnsupportedFirst != null && csAttribute.UnsupportedFirst > supportedVersion))
                                    {
                                        unsupportedRule = true;
                                        if (IsEmptyVersion(pAttribute.UnsupportedFirst!))
                                        {
                                            platformsBuilder.Add(AppendMessage(pAttribute, GetFormattedString(PlatformCompatibilityVersionAndBefore, pName, supportedVersion)));
                                        }
                                        else
                                        {
                                            platformsBuilder.Add(AppendMessage(pAttribute,
                                                GetFormattedString(PlatformCompatibilityFromVersionToVersion, pName, unsupportedVersion, supportedVersion)));
                                        }
                                    }
                                    else
                                    {
                                        platformsBuilder.Add(AppendMessage(pAttribute, GetFormattedString(PlatformCompatibilityVersionAndLater, pName, supportedVersion)));
                                    }
                                }
                                else
                                {
                                    platformsBuilder.Add(AppendMessage(pAttribute,
                                        GetFormattedString(PlatformCompatibilityFromVersionToVersion, pName, supportedVersion, unsupportedVersion)));
                                }
                            }
                            else
                            {
                                if (IsEmptyVersion(unsupportedVersion))
                                {
                                    if (csAttributes != null && HasSameVersionedPlatformSupport(csAttributes, pName, checkSupport: true))
                                    {
                                        platformsBuilder.Add(AppendMessage(pAttribute, GetFormattedString(PlatformCompatibilityAllVersions, pName)));
                                    }
                                    else
                                    {
                                        platformsBuilder.Add(AppendMessage(pAttribute, EncloseWithQuotes(pName)));
                                    }
                                }
                                else
                                {
                                    platformsBuilder.Add(AppendMessage(pAttribute, GetFormattedString(PlatformCompatibilityVersionAndLater, pName, unsupportedVersion)));
                                }
                            }
                        }
                        else if (supportedVersion != null)
                        {
                            unsupportedRule = false;
                            if (IsEmptyVersion(supportedVersion))
                            {
                                platformsBuilder.Add(EncloseWithQuotes(pName));
                            }
                            else
                            {
                                platformsBuilder.Add(GetFormattedString(PlatformCompatibilityVersionAndLater, pName, supportedVersion));
                            }
                        }

                        AddObsoleted(csAttributes, obsoletedBuilder, pName, pAttribute);
                    }

                    obsoletedPlatforms = obsoletedBuilder.ToImmutable();
                    platformNames = platformsBuilder.ToImmutable();
                    return unsupportedRule;
                }
            }

            static void AddObsoleted(SmallDictionary<string, Versions>? csAttributes, ArrayBuilder<string> obsoletedBuilder, string pName, Versions pAttribute)
            {
                if (pAttribute.Obsoleted != null)
                {
                    if (IsEmptyVersion(pAttribute.Obsoleted)) // Do not need to add the version part if it is 0.0
                    {
                        if (csAttributes != null && HasVersionedCallsite(csAttributes, pName))
                        {
                            obsoletedBuilder.Add(AppendMessage(pAttribute, GetFormattedString(PlatformCompatibilityAllVersions, pName)));
                        }
                        else
                        {
                            obsoletedBuilder.Add(AppendMessage(pAttribute, EncloseWithQuotes(pName)));
                        }
                    }
                    else
                    {
                        obsoletedBuilder.Add(AppendMessageAndUrl(pAttribute, GetFormattedString(PlatformCompatibilityVersionAndLater, pName, pAttribute.Obsoleted)));
                    }
                }
            }

            static ImmutableArray<string> GetCallsitePlatforms(SmallDictionary<string, Versions> attributes,
                SmallDictionary<string, Versions>? callsiteAttributes, out Callsite callsite, bool supported)
            {
                callsite = Callsite.AllPlatforms;
                using var platformNames = ArrayBuilder<string>.GetInstance();
                if (callsiteAttributes != null)
                {
                    foreach (var (pName, csAttribute) in callsiteAttributes)
                    {
                        var supportedVersion = csAttribute.SupportedSecond ?? csAttribute.SupportedFirst;
                        if (supportedVersion != null)
                        {
                            callsite = Callsite.Reachable;
                            if (csAttribute.UnsupportedFirst != null && !IsEmptyVersion(csAttribute.UnsupportedFirst))
                            {
                                if (IsEmptyVersion(supportedVersion))
                                {
                                    platformNames.Add(GetFormattedString(PlatformCompatibilityVersionAndBefore, pName, csAttribute.UnsupportedFirst));
                                }
                                else if (supportedVersion > csAttribute.UnsupportedFirst)
                                {
                                    platformNames.Add(GetFormattedString(PlatformCompatibilityVersionAndLater, pName, supportedVersion));
                                }
                                else
                                {
                                    platformNames.Add(GetFormattedString(PlatformCompatibilityFromVersionToVersion,
                                        pName, supportedVersion, csAttribute.UnsupportedFirst));
                                }
                            }
                            else if (IsEmptyVersion(supportedVersion))
                            {
                                if (HasSameVersionedPlatformSupport(attributes, pName, supported))
                                {
                                    platformNames.Add(GetFormattedString(PlatformCompatibilityAllVersions, pName));
                                    continue;
                                }

                                platformNames.Add(EncloseWithQuotes(pName));
                            }
                            else
                            {
                                var unsupportedVersion = csAttribute.UnsupportedSecond ?? csAttribute.UnsupportedFirst;
                                if (unsupportedVersion != null && unsupportedVersion > supportedVersion)
                                {
                                    platformNames.Add(GetFormattedString(PlatformCompatibilityFromVersionToVersion,
                                        pName, supportedVersion, unsupportedVersion));
                                }
                                else
                                {
                                    platformNames.Add(GetFormattedString(PlatformCompatibilityVersionAndLater, pName, supportedVersion));
                                }
                            }
                        }
                        else
                        {
                            var unsupportedVersion = csAttribute.UnsupportedSecond ?? csAttribute.UnsupportedFirst;
                            if (unsupportedVersion != null && attributes.TryGetValue(pName, out var attribute))
                            {
                                var calledUnsupported = attribute.UnsupportedSecond ?? attribute.UnsupportedFirst;
                                callsite = Callsite.Unreachable;
                                if (IsEmptyVersion(unsupportedVersion))
                                {
                                    if (HasSameVersionedPlatformSupport(attributes, pName, supported))
                                    {
                                        platformNames.Add(GetFormattedString(PlatformCompatibilityAllVersions, pName));
                                        continue;
                                    }

                                    platformNames.Add(EncloseWithQuotes(pName));
                                }
                                else
                                {
                                    if ((attribute.SupportedFirst == null || !IsEmptyVersion(attribute.SupportedFirst)) &&
                                        (calledUnsupported == null || calledUnsupported < unsupportedVersion))
                                    {
                                        callsite = Callsite.Reachable;
                                        platformNames.Add(GetFormattedString(PlatformCompatibilityVersionAndBefore,
                                            pName, unsupportedVersion));
                                    }
                                    else
                                    {
                                        platformNames.Add(GetFormattedString(PlatformCompatibilityVersionAndLater,
                                            pName, unsupportedVersion));
                                    }
                                }
                            }
                        }
                    }
                }

                return platformNames.ToImmutable();
            }

            static string GetFormattedString(string resource, string platformName, object? arg1 = null, object? arg2 = null) =>
                string.Format(CultureInfo.InvariantCulture, resource, AddOsxIfMacOS(platformName), arg1, arg2);

            static string AddOsxIfMacOS(string platformName) =>
                platformName.Equals(macOS, StringComparison.OrdinalIgnoreCase) ? MacSlashOSX : platformName;

            static string EncloseWithQuotes(string pName) => $"'{AddOsxIfMacOS(pName)}'";

            static string JoinNames(ImmutableArray<string> platformNames)
            {
                platformNames = platformNames.Sort(StringComparer.OrdinalIgnoreCase);
                return string.Join(CommaSeparator, platformNames);
            }

            static SymbolDisplayFormat GetLanguageSpecificFormat(IOperation operation) =>
                operation.Language == LanguageNames.CSharp ? SymbolDisplayFormat.CSharpShortErrorMessageFormat : SymbolDisplayFormat.VisualBasicShortErrorMessageFormat;

            static bool HasSameVersionedPlatformSupport(SmallDictionary<string, Versions> attributes, string pName, bool checkSupport)
            {
                if (attributes.TryGetValue(pName, out var attribute))
                {
                    var version = attribute.UnsupportedSecond ?? attribute.UnsupportedFirst;
                    if (checkSupport)
                    {
                        var supportedVersion = attribute.SupportedSecond ?? attribute.SupportedFirst;
                        if (supportedVersion != null)
                        {
                            version = supportedVersion.IsGreaterThanOrEqualTo(version) ? supportedVersion : version;
                        }
                    }

                    if (version != null && !IsEmptyVersion(version))
                    {
                        return true;
                    }
                }

                return false;
            }

            static bool HasVersionedCallsite(SmallDictionary<string, Versions> csAttributes, string pName)
            {
                if (csAttributes.TryGetValue(pName, out var attribute))
                {
                    var version = attribute.Obsoleted;
                    var supportedVersion = attribute.SupportedSecond ?? attribute.SupportedFirst;
                    if (supportedVersion != null)
                    {
                        version = supportedVersion.IsGreaterThanOrEqualTo(version) ? supportedVersion : version;
                    }

                    if (version != null && !IsEmptyVersion(version))
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        private enum Callsite
        {
            AllPlatforms,
            Reachable,
            Unreachable,
            Empty
        }

        private static ISymbol? GetOperationSymbol(IOperation operation)
            => operation switch
            {
                IInvocationOperation iOperation => iOperation.TargetMethod,
                IObjectCreationOperation cOperation => cOperation.Constructor,
                IFieldReferenceOperation fOperation => IsWithinConditionalOperation(fOperation) ? null : fOperation.Field,
                IMemberReferenceOperation mOperation => mOperation.Member,
                _ => null,
            };

        private static IEnumerable<ISymbol> GetPropertyAccessors(IPropertySymbol property, IOperation operation)
        {
            var usageInfo = operation.GetValueUsageInfo(property.ContainingSymbol);

            // not checking/using ValueUsageInfo.Reference related values as property cannot be used as ref or out parameter
            // not using ValueUsageInfo.Name too, it only use name of the property
            if (usageInfo == ValueUsageInfo.ReadWrite)
            {
                yield return property.GetMethod!;
                yield return property.SetMethod!;
            }
            else if (usageInfo.IsWrittenTo())
            {
                yield return property.SetMethod!;
            }
            else if (usageInfo.IsReadFrom())
            {
                yield return property.GetMethod!;
            }
            else
            {
                yield return property;
            }
        }

        private static ISymbol GetEventAccessor(IEventSymbol iEvent, IOperation operation)
        {
            if (operation.Parent is IEventAssignmentOperation eventAssignment)
            {
                if (eventAssignment.Adds)
                    return iEvent.AddMethod!;
                else
                    return iEvent.RemoveMethod!;
            }

            return iEvent;
        }

        private static void AnalyzeOperation(IOperation operation, OperationAnalysisContext context, PooledConcurrentDictionary<KeyValuePair<IOperation, ISymbol>,
            (SmallDictionary<string, Versions> attributes, SmallDictionary<string, Versions>? csAttributes)> platformSpecificOperations,
             ConcurrentDictionary<ISymbol, PlatformAttributes> platformSpecificMembers, ImmutableArray<string> msBuildPlatforms,
             ITypeSymbol? notSupportedExceptionType, bool crossPlatform, SmallDictionary<string, (string relatedPlatform, bool isSubset)> relatedPlatforms)
        {
            if (operation.Parent is IArgumentOperation argumentOperation && UsedInCreatingNotSupportedException(argumentOperation, notSupportedExceptionType))
            {
                return;
            }

            var symbol = GetOperationSymbol(operation);

            if (symbol == null || symbol is ITypeSymbol type && type.SpecialType != SpecialType.None)
            {
                return;
            }

            CheckOperationAttributes(symbol, checkParents: true);

            if (symbol is IPropertySymbol property)
            {
                foreach (var accessor in GetPropertyAccessors(property, operation))
                {
                    if (accessor != null)
                    {
                        CheckOperationAttributes(accessor, checkParents: false);
                    }
                }
            }
            else if (symbol is IEventSymbol iEvent)
            {
                var accessor = GetEventAccessor(iEvent, operation);

                if (accessor != null)
                {
                    CheckOperationAttributes(accessor, checkParents: false);
                }
            }
            else if (symbol is IMethodSymbol method && method.IsGenericMethod)
            {
                CheckTypeArguments(method.TypeArguments);
            }

            if (symbol.ContainingSymbol is INamedTypeSymbol namedType && namedType.IsGenericType)
            {
                CheckTypeArguments(namedType.TypeArguments);
            }

            void CheckTypeArguments(ImmutableArray<ITypeSymbol> typeArguments)
            {
                using var workingSet = PooledHashSet<ITypeSymbol>.GetInstance();
                CheckTypeArgumentsCore(typeArguments, workingSet);
            }

            void CheckTypeArgumentsCore(ImmutableArray<ITypeSymbol> typeArguments, PooledHashSet<ITypeSymbol> workingSet)
            {
                foreach (var typeArgument in typeArguments)
                {
                    if (workingSet.Add(typeArgument))
                    {
                        if (typeArgument.SpecialType == SpecialType.None)
                        {
                            CheckOperationAttributes(typeArgument, checkParents: true);

                            if (typeArgument is INamedTypeSymbol nType && nType.IsGenericType)
                            {
                                CheckTypeArgumentsCore(nType.TypeArguments, workingSet);
                            }
                        }
                    }
                }
            }

            void CheckOperationAttributes(ISymbol symbol, bool checkParents)
            {
                if (TryGetOrCreatePlatformAttributes(symbol, checkParents, crossPlatform, platformSpecificMembers, relatedPlatforms, out var operationAttributes))
                {
                    var containingSymbol = context.ContainingSymbol;
                    if (containingSymbol is IMethodSymbol method && method.IsAccessorMethod())
                    {
                        containingSymbol = method.AssociatedSymbol!;
                    }

                    if (TryGetOrCreatePlatformAttributes(containingSymbol, true, crossPlatform, platformSpecificMembers, relatedPlatforms, out var callSiteAttributes))
                    {
                        if (callSiteAttributes.Callsite != Callsite.Empty &&
                            IsNotSuppressedByCallSite(operationAttributes.Platforms!, callSiteAttributes.Platforms!, msBuildPlatforms,
                                out var notSuppressedAttributes, crossPlatform & operationAttributes.IsAssemblyAttribute))
                        {
                            platformSpecificOperations.TryAdd(new KeyValuePair<IOperation, ISymbol>(operation, symbol), (notSuppressedAttributes, callSiteAttributes.Platforms));
                        }
                    }
                    else if (!OperationHasOnlyAssemblyAttributesAndCalledFromSameAssembly(operationAttributes, symbol, containingSymbol) &&
                             TryCopyAttributesNotSuppressedByMsBuild(operationAttributes.Platforms!, msBuildPlatforms, out var copiedAttributes))
                    {
                        platformSpecificOperations.TryAdd(new KeyValuePair<IOperation, ISymbol>(operation, symbol), (copiedAttributes, null));
                    }
                }
            }
        }

        private static bool OperationHasOnlyAssemblyAttributesAndCalledFromSameAssembly(PlatformAttributes operationAttributes, ISymbol symbol, ISymbol containingSymbol) =>
            operationAttributes.IsAssemblyAttribute && containingSymbol.ContainingAssembly == symbol.ContainingAssembly;

        private static bool UsedInCreatingNotSupportedException(IArgumentOperation operation, ITypeSymbol? notSupportedExceptionType)
        {
            if (operation.Parent is IObjectCreationOperation creation &&
                operation.Parameter?.Type.SpecialType == SpecialType.System_String &&
                creation.Type.DerivesFrom(notSupportedExceptionType, baseTypesOnly: true, checkTypeParameterConstraints: false))
            {
                return true;
            }

            return false;
        }

        private static bool TryCopyAttributesNotSuppressedByMsBuild(SmallDictionary<string, Versions> operationAttributes,
            ImmutableArray<string> msBuildPlatforms, out SmallDictionary<string, Versions> copiedAttributes)
        {
            copiedAttributes = new SmallDictionary<string, Versions>(StringComparer.OrdinalIgnoreCase);
            foreach (var (platformName, attributes) in operationAttributes)
            {
                if (AllowList(attributes) || msBuildPlatforms.Contains(platformName, StringComparer.OrdinalIgnoreCase))
                {
                    copiedAttributes.Add(platformName, CopyAllAttributes(new Versions(), attributes));
                }
            }

            return !copiedAttributes.IsEmpty;
        }

        private static PlatformAttributes CopyAttributes(PlatformAttributes copyAttributes)
        {
            var copy = new PlatformAttributes(copyAttributes.Callsite, new SmallDictionary<string, Versions>(StringComparer.OrdinalIgnoreCase));

            foreach (var (platformName, attributes) in copyAttributes.Platforms!)
            {
                copy.Platforms!.Add(platformName, CopyAllAttributes(new Versions(), attributes));
            }

            copy.IsAssemblyAttribute = copyAttributes.IsAssemblyAttribute;
            return copy;
        }

        private static SmallDictionary<string, Versions> CopyAttributes(SmallDictionary<string, Versions> copyAttributes)
        {
            var copy = new SmallDictionary<string, Versions>(StringComparer.OrdinalIgnoreCase);

            foreach (var (platformName, attributes) in copyAttributes!)
            {
                copy.Add(platformName, CopyAllAttributes(new Versions(), attributes));
            }

            return copy;
        }

        /// <summary>
        /// Checks if API attributes suppressed by call site attribute. For examble if windows only API is called within call site attributes as windows only then that call will be suppressed
        /// The semantics of the platform specific attributes are :
        ///    - An API that doesn't have any of these attributes is considered supported by all platforms.
        ///    - If either [SupportedOSPlatform] or [UnsupportedOSPlatform] attributes are present, we group all attributes by OS platform identifier:
        ///        - Allow list.If the lowest version for each OS platform is a [SupportedOSPlatform] attribute, the API
        ///          is considered to only be supported by the listed platforms and unsupported by all other platforms.
        ///        - Deny list. If the lowest version for each OS platform is a [UnsupportedOSPlatform] attribute, then the
        ///          API is considered to only be unsupported by the listed platforms and supported by all other platforms.
        ///        - Inconsistent list. If for some platforms the lowest version attribute is [SupportedOSPlatform] while for others it is [UnsupportedOSPlatform],
        ///          we will add another analyzer to produce a warning on the API definition in such scenario because the API is attributed inconsistently.
        ///    - Both attributes can be instantiated without version numbers. This means the version number is assumed to be 0.0. This simplifies guard clauses, see examples below for more details.
        /// </summary>
        /// <param name="operationAttributes">Platform specific attributes applied to the invoked member</param>
        /// <param name="callSiteAttributes">Platform specific attributes applied to the call site where the member invoked</param>
        /// <param name="msBuildPlatforms">Supported platform names provided by MSBuild, used for deciding if we need to flag for Deny list (Unssuported) attributes</param>
        /// <param name="notSuppressedAttributes"> Out parameter will include all attributes not suppressed by call site</param>
        /// <returns>true if all attributes applied to the operation is suppressed, false otherwise</returns>

        private static bool IsNotSuppressedByCallSite(SmallDictionary<string, Versions> operationAttributes,
            SmallDictionary<string, Versions> callSiteAttributes, ImmutableArray<string> msBuildPlatforms,
            out SmallDictionary<string, Versions> notSuppressedAttributes, bool crossPlatform)
        {
            notSuppressedAttributes = new SmallDictionary<string, Versions>(StringComparer.OrdinalIgnoreCase);
            bool? mandatorySupportFound = null;
            using var supportedOnlyPlatforms = PooledHashSet<string>.GetInstance(StringComparer.OrdinalIgnoreCase);
            foreach (var (platformName, attribute) in operationAttributes)
            {
                var diagnosticAttribute = new Versions();

                if (attribute.SupportedFirst != null)
                {
                    if (attribute.UnsupportedFirst == null || attribute.UnsupportedFirst > attribute.SupportedFirst)
                    {
                        // If only supported for current platform
                        supportedOnlyPlatforms.Add(platformName);
                        mandatorySupportFound ??= false;

                        if (callSiteAttributes.TryGetValue(platformName, out var callSiteAttribute))
                        {
                            var attributeToCheck = attribute.SupportedSecond ?? attribute.SupportedFirst;
                            if ((MandatoryOsVersionsSuppressed(callSiteAttribute, attributeToCheck) || crossPlatform) && AllowList(callSiteAttribute))
                            {
                                mandatorySupportFound = true;
                            }
                            else
                            {
                                diagnosticAttribute.SupportedFirst = (Version)attributeToCheck.Clone();
                            }

                            if (attribute.UnsupportedFirst != null &&
                                (!mandatorySupportFound.Value ||
                                  !(SuppressedByCallSiteSupported(attribute, callSiteAttribute.SupportedFirst) ||
                                  SuppressedByCallSiteUnsupported(callSiteAttribute, attribute.UnsupportedFirst))))
                            {
                                diagnosticAttribute.UnsupportedFirst = (Version)attribute.UnsupportedFirst.Clone();
                                diagnosticAttribute.UnsupportedMessage = attribute.UnsupportedMessage;
                            }
                        }
                    }
                    else if (attribute.UnsupportedFirst != null) // also means Unsupported < Supported, deny list
                    {
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
                                    diagnosticAttribute.UnsupportedMessage = attribute.UnsupportedMessage;
                                }

                                if (attribute.UnsupportedSecond != null &&
                                    !UnsupportedSecondSuppressed(attribute, callSiteAttribute))
                                {
                                    diagnosticAttribute.UnsupportedSecond = (Version)attribute.UnsupportedSecond.Clone();
                                    diagnosticAttribute.UnsupportedMessage = attribute.UnsupportedMessage;
                                }
                            }
                            else if (msBuildPlatforms.Contains(platformName, StringComparer.OrdinalIgnoreCase))
                            {
                                if (!SuppressedByCallSiteUnsupported(callSiteAttribute, attribute.UnsupportedFirst))
                                {
                                    diagnosticAttribute.SupportedFirst = (Version)attribute.SupportedFirst.Clone();
                                    diagnosticAttribute.UnsupportedFirst = (Version)attribute.UnsupportedFirst.Clone();
                                    diagnosticAttribute.UnsupportedMessage = attribute.UnsupportedMessage;
                                }

                                if (attribute.UnsupportedSecond != null && !SuppressedByCallSiteUnsupported(callSiteAttribute, attribute.UnsupportedSecond))
                                {
                                    diagnosticAttribute.SupportedFirst = (Version)attribute.SupportedFirst.Clone();
                                    diagnosticAttribute.UnsupportedSecond = (Version)attribute.UnsupportedSecond.Clone();
                                    diagnosticAttribute.UnsupportedMessage = attribute.UnsupportedMessage;
                                }
                            }
                        }
                        // Call site has no attributes for this platform, check if MsBuild list has it,
                        // then if call site has deny list, it should support its later support
                        else if (msBuildPlatforms.Contains(platformName, StringComparer.OrdinalIgnoreCase) &&
                                callSiteAttributes.Any(ca => DenyList(ca.Value)))
                        {
                            diagnosticAttribute.SupportedFirst = (Version)attribute.SupportedFirst.Clone();
                        }
                    }
                }
                else if (attribute.UnsupportedFirst != null) // Unsupported for this but supported all other
                {
                    if (callSiteAttributes.TryGetValue(platformName, out var callSiteAttribute))
                    {
                        if (callSiteAttribute.SupportedFirst != null)
                        {
                            if (callSiteAttribute.UnsupportedFirst != null)
                            {
                                if (!SuppressedByCallSiteUnsupported(callSiteAttribute, attribute.UnsupportedFirst))
                                {
                                    diagnosticAttribute.UnsupportedFirst = (Version)attribute.UnsupportedFirst.Clone();
                                    diagnosticAttribute.UnsupportedMessage = attribute.UnsupportedMessage;
                                }
                                else if (DenyList(callSiteAttribute))
                                {
                                    diagnosticAttribute.UnsupportedFirst = (Version)attribute.UnsupportedFirst.Clone();
                                    diagnosticAttribute.UnsupportedMessage = attribute.UnsupportedMessage;
                                }
                            }
                            else
                            {
                                diagnosticAttribute.UnsupportedFirst = (Version)attribute.UnsupportedFirst.Clone();
                                diagnosticAttribute.UnsupportedMessage = attribute.UnsupportedMessage;
                            }
                        }
                        else if (msBuildPlatforms.Contains(platformName, StringComparer.OrdinalIgnoreCase) &&
                                !SuppressedByCallSiteUnsupported(callSiteAttribute, attribute.UnsupportedFirst))
                        {
                            diagnosticAttribute.UnsupportedFirst = (Version)attribute.UnsupportedFirst.Clone();
                            diagnosticAttribute.UnsupportedMessage = attribute.UnsupportedMessage;
                        }
                    }
                    else if (msBuildPlatforms.Contains(platformName, StringComparer.OrdinalIgnoreCase) &&
                             !callSiteAttributes.Values.Any(v => v.SupportedFirst != null))
                    {
                        // if MsBuild list contain the platform and call site has no any other supported attribute it means global, so need to warn
                        diagnosticAttribute.UnsupportedFirst = (Version)attribute.UnsupportedFirst.Clone();
                        diagnosticAttribute.UnsupportedMessage = attribute.UnsupportedMessage;
                    }
                }

                // Check if obsoleted attribute guarded by callsite attributes
                if (attribute.Obsoleted != null)
                {
                    if (callSiteAttributes.TryGetValue(platformName, out var callSiteAttribute))
                    {
                        if (callSiteAttribute.SupportedFirst != null)
                        {
                            if ((callSiteAttribute.Obsoleted == null || callSiteAttribute.Obsoleted > attribute.Obsoleted) &&
                                (callSiteAttribute.UnsupportedFirst == null || callSiteAttribute.UnsupportedFirst > attribute.Obsoleted))
                            {
                                diagnosticAttribute.Obsoleted = (Version)attribute.Obsoleted.Clone();
                                diagnosticAttribute.ObsoletedMessage = attribute.ObsoletedMessage;
                                diagnosticAttribute.ObsoletedUrl = attribute.ObsoletedUrl;
                            }
                        }
                        else if (msBuildPlatforms.Contains(platformName, StringComparer.OrdinalIgnoreCase) &&
                                 (callSiteAttribute.UnsupportedFirst != null && callSiteAttribute.UnsupportedFirst > attribute.Obsoleted ||
                                  callSiteAttribute.Obsoleted != null && callSiteAttribute.Obsoleted > attribute.Obsoleted))
                        {
                            diagnosticAttribute.Obsoleted = (Version)attribute.Obsoleted.Clone();
                            diagnosticAttribute.ObsoletedMessage = attribute.ObsoletedMessage;
                            diagnosticAttribute.ObsoletedUrl = attribute.ObsoletedUrl;
                        }
                    }
                    else if (msBuildPlatforms.Contains(platformName, StringComparer.OrdinalIgnoreCase) &&
                             !callSiteAttributes.Values.Any(AllowList))
                    {
                        diagnosticAttribute.Obsoleted = (Version)attribute.Obsoleted.Clone();
                        diagnosticAttribute.ObsoletedMessage = attribute.ObsoletedMessage;
                        diagnosticAttribute.ObsoletedUrl = attribute.ObsoletedUrl;
                    }
                }

                if (diagnosticAttribute.IsSet())
                {
                    notSuppressedAttributes[platformName] = diagnosticAttribute;
                }
            }

            if (mandatorySupportFound.HasValue)
            {
                if (!mandatorySupportFound.Value)
                {
                    foreach (var (name, attributes) in operationAttributes)
                    {
                        if (attributes.SupportedFirst != null &&
                            !notSuppressedAttributes.TryGetValue(name, out var diagnosticAttribute))
                        {
                            diagnosticAttribute = new Versions();
                            CopyAllAttributes(diagnosticAttribute, attributes);
                            notSuppressedAttributes[name] = diagnosticAttribute;
                        }
                    }
                }
                else if (!crossPlatform)
                {
                    // if supportedOnlyList then call site should not have any platform not listed in the support list
                    foreach (var (platform, csAttributes) in callSiteAttributes)
                    {
                        if (csAttributes.SupportedFirst != null &&
                            !supportedOnlyPlatforms.Contains(platform) &&
                            !notSuppressedAttributes.ContainsKey(platform))
                        {
                            foreach (var (name, version) in operationAttributes)
                            {
                                AddOrUpdatedDiagnostic(version, notSuppressedAttributes, name);
                            }
                        }
                    }
                }
            }

            return !notSuppressedAttributes.IsEmpty;

            static void AddOrUpdatedDiagnostic(Versions operationAttributes,
                SmallDictionary<string, Versions> notSuppressedAttributes, string name)
            {
                if (operationAttributes.SupportedFirst != null)
                {
                    if (!notSuppressedAttributes.TryGetValue(name, out var diagnosticAttribute))
                    {
                        diagnosticAttribute = new Versions();
                    }

                    diagnosticAttribute.SupportedFirst = (Version)operationAttributes.SupportedFirst.Clone();
                    notSuppressedAttributes[name] = diagnosticAttribute;
                }
            }

            static bool UnsupportedSecondSuppressed(Versions attribute, Versions callSiteAttribute) =>
                SuppressedByCallSiteSupported(attribute, callSiteAttribute.SupportedFirst) ||
                SuppressedByCallSiteUnsupported(callSiteAttribute, attribute.UnsupportedSecond!);

            static bool SuppressedByCallSiteUnsupported(Versions callSiteAttribute, Version unsupporteAttribute) =>
                DenyList(callSiteAttribute) && callSiteAttribute.SupportedFirst != null ?
                callSiteAttribute.UnsupportedSecond != null && unsupporteAttribute.IsGreaterThanOrEqualTo(callSiteAttribute.UnsupportedSecond) :
                callSiteAttribute.UnsupportedFirst != null && unsupporteAttribute.IsGreaterThanOrEqualTo(callSiteAttribute.UnsupportedFirst);

            static bool SuppressedByCallSiteSupported(Versions attribute, Version? callSiteSupportedFirst) =>
                callSiteSupportedFirst != null && callSiteSupportedFirst.IsGreaterThanOrEqualTo(attribute.SupportedFirst) &&
                attribute.SupportedSecond != null && callSiteSupportedFirst.IsGreaterThanOrEqualTo(attribute.SupportedSecond);

            static bool UnsupportedFirstSuppressed(Versions attribute, Versions callSiteAttribute) =>
                callSiteAttribute.SupportedFirst != null && callSiteAttribute.SupportedFirst.IsGreaterThanOrEqualTo(attribute.SupportedFirst) ||
                SuppressedByCallSiteUnsupported(callSiteAttribute, attribute.UnsupportedFirst!);

            // As optianal if call site supports that platform, their versions should match
            static bool OptionalOsSupportSuppressed(Versions callSiteAttribute, Versions attribute) =>
                (callSiteAttribute.SupportedFirst == null || callSiteAttribute.SupportedFirst.IsGreaterThanOrEqualTo(attribute.SupportedFirst)) &&
                (callSiteAttribute.SupportedSecond == null || callSiteAttribute.SupportedSecond.IsGreaterThanOrEqualTo(attribute.SupportedFirst));

            static bool MandatoryOsVersionsSuppressed(Versions callSitePlatforms, Version checkingVersion) =>
                callSitePlatforms.SupportedFirst != null && callSitePlatforms.SupportedFirst.IsGreaterThanOrEqualTo(checkingVersion) ||
                callSitePlatforms.SupportedSecond != null && callSitePlatforms.SupportedSecond.IsGreaterThanOrEqualTo(checkingVersion);
        }

        private static Versions CopyAllAttributes(Versions copyTo, Versions copyFrom)
        {
            copyTo.SupportedFirst = (Version?)copyFrom.SupportedFirst?.Clone();
            copyTo.SupportedSecond = (Version?)copyFrom.SupportedSecond?.Clone();
            copyTo.UnsupportedFirst = (Version?)copyFrom.UnsupportedFirst?.Clone();
            copyTo.UnsupportedSecond = (Version?)copyFrom.UnsupportedSecond?.Clone();
            copyTo.UnsupportedMessage = copyFrom.UnsupportedMessage;
            copyTo.Obsoleted = (Version?)copyFrom.Obsoleted?.Clone();
            copyTo.ObsoletedMessage = copyFrom.ObsoletedMessage;
            copyTo.ObsoletedUrl = copyFrom.ObsoletedUrl;
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

        private static bool TryGetOrCreatePlatformAttributes(ISymbol symbol, bool checkParents,
            bool crossPlatform, ConcurrentDictionary<ISymbol, PlatformAttributes> platformSpecificMembers,
            SmallDictionary<string, (string relatedPlatform, bool isSubset)> relatedPlatforms, out PlatformAttributes attributes)
        {
            if (!platformSpecificMembers.TryGetValue(symbol, out attributes))
            {
                if (checkParents)
                {
                    var container = symbol.ContainingSymbol;

                    // Namespaces do not have attributes
                    while (container is INamespaceSymbol)
                    {
                        container = container.ContainingSymbol;
                    }

                    if (container != null &&
                        TryGetOrCreatePlatformAttributes(container, checkParents, crossPlatform, platformSpecificMembers, relatedPlatforms, out var containerAttributes))
                    {
                        attributes = CopyAttributes(containerAttributes);
                    }
                }

                attributes ??= new PlatformAttributes() { IsAssemblyAttribute = symbol is IAssemblySymbol };
                MergePlatformAttributes(symbol.GetAttributes(), ref attributes, crossPlatform, relatedPlatforms);
                attributes = platformSpecificMembers.GetOrAdd(symbol, attributes);
            }

            return attributes.Platforms != null;

            static void MergePlatformAttributes(ImmutableArray<AttributeData> immediateAttributes, ref PlatformAttributes parentAttributes,
                bool crossPlatform, SmallDictionary<string, (string relatedPlatform, bool isSubset)> relatedPlatforms)
            {
                SmallDictionary<string, Versions>? childAttributes = null;
                foreach (AttributeData attribute in immediateAttributes)
                {
                    if (attribute.AttributeClass == null)
                        continue;

                    if (attribute.AttributeClass.Name is SupportedOSPlatformGuardAttribute or UnsupportedOSPlatformGuardAttribute)
                    {
                        parentAttributes = new PlatformAttributes(); // The API is for guard, clear parent attributes
                        return;
                    }

                    if (s_osPlatformAttributes.Contains(attribute.AttributeClass.Name))
                    {
                        TryAddValidAttribute(ref childAttributes, attribute, relatedPlatforms);
                    }
                }

                if (childAttributes == null)
                {
                    return;
                }

                CheckAttributesConsistency(childAttributes);
                var pAttributes = parentAttributes.Platforms;
                if (pAttributes != null && !pAttributes.IsEmpty)
                {
                    var notFoundPlatforms = PooledHashSet<string>.GetInstance();
                    bool supportFound = false;
                    foreach (var (platform, attributes) in pAttributes)
                    {
                        if (DenyList(attributes) &&
                            !pAttributes.Any(ca => AllowList(ca.Value)))
                        {
                            // if all are deny list then we can add the child attributes
                            foreach (var (name, childAttribute) in childAttributes)
                            {
                                if (pAttributes.TryGetValue(name, out var existing))
                                {
                                    if (childAttribute.UnsupportedFirst != null)
                                    {
                                        // but don't override existing unless narrowing the support
                                        if (childAttribute.UnsupportedFirst < existing.UnsupportedFirst)
                                        {
                                            existing.UnsupportedFirst = childAttribute.UnsupportedFirst;
                                            if (childAttribute.SupportedFirst != null && (existing.SupportedFirst == null ||
                                                childAttribute.SupportedFirst > existing.SupportedFirst))
                                            {
                                                existing.SupportedFirst = childAttribute.SupportedFirst;
                                            }
                                        }

                                        if (childAttribute.UnsupportedSecond != null && (existing.UnsupportedSecond == null ||
                                             childAttribute.UnsupportedSecond < existing.UnsupportedSecond))
                                        {
                                            existing.UnsupportedSecond = childAttribute.UnsupportedSecond;
                                        }

                                        if (existing.SupportedFirst != null &&
                                            childAttribute.SupportedFirst != null &&
                                            childAttribute.SupportedFirst > existing.SupportedFirst)
                                        {
                                            existing.SupportedFirst = childAttribute.SupportedFirst;
                                        }
                                    }

                                    if (childAttribute.Obsoleted != null &&
                                        (childAttribute.Obsoleted < existing.UnsupportedFirst ||
                                            existing.SupportedFirst != null &&
                                            (existing.UnsupportedSecond == null || existing.UnsupportedSecond > childAttribute.Obsoleted)) &&
                                        (existing.Obsoleted == null || childAttribute.Obsoleted < existing.Obsoleted))
                                    {
                                        existing.Obsoleted = childAttribute.Obsoleted;
                                        existing.ObsoletedMessage = childAttribute.ObsoletedMessage;
                                        existing.ObsoletedUrl = childAttribute.ObsoletedUrl;
                                    }
                                }
                                else
                                {
                                    pAttributes[name] = childAttribute;
                                }
                            }
                            // merged all attributes, no need to continue looping
                            return;
                        }
                        else if (AllowList(attributes))
                        {
                            // only attributes with same platform matter, could narrow the list
                            if (childAttributes.TryGetValue(platform, out var childAttribute))
                            {
                                // only later versions could narrow, other versions ignored
                                if (childAttribute.SupportedFirst.IsGreaterThanOrEqualTo(attributes.SupportedFirst) &&
                                    (attributes.SupportedSecond == null || attributes.SupportedSecond < childAttribute.SupportedFirst))
                                {
                                    attributes.SupportedSecond = childAttribute.SupportedFirst;
                                    supportFound = true;
                                }

                                if (childAttribute.UnsupportedFirst != null)
                                {
                                    if (attributes.SupportedFirst.IsGreaterThanOrEqualTo(childAttribute.UnsupportedFirst))
                                    {
                                        parentAttributes.Callsite = Callsite.Empty;
                                        attributes.SupportedFirst = childAttribute.SupportedFirst > attributes.SupportedFirst ? childAttribute.SupportedFirst : null;
                                        attributes.UnsupportedFirst = childAttribute.UnsupportedFirst;
                                        attributes.UnsupportedMessage = childAttribute.UnsupportedMessage;
                                    }
                                    else if (attributes.UnsupportedFirst == null || attributes.UnsupportedFirst > childAttribute.UnsupportedFirst)
                                    {
                                        attributes.UnsupportedFirst = childAttribute.UnsupportedFirst;
                                        attributes.UnsupportedMessage = childAttribute.UnsupportedMessage;
                                    }

                                    if (attributes.SupportedSecond.IsGreaterThanOrEqualTo(childAttribute.UnsupportedFirst))
                                    {
                                        attributes.SupportedSecond = null;
                                    }

                                    if (childAttribute.UnsupportedSecond != null && childAttribute.UnsupportedSecond > attributes.UnsupportedFirst)
                                    {
                                        attributes.UnsupportedFirst = childAttribute.UnsupportedSecond;
                                        attributes.UnsupportedMessage = childAttribute.UnsupportedMessage;
                                    }
                                }
                            }
                            else
                            {
                                // not existing parent platforms might need to be removed
                                notFoundPlatforms.Add(platform);
                            }
                        }

                        // Check for Obsoleted attributes, only lower version could overwrite
                        if (childAttributes.TryGetValue(platform, out var childAttr) &&
                            childAttr.Obsoleted != null &&
                            (attributes.Obsoleted == null || childAttr.Obsoleted < attributes.Obsoleted))
                        {
                            attributes.Obsoleted = childAttr.Obsoleted;
                            attributes.ObsoletedMessage = childAttr.ObsoletedMessage;
                            attributes.ObsoletedUrl = childAttr.ObsoletedUrl;
                        }
                    }

                    CheckAttributesConsistency(pAttributes);
                    if (crossPlatform && parentAttributes.IsAssemblyAttribute)
                    {
                        foreach (var childAttribute in childAttributes)
                        {
                            if (AllowList(childAttribute.Value))
                            {
                                parentAttributes.Platforms = childAttributes;
                                break;
                            }
                        }
                    }

                    if (notFoundPlatforms.Count > 0)
                    {
                        // For allow list if child narrowing supported platforms by having less platforms support than parent,
                        // not existing parent platforms should be removed
                        if (supportFound)
                        {
                            foreach (var platform in notFoundPlatforms)
                            {
                                parentAttributes.Platforms!.Remove(platform);
                            }
                        }
                        else
                        {
                            parentAttributes.Callsite = Callsite.Empty;
                        }
                    }

                    parentAttributes.IsAssemblyAttribute = false;
                }
                else
                {
                    pAttributes ??= new SmallDictionary<string, Versions>(StringComparer.OrdinalIgnoreCase);
                    foreach (var (platform, attributes) in childAttributes)
                    {
                        pAttributes[platform] = attributes;
                    }

                    parentAttributes.Platforms = pAttributes;
                }

                return;

                static void CheckAttributesConsistency(SmallDictionary<string, Versions> childAttributes)
                {
                    bool allowList = false;
                    using var unsupportedList = PooledHashSet<string>.GetInstance();

                    foreach (var (platform, attributes) in childAttributes)
                    {
                        NormalizeAttribute(attributes);
                        if (AllowList(attributes))
                        {
                            allowList = true;
                        }
                        else if (DenyList(attributes))
                        {
                            unsupportedList.Add(platform);
                        }
                    }

                    if (allowList && unsupportedList.Count > 0)
                    {
                        foreach (var name in unsupportedList)
                        {
                            childAttributes.Remove(name);
                        }
                    }
                }

                static Versions NormalizeAttribute(Versions attributes)
                {
                    if (AllowList(attributes))
                    {
                        // For Allow list UnsupportedSecond should not be used.
                        attributes.UnsupportedSecond = null;
                        if (attributes.UnsupportedFirst != null && attributes.UnsupportedFirst == attributes.SupportedFirst)
                        {
                            attributes.SupportedFirst = null;
                        }
                    }
                    // For deny list UnsupportedSecond should only set if there is SupportedFirst version between UnsupportedSecond and UnsupportedFirst 
                    else if (attributes.SupportedFirst == null ||
                            (attributes.UnsupportedSecond != null &&
                             attributes.SupportedFirst > attributes.UnsupportedSecond))
                    {
                        attributes.UnsupportedSecond = null;
                    }

                    return attributes;
                }
            }
        }

        private static bool TryAddValidAttribute([NotNullWhen(true)] ref SmallDictionary<string, Versions>? attributes,
            AttributeData attribute, SmallDictionary<string, (string relatedPlatform, bool isSubset)> relatedPlatforms)
        {
            if (TryParsePlatformNameAndVersion(attribute, out var platformName, out var version))
            {
                attributes ??= new SmallDictionary<string, Versions>(StringComparer.OrdinalIgnoreCase);

                if (!attributes.TryGetValue(platformName, out var _))
                {
                    attributes[platformName] = new Versions();
                }

                if (!AddAttribute(attribute, version, attributes[platformName]))
                {
                    attributes.Remove(platformName);
                }
                else if (relatedPlatforms.TryGetValue(platformName, out var relation) && relation.isSubset)
                {
                    if (!attributes.TryGetValue(relation.relatedPlatform, out var _))
                    {
                        attributes[relation.relatedPlatform] = new Versions();
                    }

                    AddAttribute(attribute, version, attributes[relation.relatedPlatform]);
                }

                return true;
            }

            return false;
        }

        private static bool TryParsePlatformNameAndVersion(AttributeData attribute, out string platformName, [NotNullWhen(true)] out Version? version)
        {
            if (HasNonEmptyStringArgument(attribute, out var argument))
            {
                return TryParsePlatformNameAndVersion(argument, out platformName, out version);
            }

            version = null;
            platformName = string.Empty;
            return false;
        }

        private static bool HasNonEmptyStringArgument(AttributeData attribute, [NotNullWhen(true)] out string? stringArgument)
        {
            if (!attribute.ConstructorArguments.IsEmpty &&
                attribute.ConstructorArguments[0] is { } argument &&
                argument.Type?.SpecialType == SpecialType.System_String &&
                !argument.IsNull &&
                argument.Value is string value &&
                !string.IsNullOrEmpty(value))
            {
                stringArgument = argument.Value.ToString();
                return true;
            }

            stringArgument = null;
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
                        osPlatformName = GetNameAsMacOsWhenOSX(osString[..i]);
                        version = parsedVersion;
                        return true;
                    }

                    return false;
                }
            }

            osPlatformName = GetNameAsMacOsWhenOSX(osString);
            version = EmptyVersion;
            return true;
        }

        private static string GetNameAsMacOsWhenOSX(string platformName) =>
            platformName.Equals(OSX, StringComparison.OrdinalIgnoreCase) ? macOS : platformName;

        private static bool AddAttribute(AttributeData attribute, Version version, Versions attributes)
        {
            var name = attribute.AttributeClass?.Name;
            if (name == SupportedOSPlatformAttribute)
            {
                if (attributes.UnsupportedFirst != null && attributes.UnsupportedFirst == version)
                {
                    attributes.UnsupportedFirst = null;
                    return false;
                }
                else
                {
                    AddOrUpdateSupportedAttribute(attributes, version);
                }
            }
            else if (name == UnsupportedOSPlatformAttribute)
            {
                if (attributes.SupportedFirst != null && attributes.SupportedFirst == version)
                {
                    attributes.SupportedFirst = null;
                }

                AddOrUpdateUnsupportedAttribute(attribute, attributes, version);
            }
            else
            {
                Debug.Assert(name == ObsoletedOSPlatformAttribute);
                AddOrUpdateObsoletedAttribute(attribute, attributes, version);
            }

            return true;

            static void AddOrUpdateObsoletedAttribute(AttributeData attribute, Versions attributes, Version version)
            {
                var message = PopulateMessage(attribute);
                var url = PopulateUrl(attribute);

                if (attributes.Obsoleted != null)
                {
                    // only keep lowest version, ignore other versions
                    if (attributes.Obsoleted > version)
                    {
                        attributes.Obsoleted = version;
                        attributes.ObsoletedMessage = message;
                        attributes.ObsoletedUrl = url;
                    }
                }
                else
                {
                    attributes.Obsoleted = version;
                    attributes.ObsoletedMessage = message;
                    attributes.ObsoletedUrl = url;
                }
            }

            static void AddOrUpdateUnsupportedAttribute(AttributeData attribute, Versions attributes, Version version)
            {
                var message = PopulateMessage(attribute);
                if (attributes.UnsupportedFirst != null)
                {
                    if (attributes.UnsupportedFirst > version)
                    {
                        attributes.UnsupportedSecond = attributes.UnsupportedFirst;
                        attributes.UnsupportedFirst = version;
                        attributes.UnsupportedMessage = message;
                    }
                    else if (attributes.UnsupportedSecond == null ||
                            attributes.UnsupportedSecond > version)
                    {
                        attributes.UnsupportedSecond = version;
                        attributes.UnsupportedMessage = message;
                    }
                }
                else
                {
                    attributes.UnsupportedFirst = version;
                    attributes.UnsupportedMessage = message;
                }
            }

            static void AddOrUpdateSupportedAttribute(Versions attributes, Version version)
            {
                if (attributes.SupportedFirst != null)
                {
                    // only keep lowest version, ignore other versions
                    if (attributes.SupportedFirst > version)
                    {
                        attributes.SupportedFirst = version;
                    }
                }
                else
                {
                    attributes.SupportedFirst = version;
                }
            }
        }

        private static string? PopulateMessage(AttributeData attribute)
        {
            if (attribute.ConstructorArguments.Length == 2)
            {
                return attribute.ConstructorArguments[1].Value?.ToString();
            }

            return null;
        }

#pragma warning disable CA1055 // URI-like return values should not be strings - https://github.com/dotnet/roslyn-analyzers/issues/6379
        private static string? PopulateUrl(AttributeData attribute)
#pragma warning restore CA1055 // URI-like return values should not be strings
        {
            if (attribute.NamedArguments.Length == 1 && attribute.NamedArguments[0].Key is "Url")
            {
                return attribute.NamedArguments[0].Value.Value?.ToString();
            }

            return null;
        }

        /// <summary>
        /// Determines if the attributes supported only for the platform (allow list)
        /// </summary>
        /// <param name="attributes">PlatformAttributes being checked</param>
        /// <returns>true if it is allow list</returns>
        private static bool AllowList(Versions attributes) =>
            attributes.SupportedFirst != null &&
            (attributes.UnsupportedFirst == null || attributes.UnsupportedFirst.IsGreaterThanOrEqualTo(attributes.SupportedFirst));

        /// <summary>
        /// Determines if the attributes unsupported only for the platform (deny list)
        /// </summary>
        /// <param name="attributes">PlatformAttributes being checked</param>
        /// <returns>true if it is deny list</returns>
        private static bool DenyList(Versions attributes) =>
            attributes.UnsupportedFirst != null &&
            (attributes.SupportedFirst == null || attributes.UnsupportedFirst < attributes.SupportedFirst);
    }
}
