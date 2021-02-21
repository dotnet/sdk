// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.QualityGuidelines
{
    /// <summary>
    /// CA2119: Seal methods that satisfy private interfaces
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class SealMethodsThatSatisfyPrivateInterfacesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA2119";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.SealMethodsThatSatisfyPrivateInterfacesTitle), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.SealMethodsThatSatisfyPrivateInterfacesMessage), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftCodeQualityAnalyzersResources.SealMethodsThatSatisfyPrivateInterfacesDescription), MicrosoftCodeQualityAnalyzersResources.ResourceManager, typeof(MicrosoftCodeQualityAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(RuleId,
                                                                             s_localizableTitle,
                                                                             s_localizableMessage,
                                                                             DiagnosticCategory.Security,
                                                                             RuleLevel.CandidateForRemoval,     // Need confirmation from security team if this is no longer a security concern.
                                                                             description: s_localizableDescription,
                                                                             isPortedFxCopRule: true,
                                                                             isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterSymbolAction(CheckTypes, SymbolKind.NamedType);
        }

        private static void CheckTypes(SymbolAnalysisContext context)
        {
            var type = (INamedTypeSymbol)context.Symbol;

            // Only classes can have overridable members, and furthermore, only consider classes that can be subclassed outside this assembly. Note: Internal types can still be subclassed in this assembly, and also in other assemblies that have access to internal types in this assembly via [InternalsVisibleTo] (recall that this permission must be whitelisted in this assembly). In both of these cases, there should be no security vulnerabilities introduced by overriding methods, hence these types can be ignored.
            if (type.TypeKind != TypeKind.Class ||
                type.IsSealed ||
                !type.GetResultantVisibility().IsAtLeastAsVisibleAs(SymbolVisibility.Public) ||
                type.Constructors.Any() && !type.Constructors.Any(c => c.GetResultantVisibility().IsAtLeastAsVisibleAs(SymbolVisibility.Public)))
            {
                return;
            }

            // look for implementations of interfaces members declared on this type
            var directInterfacesAndTheirInterfaces = type.Interfaces.SelectMany(i => Enumerable.Repeat(i, 1).Concat(i.AllInterfaces))
                .Distinct<INamedTypeSymbol>(SymbolEqualityComparer.Default);
            foreach (INamedTypeSymbol iface in directInterfacesAndTheirInterfaces)
            {
                // only matters if the interface is defined to be internal
                if (iface.DeclaredAccessibility != Accessibility.Internal)
                {
                    continue;
                }

                // look for implementation of interface members
                foreach (var imember in iface.GetMembers())
                {
                    var member = type.FindImplementationForInterfaceMember(imember);
                    // only matters if member can be overridden
                    if (member == null || !CanBeOverridden(member))
                    {
                        continue;
                    }

                    if (member.ContainingType != null && member.ContainingType.Equals(type))
                    {
                        context.ReportDiagnostic(member.CreateDiagnostic(Rule));
                    }
                    else
                    {
                        // we have a member and its not declared on this type?
                        // must be implicit implementation of base member
                        context.ReportDiagnostic(type.CreateDiagnostic(Rule));
                    }
                }
            }
        }

        private static bool CanBeOverridden(ISymbol member)
        {
            return (member.IsAbstract || member.IsVirtual || member.IsOverride)
                            && !(member.IsSealed || member.IsStatic || member.DeclaredAccessibility == Accessibility.Private)
                            && member.ContainingType != null
                            && member.ContainingType.TypeKind == TypeKind.Class;
        }
    }
}
