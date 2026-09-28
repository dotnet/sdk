// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1720: <inheritdoc cref="IdentifiersShouldNotContainTypeNamesTitle"/>
    /// Cause:
    /// The name of a parameter or a member contains a language-specific data type name.
    ///
    /// Description:
    /// Names of parameters and members are better used to communicate their meaning than
    /// to describe their type, which is expected to be provided by development tools. For names of members,
    /// if a data type name must be used, use a language-independent name instead of a language-specific one.
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class IdentifiersShouldNotContainTypeNames : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1720";

        private static readonly ImmutableHashSet<string> s_typeNames =
            ImmutableHashSet.CreateRange(StringComparer.OrdinalIgnoreCase, new[]
            {
                "char",
                "wchar",
                "int8",
                "uint8",
                "short",
                "ushort",
                "int",
                "uint",
                "integer",
                "uinteger",
                "long",
                "ulong",
                "unsigned",
                "signed",
                "float",
                "float32",
                "float64",
                "int16",
                "int32",
                "int64",
                "uint16",
                "uint32",
                "uint64",
                "intptr",
                "uintptr",
                "ptr",
                "uptr",
                "pointer",
                "upointer",
                "single",
                "double",
                "decimal",
                "guid",
                "object",
                "string"
            });

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(IdentifiersShouldNotContainTypeNamesTitle)),
            CreateLocalizableResourceString(nameof(IdentifiersShouldNotContainTypeNamesMessage)),
            DiagnosticCategory.Naming,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: CreateLocalizableResourceString(nameof(IdentifiersShouldNotContainTypeNamesDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
            {
                // Analyze named types and fields.
                compilationStartAnalysisContext.RegisterSymbolAction(
                    symbolContext => AnalyzeSymbol(symbolContext.Symbol, symbolContext),
                    SymbolKind.NamedType,
                    SymbolKind.Field);

                // Analyze properties and methods, and their parameters.
                compilationStartAnalysisContext.RegisterSymbolAction(
                    symbolContext =>
                    {
                        // Although indexers aren't IMethodSymbols, their accessors are, and we can get their parameters from them
                        if (symbolContext.Symbol is IMethodSymbol method)
                        {
                            // If this method contains parameters with names violating this rule, we only want to flag them
                            // if this method is not overriding another or implementing an interface. Otherwise, changing the
                            // parameter names will violate CA1725 - Parameter names should match base declaration.
                            if (method.OverriddenMethod == null && !method.IsImplementationOfAnyInterfaceMember())
                            {
                                foreach (var param in method.Parameters)
                                {
                                    AnalyzeSymbol(param, symbolContext);
                                }
                            }
                        }

                        AnalyzeSymbol(symbolContext.Symbol, symbolContext);
                    },
                    SymbolKind.Property,
                    SymbolKind.Method);
            });
        }

        private static void AnalyzeSymbol(ISymbol symbol, SymbolAnalysisContext context)
        {
            // FxCop compat: only analyze externally visible symbols by default.
            if (!context.Options.MatchesConfiguredVisibility(Rule, symbol, context.Compilation))
            {
                return;
            }

            var identifier = symbol.Name;
            if (s_typeNames.Contains(identifier))
            {
                Diagnostic diagnostic = symbol.CreateDiagnostic(Rule, identifier);
                context.ReportDiagnostic(diagnostic);
            }
        }
    }
}