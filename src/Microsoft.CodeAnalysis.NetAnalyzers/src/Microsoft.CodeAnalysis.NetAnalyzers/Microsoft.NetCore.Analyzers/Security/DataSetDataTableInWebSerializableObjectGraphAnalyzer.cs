// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    /// <summary>
    /// For detecting deserialization of <see cref="T:System.Data.DataSet"/> or <see cref="T:System.Data.DataTable"/> in an
    /// web API / WCF API serializable object graph.
    /// </summary>
    [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
    public abstract class DataSetDataTableInWebSerializableObjectGraphAnalyzer : DiagnosticAnalyzer
    {
        internal static readonly DiagnosticDescriptor ObjectGraphContainsDangerousTypeDescriptor =
            SecurityHelpers.CreateDiagnosticDescriptor(
                "CA2356",
                nameof(MicrosoftNetCoreAnalyzersResources.DataSetDataTableInWebDeserializableObjectGraphTitle),
                nameof(MicrosoftNetCoreAnalyzersResources.DataSetDataTableInWebDeserializableObjectGraphMessage),
                RuleLevel.Disabled,
                isPortedFxCopRule: false,
                isDataflowRule: false,
                isReportedAtCompilationEnd: false);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(ObjectGraphContainsDangerousTypeDescriptor);

        protected abstract string ToString(TypedConstant typedConstant);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartAnalysisContext) =>
                {
                    Compilation? compilation = compilationStartAnalysisContext.Compilation;
                    WellKnownTypeProvider wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilation);

                    if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                                WellKnownTypeNames.SystemDataDataSet,
                                out INamedTypeSymbol? dataSetTypeSymbol)
                        || !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                                WellKnownTypeNames.SystemDataDataTable,
                                out INamedTypeSymbol? dataTableTypeSymbol))
                    {
                        return;
                    }

                    INamedTypeSymbol? webMethodAttributeTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemWebServicesWebMethodAttribute);
                    INamedTypeSymbol? operationContractAttributeTypeSymbol =
                        wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                            WellKnownTypeNames.SystemServiceModelOperationContractAttribute);

                    if (webMethodAttributeTypeSymbol == null && operationContractAttributeTypeSymbol == null)
                    {
                        return;
                    }

                    InsecureDeserializationTypeDecider decider = InsecureDeserializationTypeDecider.GetOrCreate(compilation);

                    // Symbol actions for SymbolKind.Method don't seem to trigger on interface methods, so we'll do register
                    // for SymbolKind.NamedTypeSymbol instead.
                    compilationStartAnalysisContext.RegisterSymbolAction(
                        (SymbolAnalysisContext symbolAnalysisContext) =>
                        {
                            INamedTypeSymbol namedTypeSymbol = (INamedTypeSymbol)symbolAnalysisContext.Symbol;
                            if (namedTypeSymbol.TypeKind is not TypeKind.Interface
                                and not TypeKind.Class)
                            {
                                return;
                            }

                            foreach (ISymbol? memberSymbol in namedTypeSymbol.GetMembers())
                            {
                                if (memberSymbol is not IMethodSymbol methodSymbol)
                                {
                                    continue;
                                }

                                ObjectGraphOptions optionsToUse;
                                if (methodSymbol.HasAttribute(webMethodAttributeTypeSymbol))
                                {
                                    optionsToUse = ObjectGraphOptions.XmlSerializerOptions;
                                }
                                else if (methodSymbol.HasAttribute(operationContractAttributeTypeSymbol))
                                {
                                    optionsToUse = ObjectGraphOptions.DataContractOptions;
                                }
                                else
                                {
                                    continue;
                                }

                                foreach (IParameterSymbol parameterSymbol in methodSymbol.Parameters)
                                {
                                    if (decider.IsObjectGraphInsecure(
                                            parameterSymbol.Type,
                                            optionsToUse,
                                            out ImmutableArray<InsecureObjectGraphResult> results))
                                    {
                                        foreach (InsecureObjectGraphResult result in results)
                                        {
                                            symbolAnalysisContext.ReportDiagnostic(
                                                Diagnostic.Create(
                                                    ObjectGraphContainsDangerousTypeDescriptor,
                                                    parameterSymbol.DeclaringSyntaxReferences.First().GetSyntax().GetLocation(),
                                                    result.InsecureType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                                                    result.GetDisplayString(typedConstant => ToString(typedConstant))));
                                        }
                                    }
                                }
                            }
                        },
                        SymbolKind.NamedType);
                });
        }
    }
}
