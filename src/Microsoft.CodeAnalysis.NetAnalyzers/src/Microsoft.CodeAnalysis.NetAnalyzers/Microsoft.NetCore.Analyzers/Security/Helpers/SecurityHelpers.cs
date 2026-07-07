// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

#pragma warning disable CA1054 // Uri parameters should not be strings

namespace Microsoft.NetCore.Analyzers.Security.Helpers
{
    using static MicrosoftNetCoreAnalyzersResources;

    internal static class SecurityHelpers
    {
        /// <summary>
        /// Creates a DiagnosticDescriptor with <see cref="LocalizableResourceString"/>s from the specified resource source type.
        /// </summary>
        /// <param name="id">Diagnostic identifier.</param>
        /// <param name="titleResourceStringName">Name of the resource string for the diagnostic's title.</param>
        /// <param name="messageResourceStringName">Name of the resource string for the diagnostic's message.</param>
        /// <param name="ruleLevel">Indicates the <see cref="RuleLevel"/> for this rule.</param>
        /// <param name="descriptionResourceStringName">Name of the resource string for the diagnostic's description.</param>
        /// <param name="isPortedFxCopRule">Flag indicating if this is a rule ported from legacy FxCop.</param>
        /// <param name="isDataflowRule">Flag indicating if this is a dataflow analysis based rule.</param>
        /// <returns>new DiagnosticDescriptor</returns>
        public static DiagnosticDescriptor CreateDiagnosticDescriptor(
            string id,
            string titleResourceStringName,
            string messageResourceStringName,
            RuleLevel ruleLevel,
            bool isPortedFxCopRule,
            bool isDataflowRule,
            bool isReportedAtCompilationEnd,
            string? descriptionResourceStringName = null)
        {
            return DiagnosticDescriptorHelper.Create(
                id,
                CreateLocalizableResourceString(titleResourceStringName),
                CreateLocalizableResourceString(messageResourceStringName),
                DiagnosticCategory.Security,
                ruleLevel,
                descriptionResourceStringName != null ? CreateLocalizableResourceString(descriptionResourceStringName) : null,
                isPortedFxCopRule,
                isDataflowRule,
                isReportedAtCompilationEnd: isReportedAtCompilationEnd);
        }

