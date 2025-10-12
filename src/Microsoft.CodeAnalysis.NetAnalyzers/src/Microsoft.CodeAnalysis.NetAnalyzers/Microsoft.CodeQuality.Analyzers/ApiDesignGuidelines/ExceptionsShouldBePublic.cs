// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
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

        private static readonly ImmutableHashSet<OutputKind> s_excludedOutputKinds =
            ImmutableHashSet.Create(OutputKind.ConsoleApplication, OutputKind.WindowsApplication, OutputKind.WindowsRuntimeApplication);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);

            context.RegisterCompilationStartAction(AnalyzeCompilationStart);
        }

        private static void AnalyzeCompilationStart(CompilationStartAnalysisContext csContext)
        {
            if (s_excludedOutputKinds.Contains(csContext.Compilation.Options.OutputKind))
            {
                return;
            }

            var typeProvider = WellKnownTypeProvider.GetOrCreate(csContext.Compilation);
            // Get named type symbols for targeted exception types
            ImmutableHashSet<INamedTypeSymbol> exceptionTypes = s_exceptionTypeNames
                .Select(typeProvider.GetOrCreateTypeByMetadataName)
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
