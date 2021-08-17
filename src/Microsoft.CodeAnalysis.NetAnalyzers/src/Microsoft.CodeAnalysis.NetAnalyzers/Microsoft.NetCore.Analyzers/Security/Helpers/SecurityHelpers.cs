// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Resources;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

#pragma warning disable CA1054 // Uri parameters should not be strings

namespace Microsoft.NetCore.Analyzers.Security.Helpers
{
    internal static class SecurityHelpers
    {
        /// <summary>
        /// Creates a DiagnosticDescriptor with <see cref="LocalizableResourceString"/>s from <see cref="MicrosoftNetCoreAnalyzersResources"/>.
        /// </summary>
        /// <param name="id">Diagnostic identifier.</param>
        /// <param name="titleResourceStringName">Name of the resource string inside <see cref="MicrosoftNetCoreAnalyzersResources"/> for the diagnostic's title.</param>
        /// <param name="messageResourceStringName">Name of the resource string inside <see cref="MicrosoftNetCoreAnalyzersResources"/> for the diagnostic's message.</param>
        /// <param name="ruleLevel">Indicates the <see cref="RuleLevel"/> for this rule.</param>
        /// <param name="descriptionResourceStringName">Name of the resource string inside <see cref="MicrosoftNetCoreAnalyzersResources"/> for the diagnostic's description.</param>
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
            return CreateDiagnosticDescriptor(
                id,
                typeof(MicrosoftNetCoreAnalyzersResources),
                titleResourceStringName,
                messageResourceStringName,
                ruleLevel,
                isPortedFxCopRule,
                isDataflowRule,
                isReportedAtCompilationEnd,
                descriptionResourceStringName);
        }

        /// <summary>
        /// Creates a DiagnosticDescriptor with <see cref="LocalizableResourceString"/>s from the specified resource source type.
        /// </summary>
        /// <param name="id">Diagnostic identifier.</param>
        /// <param name="resourceSource">Type containing the resource strings.</param>
        /// <param name="titleResourceStringName">Name of the resource string inside <paramref name="resourceSource"/> for the diagnostic's title.</param>
        /// <param name="messageResourceStringName">Name of the resource string inside <paramref name="resourceSource"/> for the diagnostic's message.</param>
        /// <param name="ruleLevel">Indicates the <see cref="RuleLevel"/> for this rule.</param>
        /// <param name="descriptionResourceStringName">Name of the resource string inside <paramref name="resourceSource"/> for the diagnostic's description.</param>
        /// <param name="isPortedFxCopRule">Flag indicating if this is a rule ported from legacy FxCop.</param>
        /// <param name="isDataflowRule">Flag indicating if this is a dataflow analysis based rule.</param>
        /// <returns>new DiagnosticDescriptor</returns>
        public static DiagnosticDescriptor CreateDiagnosticDescriptor(
            string id,
            Type resourceSource,
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
                GetResourceString(resourceSource, titleResourceStringName),
                GetResourceString(resourceSource, messageResourceStringName),
                DiagnosticCategory.Security,
                ruleLevel,
                descriptionResourceStringName != null ? GetResourceString(resourceSource, descriptionResourceStringName) : null,
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

        private static readonly ImmutableDictionary<Type, ResourceManager> ResourceManagerMapping =
            ImmutableDictionary.CreateRange<Type, ResourceManager>(
                new[]
                {
                    (typeof(MicrosoftNetCoreAnalyzersResources), MicrosoftNetCoreAnalyzersResources.ResourceManager),
                    (typeof(MicrosoftNetCoreAnalyzersResources), MicrosoftNetCoreAnalyzersResources.ResourceManager),
                }.Select(o => new KeyValuePair<Type, ResourceManager>(o.Item1, o.ResourceManager)));

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
                && methodSymbol.ContainingType?.HasAttribute(designerCategoryAttributeTypeSymbol) == true
                && methodSymbol.HasAttribute(debuggerNonUserCodeAttributeTypeSymbol);
        }

        /// <summary>
        /// Gets a <see cref="LocalizableResourceString"/> from <see cref="MicrosoftNetCoreAnalyzersResources"/>.
        /// </summary>
        /// <param name="resourceSource">Type containing the resource strings.</param>
        /// <param name="name">Name of the resource string to retrieve.</param>
        /// <returns>The corresponding <see cref="LocalizableResourceString"/>.</returns>
        private static LocalizableResourceString GetResourceString(Type resourceSource, string name)
        {
            if (resourceSource == null)
            {
                throw new ArgumentNullException(nameof(resourceSource));
            }

            if (!ResourceManagerMapping.TryGetValue(resourceSource, out ResourceManager resourceManager))
            {
                throw new ArgumentException($"No mapping found for {resourceSource}", nameof(resourceSource));
            }

#if DEBUG
            if (resourceManager.GetString(name, System.Globalization.CultureInfo.InvariantCulture) == null)
            {
                throw new ArgumentException($"Resource string '{name}' not found in {resourceSource}", nameof(name));
            }
#endif

            LocalizableResourceString localizableResourceString = new LocalizableResourceString(name, resourceManager, resourceSource);
            return localizableResourceString;
        }
    }
}
