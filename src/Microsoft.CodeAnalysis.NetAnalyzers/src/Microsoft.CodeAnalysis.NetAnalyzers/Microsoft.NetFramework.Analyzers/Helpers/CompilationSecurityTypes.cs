// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;

namespace Microsoft.NetFramework.Analyzers.Helpers
{
    public class CompilationSecurityTypes
    {
        public INamedTypeSymbol? HandleProcessCorruptedStateExceptionsAttribute { get; private set; }
        public INamedTypeSymbol? SystemObject { get; private set; }
        public INamedTypeSymbol? SystemException { get; private set; }
        public INamedTypeSymbol? SystemSystemException { get; private set; }
        public INamedTypeSymbol? XmlDocument { get; private set; }
        public INamedTypeSymbol? XPathDocument { get; private set; }
        public INamedTypeSymbol? XmlSchema { get; private set; }
        public INamedTypeSymbol? DataSet { get; private set; }
        public INamedTypeSymbol? XmlSerializer { get; private set; }
        public INamedTypeSymbol? DataTable { get; private set; }
        public INamedTypeSymbol? XmlNode { get; private set; }
        public INamedTypeSymbol? DataViewManager { get; private set; }
        public INamedTypeSymbol? XmlTextReader { get; private set; }
        public INamedTypeSymbol? XmlReader { get; private set; }
        public INamedTypeSymbol? DtdProcessing { get; private set; }
        public INamedTypeSymbol? XmlReaderSettings { get; private set; }
        public INamedTypeSymbol? XslCompiledTransform { get; private set; }
        public INamedTypeSymbol? XmlResolver { get; private set; }
        public INamedTypeSymbol? XmlSecureResolver { get; private set; }
        public INamedTypeSymbol? XsltSettings { get; private set; }

        public CompilationSecurityTypes(Compilation compilation)
        {
            HandleProcessCorruptedStateExceptionsAttribute =
                compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeExceptionServicesHandleProcessCorruptedStateExceptionsAttribute);
            SystemObject = compilation.GetSpecialType(SpecialType.System_Object);
            SystemException = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemException);
            SystemSystemException = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSystemException);
            XmlDocument = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXmlDocument);
            XPathDocument = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXPathXPathDocument);
            XmlSchema = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlSchemaXmlSchema);
            DataSet = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDataDataSet);
            XmlSerializer = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlSerializationXmlSerializer);
            DataTable = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDataDataTable);
            XmlNode = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXmlNode);
            DataViewManager = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemDataDataViewManager);
            XmlTextReader = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXmlTextReader);
            XmlReader = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXmlReader);
            DtdProcessing = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlDtdProcessing);
            XmlReaderSettings = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXmlReaderSettings);
            XslCompiledTransform = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXslXslCompiledTransform);
            XmlResolver = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXmlResolver);
            XmlSecureResolver = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXmlSecureResolver);
            XsltSettings = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXslXsltSettings);
        }
    }
}
