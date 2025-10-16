﻿// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using System.Linq;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1043: <inheritdoc cref="UseIntegralOrStringArgumentForIndexersTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseIntegralOrStringArgumentForIndexersAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1043";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(UseIntegralOrStringArgumentForIndexersTitle)),
            CreateLocalizableResourceString(nameof(UseIntegralOrStringArgumentForIndexersMessage)),
            DiagnosticCategory.Design,
            RuleLevel.CandidateForRemoval,
            description: CreateLocalizableResourceString(nameof(UseIntegralOrStringArgumentForIndexersDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        /// <summary>
        /// PERF: ImmutableArray contains performs better than ImmutableHashSet for small arrays of primitive types.
        /// See: https://github.com/dotnet/roslyn-analyzers/pull/3648#discussion_r428714894
        /// </summary>
        private static readonly ImmutableArray<SpecialType> s_allowedSpecialTypes =
            ImmutableArray.Create(
                SpecialType.System_String,
                SpecialType.System_Int16,
                SpecialType.System_Int32,
                SpecialType.System_Int64,
                SpecialType.System_Object,
                SpecialType.System_UInt16,
                SpecialType.System_UInt32,
                SpecialType.System_UInt64
            );

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(context =>
            {
                var allowedTypes = ImmutableHashSet.CreateBuilder<INamedTypeSymbol>();

                if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRange, out var rangeType))
                {
                    allowedTypes.Add(rangeType);
                }

                if (context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemIndex, out var indexType))
                {
                    allowedTypes.Add(indexType);
                }

                context.RegisterSymbolAction(context => AnalyzeSymbol(context, allowedTypes.ToImmutableHashSet()), SymbolKind.Property);
            });
        }

        private static void AnalyzeSymbol(SymbolAnalysisContext context, ImmutableHashSet<INamedTypeSymbol> allowedTypes)
        {
            var symbol = (IPropertySymbol)context.Symbol;
            if (!symbol.IsIndexer || symbol.IsOverride)
            {
                return;
            }

            if (symbol.Parameters.Length != 1)
            {
                return;
            }

            ITypeSymbol paramType = symbol.Parameters[0].Type;

            if (paramType.TypeKind == TypeKind.TypeParameter)
            {
                return;
            }

            if (paramType.TypeKind == TypeKind.Enum)
            {
                paramType = ((INamedTypeSymbol)paramType).EnumUnderlyingType!;
            }

            if (s_allowedSpecialTypes.Contains(paramType.SpecialType) || allowedTypes.Contains(paramType))
            {
                return;
            }

            if (context.Options.MatchesConfiguredVisibility(Rule, symbol, context.Compilation))
            {
                context.ReportDiagnostic(symbol.CreateDiagnostic(Rule));
            }
        }
    }
}

