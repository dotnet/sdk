// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System.Collections.Concurrent;
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
    /// For detecting <see cref="T:System.Data.DataSet"/> or <see cref="T:System.Data.DataTable"/> deserializable members.
    /// </summary>
    [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DataSetDataTableInSerializableTypeAnalyzer : DiagnosticAnalyzer
    {
        // At this time, treat IFormatter-based serializers differently, since they have different guidance and known impact.
        internal static readonly DiagnosticDescriptor RceSerializableContainsDangerousType =
            SecurityHelpers.CreateDiagnosticDescriptor(
                "CA2352",
                nameof(MicrosoftNetCoreAnalyzersResources.DataSetDataTableInRceSerializableTypeTitle),
                nameof(MicrosoftNetCoreAnalyzersResources.DataSetDataTableInRceSerializableTypeMessage),
                RuleLevel.Disabled,
                isPortedFxCopRule: false,
                isDataflowRule: false);

        internal static readonly DiagnosticDescriptor SerializableContainsDangerousType =
            SecurityHelpers.CreateDiagnosticDescriptor(
                "CA2353",
                nameof(MicrosoftNetCoreAnalyzersResources.DataSetDataTableInSerializableTypeTitle),
                nameof(MicrosoftNetCoreAnalyzersResources.DataSetDataTableInSerializableTypeMessage),
                RuleLevel.Disabled,
                isPortedFxCopRule: false,
                isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(RceSerializableContainsDangerousType, SerializableContainsDangerousType);

        [SuppressMessage("Style", "IDE0047:Remove unnecessary parentheses", Justification = "Group related conditions together.")]
        public override void Initialize(AnalysisContext context)
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

                    INamedTypeSymbol? serializableAttributeTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemSerializableAttribute);

                    INamedTypeSymbol? generatedCodeAttributeTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemCodeDomCompilerGeneratedCodeAttribute);

                    // For completeness, could also consider CollectionDataContractAttribute
                    INamedTypeSymbol? dataContractAttributeTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemRuntimeSerializationDataContractAttribute);
                    INamedTypeSymbol? dataMemberAttributeTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemRuntimeSerializationDataMemberAttribute);
                    INamedTypeSymbol? ignoreDataMemberTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemRuntimeSerializationIgnoreDataMemberAttribute);
                    INamedTypeSymbol? knownTypeAttributeTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemRuntimeSerializationKnownTypeAttribute);

                    XmlSerializationAttributeTypes xmlSerializationAttributeTypes = new XmlSerializationAttributeTypes(
                        wellKnownTypeProvider);
                    if (serializableAttributeTypeSymbol == null
                        && (dataContractAttributeTypeSymbol == null || dataMemberAttributeTypeSymbol == null)
                        && ignoreDataMemberTypeSymbol == null
                        && knownTypeAttributeTypeSymbol == null
                        && !xmlSerializationAttributeTypes.Any)
                    {
                        return;
                    }

                    InsecureDeserializationTypeDecider decider = InsecureDeserializationTypeDecider.GetOrCreate(compilation);

                    ConcurrentDictionary<INamedTypeSymbol, bool> visitedTypes =
                        new ConcurrentDictionary<INamedTypeSymbol, bool>();

                    compilationStartAnalysisContext.RegisterSymbolAction(
                        (SymbolAnalysisContext symbolAnalysisContext) =>
                        {
                            INamedTypeSymbol namedTypeSymbol = (INamedTypeSymbol)symbolAnalysisContext.Symbol;
                            bool hasSerializableAttribute = namedTypeSymbol.HasAttribute(serializableAttributeTypeSymbol);

                            // Assume that [GeneratedCode] means not used for serialization.
                            bool hasGeneratedCodeAttribute = namedTypeSymbol.HasAttribute(generatedCodeAttributeTypeSymbol);
                            bool hasDataContractAttribute = namedTypeSymbol.HasAttribute(dataContractAttributeTypeSymbol);
                            bool hasKnownTypeAttribute = namedTypeSymbol.HasAttribute(knownTypeAttributeTypeSymbol);
                            bool hasAnyIgnoreDataMemberAttribute =
                                namedTypeSymbol.GetMembers().Any(m => m.HasAttribute(ignoreDataMemberTypeSymbol));
                            bool hasAnyXmlSerializationAttributes =
                                xmlSerializationAttributeTypes.HasAnyAttribute(namedTypeSymbol)
                                || namedTypeSymbol.GetMembers().Any(m => xmlSerializationAttributeTypes.HasAnyAttribute(m));
                            if (!hasSerializableAttribute
                                && !hasDataContractAttribute
                                && !hasKnownTypeAttribute
                                && !hasAnyIgnoreDataMemberAttribute
                                && !hasAnyXmlSerializationAttributes)
                            {
                                // Don't have any attributes suggesting this class is serialized.
                                return;
                            }

                            ObjectGraphOptions options = new ObjectGraphOptions(
                                recurse: false,
                                binarySerialization: hasSerializableAttribute,
                                dataContractSerialization:
                                    hasDataContractAttribute
                                    || hasAnyIgnoreDataMemberAttribute
                                    || hasKnownTypeAttribute,
                                xmlSerialization: hasAnyXmlSerializationAttributes);

                            if (decider.IsObjectGraphInsecure(
                                    namedTypeSymbol,
                                    options,
                                    out ImmutableArray<InsecureObjectGraphResult> results))
                            {
                                DiagnosticDescriptor diagnosticToReport =
                                    hasSerializableAttribute
                                        ? RceSerializableContainsDangerousType
                                        : SerializableContainsDangerousType;

                                foreach (InsecureObjectGraphResult result in results)
                                {
                                    symbolAnalysisContext.ReportDiagnostic(
                                        Diagnostic.Create(
                                            diagnosticToReport,
                                            result.GetLocation(),
                                            result.InsecureType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                                            result.GetDisplayString()));
                                }
                            }
                        },
                        SymbolKind.NamedType);
                });
        }
    }
}
