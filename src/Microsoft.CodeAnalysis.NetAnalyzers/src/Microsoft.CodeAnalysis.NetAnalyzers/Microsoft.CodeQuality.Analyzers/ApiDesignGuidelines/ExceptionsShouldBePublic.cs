// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Analyzer.Utilities;
using System.Collections.Generic;
using System.Linq;
using Analyzer.Utilities.Extensions;

namespace Microsoft.CodeQuality.Analyzers.ApiDesignGuidelines
{
    using static MicrosoftCodeQualityAnalyzersResources;

    /// <summary>
    /// CA1064: <inheritdoc cref="ExceptionsShouldBePublicTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class ExceptionsShouldBePublicAnalyzer : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1064";

        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            CreateLocalizableResourceString(nameof(ExceptionsShouldBePublicTitle)),
            CreateLocalizableResourceString(nameof(ExceptionsShouldBePublicMessage)),
            DiagnosticCategory.Design,
            RuleLevel.Disabled,
            description: CreateLocalizableResourceString(nameof(ExceptionsShouldBePublicDescription)),
            isPortedFxCopRule: true,
            isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        private static readonly List<string> s_exceptionTypeNames = new()
        {
            "System.Exception",
            "System.SystemException",
            "System.ApplicationException"
        };

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(AnalyzeCompilationStart);
        }

        private static void AnalyzeCompilationStart(CompilationStartAnalysisContext csContext)
        {
            // Get named type symbols for targetted exception types
            ImmutableHashSet<INamedTypeSymbol> exceptionTypes = s_exceptionTypeNames
                .Select(csContext.Compilation.GetOrCreateTypeByMetadataName)
                .WhereNotNull()
                .ToImmutableHashSet();

            if (!exceptionTypes.IsEmpty)
            {
                // register symbol action for named types
                csContext.RegisterSymbolAction(saContext =>
                {
                    var symbol = (INamedTypeSymbol)saContext.Symbol;

                    // skip public symbols
                    if (symbol.IsPublic())
                        return;

                    // only report if base type matches 
                    if (symbol.BaseType != null && exceptionTypes.Contains(symbol.BaseType))
                    {
                        saContext.ReportDiagnostic(symbol.CreateDiagnostic(Rule));
                    }
                },
                SymbolKind.NamedType);
            }
        }
    }
}