        /// <summary>
        /// Deserialization methods for <see cref="T:System.Runtime.Serialization.Formatters.Binary.BinaryFormatter"/>.
        /// </summary>
        [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
        public static readonly ImmutableHashSet<string> BinaryFormatterDeserializationMethods =
            ImmutableHashSet.Create(
                StringComparer.Ordinal,
                "Deserialize",
                "DeserializeMethodResponse",
                "UnsafeDeserialize",
                "UnsafeDeserializeMethodResponse");

        /// <summary>
        /// Deserialization methods for <see cref="T:System.Runtime.Serialization.NetDataContractSerializer"/>.
        /// </summary>
        [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
        public static readonly ImmutableHashSet<string> NetDataContractSerializerDeserializationMethods =
            ImmutableHashSet.Create(
                StringComparer.Ordinal,
                "Deserialize",
                "ReadObject");

        /// <summary>
        /// Deserialization methods for <see cref="T:System.Web.Script.Serialization.JavaScriptSerializer"/>.
        /// </summary>
        [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
        public static readonly ImmutableHashSet<string> JavaScriptSerializerDeserializationMethods =
            ImmutableHashSet.Create(
                StringComparer.Ordinal,
                "Deserialize",
                "DeserializeObject");

        /// <summary>
        /// Deserialization methods for <see cref="T:System.Web.UI.ObjectStateFormatter"/>.
        /// </summary>
        [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
        public static readonly ImmutableHashSet<string> ObjectStateFormatterDeserializationMethods =
            ImmutableHashSet.Create(
                StringComparer.Ordinal,
                "Deserialize");

        /// <summary>
        /// Deserialization methods for <see cref="T:System.Runtime.Serialization.Formatters.Soap.SoapFormatter"/>.
        /// </summary>
        [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
        public static readonly ImmutableHashSet<string> SoapFormatterDeserializationMethods =
            ImmutableHashSet.Create(
                StringComparer.Ordinal,
                "Deserialize");

        /// <summary>
        /// Methods using a <see cref="T:Newtonsoft.Json.JsonSerializerSettings"/> parameter for <see cref="T:Newtonsoft.Json.JsonSerializer"/>.
        /// </summary>
        [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
        public static readonly ImmutableHashSet<string> JsonSerializerInstantiateWithSettingsMethods =
            ImmutableHashSet.Create(
                StringComparer.Ordinal,
                "Create",
                "CreateDefault");

        /// <summary>
        /// Methods using a <see cref="T:Newtonsoft.Json.JsonSerializerSettings"/> parameter for <see cref="T:Newtonsoft.Json.JsonConvert"/>.
        /// </summary>
        [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
        public static readonly ImmutableHashSet<string> JsonConvertWithSettingsMethods =
            ImmutableHashSet.Create(
                StringComparer.Ordinal,
                "DeserializeObject",
                "DeserializeAnonymousType",
                "PopulateObject");

        /// <summary>
        /// Deserialization methods for <see cref="T:Newtonsoft.Json.JsonSerializer"/>.
        /// </summary>
        [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
        public static readonly ImmutableHashSet<string> JsonSerializerDeserializationMethods =
            ImmutableHashSet.Create(
                StringComparer.Ordinal,
                "Deserialize",
                "Populate");

        /// <summary>
        /// Deserialization methods for <see cref="T:System.Data.DataTable"/>.
        /// </summary>
        [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
        public static readonly ImmutableHashSet<string> DataTableDeserializationMethods =
            ImmutableHashSet.Create(
                StringComparer.Ordinal,
                "ReadXml");

        /// <summary>
        /// Deserialization methods for <see cref="T:System.Data.DataSet"/>.
        /// </summary>
        [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
        public static readonly ImmutableHashSet<string> DataSetDeserializationMethods =
            ImmutableHashSet.Create(
                StringComparer.Ordinal,
                "ReadXml");

        /// <summary>
        /// Determines if an operation is inside autogenerated code that's probably for a GUI app.
        /// </summary>
        /// <param name="operationAnalysisContext">Context for the operation in question.</param>
        /// <param name="wellKnownTypeProvider"><see cref="WellKnownTypeProvider"/> for the operation's compilation.</param>
        /// <returns>True if so, false otherwise.</returns>
        /// <remarks>
        /// This is useful for analyzers categorizing cases into different rules to represent different risks.
        /// </remarks>
        public static bool IsOperationInsideAutogeneratedCodeForGuiApp(
            OperationAnalysisContext operationAnalysisContext,
            WellKnownTypeProvider wellKnownTypeProvider)
        {
            INamedTypeSymbol? xmlReaderTypeSymbol =
                wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.SystemXmlXmlReader);
            INamedTypeSymbol? designerCategoryAttributeTypeSymbol =
                wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.SystemComponentModelDesignerCategoryAttribute);
            INamedTypeSymbol? debuggerNonUserCodeAttributeTypeSymbol =
                wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                    WellKnownTypeNames.SystemDiagnosticsDebuggerNonUserCode);

            return
                operationAnalysisContext.ContainingSymbol is IMethodSymbol methodSymbol
                && methodSymbol.MetadataName == "ReadXmlSerializable"
                && methodSymbol.DeclaredAccessibility == Accessibility.Protected
                && methodSymbol.IsOverride
                && methodSymbol.ReturnsVoid
                && methodSymbol.Parameters.Length == 1
                && methodSymbol.Parameters[0].Type.Equals(xmlReaderTypeSymbol)
                && methodSymbol.ContainingType?.HasAnyAttribute(designerCategoryAttributeTypeSymbol) == true
                && methodSymbol.HasAnyAttribute(debuggerNonUserCodeAttributeTypeSymbol);
        }
    }
}
