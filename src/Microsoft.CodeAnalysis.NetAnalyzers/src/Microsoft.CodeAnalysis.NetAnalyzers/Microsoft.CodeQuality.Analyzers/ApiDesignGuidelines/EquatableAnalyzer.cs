// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1066: <inheritdoc cref="ImplementIEquatableWhenOverridingObjectEqualsTitle"/>
    /// CA1067: <inheritdoc cref="OverrideObjectEqualsTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class EquatableAnalyzer : DiagnosticAnalyzer
    {
        internal const string ImplementIEquatableRuleId = "CA1066";
        internal const string OverrideObjectEqualsRuleId = "CA1067";

        internal static readonly DiagnosticDescriptor ImplementIEquatableDescriptor = DiagnosticDescriptorHelper.Create(
            ImplementIEquatableRuleId,
            CreateLocalizableResourceString(nameof(ImplementIEquatableWhenOverridingObjectEqualsTitle)),
            CreateLocalizableResourceString(nameof(ImplementIEquatableWhenOverridingObjectEqualsMessage)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(ImplementIEquatableWhenOverridingObjectEqualsDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        internal static readonly DiagnosticDescriptor OverridesObjectEqualsDescriptor = DiagnosticDescriptorHelper.Create(
            OverrideObjectEqualsRuleId,
            CreateLocalizableResourceString(nameof(OverrideObjectEqualsTitle)),
            CreateLocalizableResourceString(nameof(OverrideObjectEqualsMessage)),
            DiagnosticCategory.Design,
            RuleLevel.BuildWarningCandidate,
            description: CreateLocalizableResourceString(nameof(OverrideObjectEqualsDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(ImplementIEquatableDescriptor, OverridesObjectEqualsDescriptor);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            INamedTypeSymbol? equatableType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIEquatable1);
            if (equatableType != null)
            {
                context.RegisterSymbolAction(c => AnalyzeSymbol(c, equatableType), SymbolKind.NamedType);
            }
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context, INamedTypeSymbol equatableType)
        {
            if (context.Symbol is not INamedTypeSymbol namedType
                || (namedType.TypeKind != TypeKind.Struct && namedType.TypeKind != TypeKind.Class)
                || (namedType.TypeKind == TypeKind.Struct && namedType.IsRefLikeType))
            {
                return;
            }

            bool overridesObjectEquals = namedType.OverridesEquals();

            INamedTypeSymbol constructedEquatable = equatableType.Construct(namedType);
            INamedTypeSymbol? implementation = namedType
                .AllInterfaces
                .FirstOrDefault(x => x.Equals(constructedEquatable));
            bool implementsEquatable = implementation != null;

            if (implementsEquatable)
            {
                // Bail out for following cases:
                // 1. There is no method implementing IEquatable.Equals method, indicating compiler error.
                // 2. Base type is implementing the IEquatable for this type, and hence is responsible for overriding object Equals.
                //    For example, we should not flag type B below as IEquatable<B> is implemented by its base type:
                //       class B : A<B> { }
                //       class A<T> : IEquatable<T>
                //          where T: A<T>
                //       { ... }
                if (constructedEquatable.GetMembers("Equals").FirstOrDefault() is not IMethodSymbol equatableEqualsMethod ||
                    !Equals(namedType, namedType.FindImplementationForInterfaceMember(equatableEqualsMethod)?.ContainingType))
                {
                    return;
                }
            }

            if (overridesObjectEquals && !implementsEquatable && namedType.TypeKind == TypeKind.Struct)
            {
                context.ReportDiagnostic(namedType.CreateDiagnostic(ImplementIEquatableDescriptor, namedType));
            }

            if (!overridesObjectEquals && implementsEquatable)
            {
                context.ReportDiagnostic(namedType.CreateDiagnostic(OverridesObjectEqualsDescriptor, namedType));
            }
        }
    }
}
