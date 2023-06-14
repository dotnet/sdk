// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;

namespace Microsoft.NetFramework.Analyzers.Helpers
{
    public class CompilationSecurityTypes(Compilation compilation)
    {
        public INamedTypeSymbol? HandleProcessCorruptedStateExceptionsAttribute { get; private set; } =
                compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeExceptionServicesHandleProcessCorruptedStateExceptionsAttribute);
        public INamedTypeSymbol? SystemObject { get; private set; } = compilation.GetSpecialType(SpecialType.System_Object);
        public INamedTypeSymbol? SystemException { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemException);
        public INamedTypeSymbol? SystemSystemException { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSystemException);
        public INamedTypeSymbol? XmlDocument { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXmlDocument);
        public INamedTypeSymbol? XPathDocument { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXPathXPathDocument);
        public INamedTypeSymbol? XmlSchema { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlSchemaXmlSchema);
        public INamedTypeSymbol? DataSet { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDataDataSet);
        public INamedTypeSymbol? XmlSerializer { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlSerializationXmlSerializer);
        public INamedTypeSymbol? DataTable { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDataDataTable);
        public INamedTypeSymbol? XmlNode { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXmlNode);
        public INamedTypeSymbol? DataViewManager { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDataDataViewManager);
        public INamedTypeSymbol? XmlTextReader { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXmlTextReader);
        public INamedTypeSymbol? XmlReader { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXmlReader);
        public INamedTypeSymbol? DtdProcessing { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlDtdProcessing);
        public INamedTypeSymbol? XmlReaderSettings { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXmlReaderSettings);
        public INamedTypeSymbol? XslCompiledTransform { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXslXslCompiledTransform);
        public INamedTypeSymbol? XmlResolver { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXmlResolver);
        public INamedTypeSymbol? XmlSecureResolver { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXmlSecureResolver);
        public INamedTypeSymbol? XsltSettings { get; private set; } = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXslXsltSettings);
    }
}
