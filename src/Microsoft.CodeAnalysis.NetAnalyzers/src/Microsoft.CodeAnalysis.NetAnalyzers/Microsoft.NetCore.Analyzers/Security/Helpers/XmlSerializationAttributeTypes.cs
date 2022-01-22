// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Linq;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;

namespace Microsoft.NetCore.Analyzers.Security.Helpers
{
    /// <summary>
    /// Just a common way to get <see cref="INamedTypeSymbol"/>s for attributes that affect XML serialization.
    /// </summary>
    /// <remarks>
    /// https://docs.microsoft.com/en-us/dotnet/standard/serialization/attributes-that-control-xml-serialization
    /// </remarks>
    public class XmlSerializationAttributeTypes
    {
        /// <summary>
        /// Indicates that at least one attribute is defined.
        /// </summary>
        public bool Any { get; private set; }

        public INamedTypeSymbol? XmlAnyAttributeAttribute { get; private set; }
        public INamedTypeSymbol? XmlAnyElementAttribute { get; private set; }
        public INamedTypeSymbol? XmlArrayAttribute { get; private set; }
        public INamedTypeSymbol? XmlArrayItemAttribute { get; private set; }
        public INamedTypeSymbol? XmlAttributeAttribute { get; private set; }
        public INamedTypeSymbol? XmlChoiceIdentifierAttribute { get; private set; }
        public INamedTypeSymbol? XmlElementAttribute { get; private set; }
        public INamedTypeSymbol? XmlEnumAttribute { get; private set; }
        public INamedTypeSymbol? XmlIgnoreAttribute { get; private set; }
        public INamedTypeSymbol? XmlIncludeAttribute { get; private set; }
        public INamedTypeSymbol? XmlRootAttribute { get; private set; }
        public INamedTypeSymbol? XmlTextAttribute { get; private set; }
        public INamedTypeSymbol? XmlTypeAttribute { get; private set; }

        /// <summary>
        /// Constructs.
        /// </summary>
        /// <param name="wellKnownTypeProvider">The compilation's <see cref="WellKnownTypeProvider"/>.</param>
        public XmlSerializationAttributeTypes(WellKnownTypeProvider wellKnownTypeProvider)
        {
            this.XmlAnyAttributeAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemXmlSerializationXmlAnyAttributeAttribute);
            this.XmlAnyElementAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemXmlSerializationXmlAnyElementAttribute);
            this.XmlArrayAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemXmlSerializationXmlArrayAttribute);
            this.XmlArrayItemAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemXmlSerializationXmlArrayItemAttribute);
            this.XmlAttributeAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemXmlSerializationXmlAttributeAttribute);
            this.XmlChoiceIdentifierAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemXmlSerializationXmlChoiceIdentifierAttribute);
            this.XmlElementAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemXmlSerializationXmlElementAttribute);
            this.XmlEnumAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemXmlSerializationXmlEnumAttribute);
            this.XmlIgnoreAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemXmlSerializationXmlIgnoreAttribute);
            this.XmlIncludeAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemXmlSerializationXmlIncludeAttribute);
            this.XmlRootAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemXmlSerializationXmlRootAttribute);
            this.XmlTextAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemXmlSerializationXmlTextAttribute);
            this.XmlTypeAttribute = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                WellKnownTypeNames.SystemXmlSerializationXmlTypeAttribute);

            this.Any =
                this.XmlAnyAttributeAttribute != null
                || this.XmlAnyElementAttribute != null
                || this.XmlArrayAttribute != null
                || this.XmlArrayItemAttribute != null
                || this.XmlAttributeAttribute != null
                || this.XmlChoiceIdentifierAttribute != null
                || this.XmlElementAttribute != null
                || this.XmlEnumAttribute != null
                || this.XmlIgnoreAttribute != null
                || this.XmlIncludeAttribute != null
                || this.XmlRootAttribute != null
                || this.XmlTextAttribute != null
                || this.XmlTypeAttribute != null;
        }

        /// <summary>
        /// Determines if the given symbol has any XML serialization attributes on it.
        /// </summary>
        /// <param name="symbol">Symbol whose attributes to look through.</param>
        /// <returns>True if the symbol has an XML serialization attribute on it, false otherwise.</returns>
        public bool HasAnyAttribute(ISymbol symbol)
        {
            return symbol.GetAttributes().Any(attributeData =>
                attributeData.AttributeClass.Equals(this.XmlAnyAttributeAttribute)
                || attributeData.AttributeClass.Equals(this.XmlAnyElementAttribute)
                || attributeData.AttributeClass.Equals(this.XmlArrayAttribute)
                || attributeData.AttributeClass.Equals(this.XmlArrayItemAttribute)
                || attributeData.AttributeClass.Equals(this.XmlAttributeAttribute)
                || attributeData.AttributeClass.Equals(this.XmlChoiceIdentifierAttribute)
                || attributeData.AttributeClass.Equals(this.XmlElementAttribute)
                || attributeData.AttributeClass.Equals(this.XmlEnumAttribute)
                || attributeData.AttributeClass.Equals(this.XmlIgnoreAttribute)
                || attributeData.AttributeClass.Equals(this.XmlIncludeAttribute)
                || attributeData.AttributeClass.Equals(this.XmlRootAttribute)
                || attributeData.AttributeClass.Equals(this.XmlTextAttribute)
                || attributeData.AttributeClass.Equals(this.XmlTypeAttribute));
        }
    }
}
