// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1420: <inheritdoc cref="FeatureUnsupportedWhenRuntimeMarshallingDisabledTitle"/>
    /// CA1421: <inheritdoc cref="MethodUsesRuntimeMarshallingEvenWhenMarshallingDisabledTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    internal sealed partial class DisableRuntimeMarshallingAnalyzer : DiagnosticAnalyzer
    {
        internal const string FeatureUnsupportedWhenRuntimeMarshallingDisabledId = "CA1420";

        private static readonly DiagnosticDescriptor FeatureUnsupportedWhenRuntimeMarshallingDisabledSetLastErrorTrue =
            DiagnosticDescriptorHelper.Create(
                FeatureUnsupportedWhenRuntimeMarshallingDisabledId,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledTitle)),
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledMessageSetLastError)),
                DiagnosticCategory.Interoperability,
                RuleLevel.BuildWarning,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        private static readonly DiagnosticDescriptor FeatureUnsupportedWhenRuntimeMarshallingDisabledHResultSwapping =
            DiagnosticDescriptorHelper.Create(
                FeatureUnsupportedWhenRuntimeMarshallingDisabledId,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledTitle)),
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledMessageHResultSwapping)),
                DiagnosticCategory.Interoperability,
                RuleLevel.BuildWarning,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        private static readonly DiagnosticDescriptor FeatureUnsupportedWhenRuntimeMarshallingDisabledUsingLCIDConversionAttribute =
            DiagnosticDescriptorHelper.Create(
                FeatureUnsupportedWhenRuntimeMarshallingDisabledId,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledTitle)),
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledMessageLCIDConversionAttribute)),
                DiagnosticCategory.Interoperability,
                RuleLevel.BuildWarning,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        private static readonly DiagnosticDescriptor FeatureUnsupportedWhenRuntimeMarshallingDisabledVarargPInvokes =
            DiagnosticDescriptorHelper.Create(
                FeatureUnsupportedWhenRuntimeMarshallingDisabledId,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledTitle)),
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledMessageVarargPInvokes)),
                DiagnosticCategory.Interoperability,
                RuleLevel.BuildWarning,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        private static readonly DiagnosticDescriptor FeatureUnsupportedWhenRuntimeMarshallingDisabledByRefParameters =
            DiagnosticDescriptorHelper.Create(
                FeatureUnsupportedWhenRuntimeMarshallingDisabledId,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledTitle)),
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledMessageByRefParameters)),
                DiagnosticCategory.Interoperability,
                RuleLevel.BuildWarning,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        private static readonly DiagnosticDescriptor FeatureUnsupportedWhenRuntimeMarshallingDisabledManagedParameterOrReturnTypes =
            DiagnosticDescriptorHelper.Create(
                FeatureUnsupportedWhenRuntimeMarshallingDisabledId,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledTitle)),
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledMessageManagedParameterOrReturnTypes)),
                DiagnosticCategory.Interoperability,
                RuleLevel.BuildWarning,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        private static readonly DiagnosticDescriptor FeatureUnsupportedWhenRuntimeMarshallingDisabledAutoLayoutTypes =
            DiagnosticDescriptorHelper.Create(
                FeatureUnsupportedWhenRuntimeMarshallingDisabledId,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledTitle)),
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledMessageAutoLayoutTypes)),
                DiagnosticCategory.Interoperability,
                RuleLevel.BuildWarning,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        private static readonly DiagnosticDescriptor FeatureUnsupportedWhenRuntimeMarshallingDisabledDelegateUsage =
            DiagnosticDescriptorHelper.Create(
                FeatureUnsupportedWhenRuntimeMarshallingDisabledId,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledTitle)),
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledMessageDelegateUsage)),
                DiagnosticCategory.Interoperability,
                RuleLevel.BuildWarning,
                CreateLocalizableResourceString(nameof(FeatureUnsupportedWhenRuntimeMarshallingDisabledDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        internal const string MethodUsesRuntimeMarshallingEvenWhenMarshallingDisabledId = "CA1421";

        private static readonly DiagnosticDescriptor MethodUsesRuntimeMarshallingEvenWhenMarshallingDisabled =
            DiagnosticDescriptorHelper.Create(
                MethodUsesRuntimeMarshallingEvenWhenMarshallingDisabledId,
                CreateLocalizableResourceString(nameof(MethodUsesRuntimeMarshallingEvenWhenMarshallingDisabledTitle)),
                CreateLocalizableResourceString(nameof(MethodUsesRuntimeMarshallingEvenWhenMarshallingDisabledMessage)),
                DiagnosticCategory.Interoperability,
                RuleLevel.IdeSuggestion,
                CreateLocalizableResourceString(nameof(MethodUsesRuntimeMarshallingEvenWhenMarshallingDisabledDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        public const string CanConvertToDisabledMarshallingEquivalentKey = nameof(CanConvertToDisabledMarshallingEquivalentKey);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            FeatureUnsupportedWhenRuntimeMarshallingDisabledSetLastErrorTrue,
            FeatureUnsupportedWhenRuntimeMarshallingDisabledHResultSwapping,
            FeatureUnsupportedWhenRuntimeMarshallingDisabledUsingLCIDConversionAttribute,
            FeatureUnsupportedWhenRuntimeMarshallingDisabledVarargPInvokes,
            FeatureUnsupportedWhenRuntimeMarshallingDisabledByRefParameters,
            FeatureUnsupportedWhenRuntimeMarshallingDisabledManagedParameterOrReturnTypes,
            FeatureUnsupportedWhenRuntimeMarshallingDisabledAutoLayoutTypes,
            MethodUsesRuntimeMarshallingEvenWhenMarshallingDisabled);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            // Report diagnostics in generated code to enable this analyzer to catch usages of
            // delegates with non-blittable parameters that do not have [UnmanagedFunctionPointer] in source-generated interop.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterCompilationStartAction(context =>
            {
                if (context.Compilation.TryGetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.SystemRuntimeCompilerServicesDisableRuntimeMarshallingAttribute,
                    out INamedTypeSymbol? disableRuntimeMarshallingAttribute))
                {
                    AutoLayoutTypeCache autoLayoutCache = new(context.Compilation);
                    var hasDisableRuntimeMarshallingAttribute = context.Compilation.Assembly.HasAnyAttribute(disableRuntimeMarshallingAttribute);
                    if (hasDisableRuntimeMarshallingAttribute)
                    {
                        var disabledRuntimeMarshallingAssemblyAnalyzer = new DisabledRuntimeMarshallingAssemblyAnalyzer(context.Compilation, autoLayoutCache);
                        disabledRuntimeMarshallingAssemblyAnalyzer.RegisterActions(context);
                    }

                    var delegateInteropUsageAnalyzer = new DelegateInteropUsageAnalyzer(context.Compilation, autoLayoutCache, disableRuntimeMarshallingAttribute);
                    delegateInteropUsageAnalyzer.RegisterActions(context, hasDisableRuntimeMarshallingAttribute);
                }
            });
        }

        private static void AnalyzeMethodSignature(AutoLayoutTypeCache autoLayoutCache, Action<Diagnostic> reportDiagnostic, IMethodSymbol method, ImmutableArray<Location> locationsOverride = default, DiagnosticDescriptor? descriptorOverride = null)
        {
            AnalyzeSignatureType(locationsOverride.IsDefaultOrEmpty ? method.Locations : locationsOverride, method.ReturnType);
            foreach (var param in method.Parameters)
            {
                var paramLocation = locationsOverride.IsDefaultOrEmpty ? param.Locations : locationsOverride;
                if (param.RefKind != RefKind.None)
                {
                    reportDiagnostic(paramLocation.CreateDiagnostic(descriptorOverride ?? FeatureUnsupportedWhenRuntimeMarshallingDisabledByRefParameters));
                }

                AnalyzeSignatureType(paramLocation, param.Type);
            }

            void AnalyzeSignatureType(ImmutableArray<Location> locations, ITypeSymbol type)
            {
                if (type.SpecialType == SpecialType.System_Void)
                {
                    return;
                }

                if (type.Language == LanguageNames.CSharp)
                {
                    if (!type.IsUnmanagedType)
                    {
                        reportDiagnostic(locations.CreateDiagnostic(descriptorOverride ?? FeatureUnsupportedWhenRuntimeMarshallingDisabledManagedParameterOrReturnTypes));
                    }
                }
                // For non-C# languages, we'll do a quick check to catch simple cases
                // since IsUnmanagedType only works in languages that support unmanaged types
                // and non-C# languages that might not support is (such as VB) aren't a big focus of the attribute
                // this analyzer validates.
                else if (type.IsReferenceType || type.GetMembers().Any(m => m is IFieldSymbol { IsStatic: false, Type.IsReferenceType: true }))
                {
                    reportDiagnostic(locations.CreateDiagnostic(descriptorOverride ?? FeatureUnsupportedWhenRuntimeMarshallingDisabledManagedParameterOrReturnTypes));
                }

                if (type.IsValueType && autoLayoutCache.TypeIsAutoLayoutOrContainsAutoLayout(type))
                {
                    reportDiagnostic(locations.CreateDiagnostic(descriptorOverride ?? FeatureUnsupportedWhenRuntimeMarshallingDisabledAutoLayoutTypes));
                }
            }
        }
    }
}
