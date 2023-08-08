// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA2256: <inheritdoc cref="InterfaceMembersMissingImplementationTitle"/>
    /// CA2257: <inheritdoc cref="MembersDeclaredOnImplementationTypeMustBeStaticTitle"/>
    /// CA2258: <inheritdoc cref="DynamicInterfaceCastableImplementationUnsupportedTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    internal sealed class DynamicInterfaceCastableImplementationAnalyzer : DiagnosticAnalyzer
    {
        internal const string DynamicInterfaceCastableImplementationUnsupportedRuleId = "CA2258";

        private static readonly DiagnosticDescriptor DynamicInterfaceCastableImplementationUnsupported =
            DiagnosticDescriptorHelper.Create(
                DynamicInterfaceCastableImplementationUnsupportedRuleId,
                CreateLocalizableResourceString(nameof(DynamicInterfaceCastableImplementationUnsupportedTitle)),
                CreateLocalizableResourceString(nameof(DynamicInterfaceCastableImplementationUnsupportedMessage)),
                DiagnosticCategory.Usage,
                RuleLevel.BuildWarning,
                CreateLocalizableResourceString(nameof(DynamicInterfaceCastableImplementationUnsupportedDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        internal const string InterfaceMembersMissingImplementationRuleId = "CA2256";

        private static readonly DiagnosticDescriptor InterfaceMembersMissingImplementation =
            DiagnosticDescriptorHelper.Create(
                InterfaceMembersMissingImplementationRuleId,
                CreateLocalizableResourceString(nameof(InterfaceMembersMissingImplementationTitle)),
                CreateLocalizableResourceString(nameof(InterfaceMembersMissingImplementationMessage)),
                DiagnosticCategory.Usage,
                RuleLevel.BuildWarning,
                CreateLocalizableResourceString(nameof(InterfaceMembersMissingImplementationDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        internal const string MembersDeclaredOnImplementationTypeMustBeStaticRuleId = "CA2257";

        private static readonly DiagnosticDescriptor MembersDeclaredOnImplementationTypeMustBeStatic =
            DiagnosticDescriptorHelper.Create(
                MembersDeclaredOnImplementationTypeMustBeStaticRuleId,
                CreateLocalizableResourceString(nameof(MembersDeclaredOnImplementationTypeMustBeStaticTitle)),
                CreateLocalizableResourceString(nameof(MembersDeclaredOnImplementationTypeMustBeStaticMessage)),
                DiagnosticCategory.Usage,
                RuleLevel.BuildWarning,
                CreateLocalizableResourceString(nameof(MembersDeclaredOnImplementationTypeMustBeStaticDescription)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        internal const string NonStaticMemberIsMethodKey = nameof(NonStaticMemberIsMethodKey);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(
            DynamicInterfaceCastableImplementationUnsupported,
            InterfaceMembersMissingImplementation,
            MembersDeclaredOnImplementationTypeMustBeStatic);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);
            context.RegisterCompilationStartAction(context =>
            {
                if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesDynamicInterfaceCastableImplementationAttribute, out INamedTypeSymbol? dynamicInterfaceCastableImplementationAttribute))
                {
                    context.RegisterSymbolAction(context => AnalyzeType(context, dynamicInterfaceCastableImplementationAttribute), SymbolKind.NamedType);
                }
            });
        }

        private static void AnalyzeType(SymbolAnalysisContext context, INamedTypeSymbol dynamicInterfaceCastableImplementationAttribute)
        {
            INamedTypeSymbol targetType = (INamedTypeSymbol)context.Symbol;

            if (targetType.TypeKind != TypeKind.Interface)
            {
                return;
            }

            if (!targetType.HasAnyAttribute(dynamicInterfaceCastableImplementationAttribute))
            {
                return;
            }

            // Default Interface Methods are required to provide an IDynamicInterfaceCastable implementation type.
            // Since Visual Basic does not support DIMs, an implementation type cannot be correctly provided in VB.
            if (context.Compilation.Language == LanguageNames.VisualBasic)
            {
                context.ReportDiagnostic(targetType.CreateDiagnostic(DynamicInterfaceCastableImplementationUnsupported));
                return;
            }

            bool missingMethodImplementations = false;
            foreach (var iface in targetType.AllInterfaces)
            {
                foreach (var member in iface.GetMembers())
                {
                    if (member.IsAbstract
                        && member.Kind != SymbolKind.NamedType
                        && context.Compilation.IsSymbolAccessibleWithin(member, targetType)
                        && targetType.FindImplementationForInterfaceMember(member) is null)
                    {
                        missingMethodImplementations = true;
                        break;
                    }
                }

                // Once we find one missing method implementation, we can stop searching for missing implementations.
                if (missingMethodImplementations)
                {
                    break;
                }
            }

            if (missingMethodImplementations)
            {
                context.ReportDiagnostic(targetType.CreateDiagnostic(InterfaceMembersMissingImplementation, targetType.ToDisplayString()));
            }

            foreach (var member in targetType.GetMembers())
            {
                if (!member.IsImplementationOfAnyExplicitInterfaceMember() && !member.IsStatic)
                {
                    // We don't want to emit diagnostics when the member is an accessor method.
                    if (member is not IMethodSymbol { AssociatedSymbol: IPropertySymbol or IEventSymbol })
                    {
                        // Emit diagnostic for non-concrete method on implementation interface
                        ImmutableDictionary<string, string?> propertyBag = ImmutableDictionary<string, string?>.Empty;
                        if (member is IMethodSymbol)
                        {
                            propertyBag = propertyBag.Add(NonStaticMemberIsMethodKey, string.Empty);
                        }

                        context.ReportDiagnostic(member.CreateDiagnostic(MembersDeclaredOnImplementationTypeMustBeStatic, propertyBag, member.ToDisplayString(), targetType.ToDisplayString()));
                    }
                }
            }
        }
    }
}
