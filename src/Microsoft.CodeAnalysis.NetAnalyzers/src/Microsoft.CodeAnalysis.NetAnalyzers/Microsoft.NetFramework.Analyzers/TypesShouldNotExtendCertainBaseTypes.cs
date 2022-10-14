// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetFramework.Analyzers
{
    using static MicrosoftNetFrameworkAnalyzersResources;

    /// <summary>
    /// CA1058: <inheritdoc cref="TypesShouldNotExtendCertainBaseTypesTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class TypesShouldNotExtendCertainBaseTypesAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1058";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(TypesShouldNotExtendCertainBaseTypesTitle)),
            "{0}",
            DiagnosticCategory.Design,
            RuleLevel.CandidateForRemoval,
            description: CreateLocalizableResourceString(nameof(TypesShouldNotExtendCertainBaseTypesDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        private static readonly ImmutableDictionary<string, string> s_badBaseTypesToMessage = new Dictionary<string, string>
                                                    {
                                                        {"System.ApplicationException", MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemApplicationException},
                                                        {"System.Xml.XmlDocument", MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemXmlXmlDocument},
                                                        {"System.Collections.CollectionBase", MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsCollectionBase},
                                                        {"System.Collections.DictionaryBase", MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsDictionaryBase},
                                                        {"System.Collections.Queue", MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsQueue},
                                                        {"System.Collections.ReadOnlyCollectionBase", MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsReadOnlyCollectionBase},
                                                        {"System.Collections.SortedList", MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsSortedList},
                                                        {"System.Collections.Stack", MicrosoftNetFrameworkAnalyzersResources.TypesShouldNotExtendCertainBaseTypesMessageSystemCollectionsStack},
                                                    }.ToImmutableDictionary();

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(AnalyzeCompilationStart);
        }

        private static void AnalyzeCompilationStart(CompilationStartAnalysisContext context)
        {
            ImmutableHashSet<INamedTypeSymbol> badBaseTypes = s_badBaseTypesToMessage.Keys
                                .Select(context.Compilation.GetOrCreateTypeByMetadataName)
                                .WhereNotNull()
                                .ToImmutableHashSet();

            if (!badBaseTypes.IsEmpty)
            {
                context.RegisterSymbolAction((saContext) =>
                    {
                        var namedTypeSymbol = (INamedTypeSymbol)saContext.Symbol;

                        if (namedTypeSymbol.BaseType != null &&
                            badBaseTypes.Contains(namedTypeSymbol.BaseType) &&
                            saContext.Options.MatchesConfiguredVisibility(Rule, namedTypeSymbol, saContext.Compilation))
                        {
                            string baseTypeName = namedTypeSymbol.BaseType.ToDisplayString();
                            Debug.Assert(s_badBaseTypesToMessage.ContainsKey(baseTypeName));
                            string message = string.Format(CultureInfo.CurrentCulture, s_badBaseTypesToMessage[baseTypeName], namedTypeSymbol.ToDisplayString(), baseTypeName);
                            Diagnostic diagnostic = namedTypeSymbol.CreateDiagnostic(Rule, message);
                            saContext.ReportDiagnostic(diagnostic);
                        }
                    }
                    , SymbolKind.NamedType);
            }
        }
    }
}