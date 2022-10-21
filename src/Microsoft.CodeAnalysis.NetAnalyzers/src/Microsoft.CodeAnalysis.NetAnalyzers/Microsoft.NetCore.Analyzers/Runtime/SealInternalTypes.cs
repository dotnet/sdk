// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Resx = Microsoft.NetCore.Analyzers.MicrosoftNetCoreAnalyzersResources;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// CA1852: <inheritdoc cref="Resx.SealInternalTypesTitle"/>
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class SealInternalTypes : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1852";
        internal static readonly DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
            RuleId,
            Resx.CreateLocalizableResourceString(nameof(Resx.SealInternalTypesTitle)),
            Resx.CreateLocalizableResourceString(nameof(Resx.SealInternalTypesMessage)),
            DiagnosticCategory.Performance,
            RuleLevel.IdeHidden_BulkConfigurable,
            Resx.CreateLocalizableResourceString(nameof(Resx.SealInternalTypesDescription)),
            isPortedFxCopRule: false,
            isDataflowRule: false,
            isReportedAtCompilationEnd: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Rule);

        public override void Initialize(AnalysisContext context)
        {
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.EnableConcurrentExecution();
            context.RegisterCompilationStartAction(OnCompilationStart);
        }

        private static void OnCompilationStart(CompilationStartAnalysisContext context)
        {
            // To avoid false positives, as with CA1812 (avoid uninstantiated internal classes), skip any assemblies with InternalsVisibleTo.
            var internalsVisibleToAttributeSymbol = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeCompilerServicesInternalsVisibleToAttribute);
            if (context.Compilation.Assembly.HasAttribute(internalsVisibleToAttributeSymbol))
            {
                return;
            }

            INamedTypeSymbol? comImportAttributeType = context.Compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeInteropServicesComImportAttribute);

            var candidateTypes = PooledConcurrentSet<INamedTypeSymbol>.GetInstance(SymbolEqualityComparer.Default);
            var baseTypes = PooledConcurrentSet<INamedTypeSymbol>.GetInstance(SymbolEqualityComparer.Default);

            context.RegisterSymbolAction(context =>
            {
                var type = (INamedTypeSymbol)context.Symbol;

                if (type.TypeKind is TypeKind.Class &&
                    !type.IsAbstract &&
                    !type.IsStatic &&
                    !type.IsSealed &&
                    !type.IsExternallyVisible() &&
                    !type.HasAttribute(comImportAttributeType))
                {
                    candidateTypes.Add(type);
                }

                for (INamedTypeSymbol? baseType = type.BaseType; baseType is not null; baseType = baseType.BaseType)
                {
                    baseTypes.Add(baseType.OriginalDefinition);
                }

            }, SymbolKind.NamedType);

            context.RegisterCompilationEndAction(context =>
            {
                foreach (INamedTypeSymbol type in candidateTypes)
                {
                    if (!baseTypes.Contains(type.OriginalDefinition))
                    {
                        context.ReportDiagnostic(type.CreateDiagnostic(Rule, type.Name));
                    }
                }

                candidateTypes.Dispose();
                baseTypes.Dispose();
            });
        }
    }
}
