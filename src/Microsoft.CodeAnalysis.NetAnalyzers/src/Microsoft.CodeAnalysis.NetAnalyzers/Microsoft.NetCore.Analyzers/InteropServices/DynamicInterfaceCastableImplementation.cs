// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    internal sealed class DynamicInterfaceCastableImplementationAnalyzer : DiagnosticAnalyzer
    {
        internal const string DynamicInterfaceCastableImplementationUnsupportedRuleId = "CA2258";

        private static readonly DiagnosticDescriptor DynamicInterfaceCastableImplementationUnsupported =
            DiagnosticDescriptorHelper.Create(
                DynamicInterfaceCastableImplementationUnsupportedRuleId,
                new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DynamicInterfaceCastableImplementationUnsupportedTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
                new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DynamicInterfaceCastableImplementationUnsupportedMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
                DiagnosticCategory.Usage,
                RuleLevel.BuildWarning,
                new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.DynamicInterfaceCastableImplementationUnsupportedDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        internal const string InterfaceMembersMissingImplementationRuleId = "CA2256";

        private static readonly DiagnosticDescriptor InterfaceMembersMissingImplementation =
            DiagnosticDescriptorHelper.Create(
                InterfaceMembersMissingImplementationRuleId,
                new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.InterfaceMembersMissingImplementationTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
                new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.InterfaceMembersMissingImplementationMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
                DiagnosticCategory.Usage,
                RuleLevel.BuildWarning,
                new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.InterfaceMembersMissingImplementationDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        internal const string MembersDeclaredOnImplementationTypeMustBeStaticRuleId = "CA2257";

        private static readonly DiagnosticDescriptor MembersDeclaredOnImplementationTypeMustBeStatic =
            DiagnosticDescriptorHelper.Create(
                MembersDeclaredOnImplementationTypeMustBeStaticRuleId,
                new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.MembersDeclaredOnImplementationTypeMustBeStaticTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
                new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.MembersDeclaredOnImplementationTypeMustBeStaticMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
                DiagnosticCategory.Usage,
                RuleLevel.BuildWarning,
                new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.MembersDeclaredOnImplementationTypeMustBeStaticDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources)),
                isPortedFxCopRule: false,
                isDataflowRule: false);

        internal const string NonStaticMemberIsMethodKey = nameof(NonStaticMemberIsMethodKey);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(
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

            if (!targetType.HasAttribute(dynamicInterfaceCastableImplementationAttribute))
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
                    if (!member.IsStatic
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
