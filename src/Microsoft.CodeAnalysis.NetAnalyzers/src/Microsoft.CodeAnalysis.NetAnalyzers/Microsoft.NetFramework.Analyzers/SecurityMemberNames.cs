// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NetFramework.Analyzers
{
    /// <summary>
    /// Specifies the member names used by security analyzers
    /// </summary>
    public static class SecurityMemberNames
    {
        public const string Create = "Create";
        public const string Load = "Load";
        public const string LoadXml = "LoadXml";
        public const string Read = "Read";
        public const string ReadXml = "ReadXml";
        public const string ReadXmlSchema = "ReadXmlSchema";
        public const string Deserialize = "Deserialize";
        public const string InnerXml = "InnerXml";
        public const string DataViewSettingCollectionString = "DataViewSettingCollectionString";
        public const string DtdProcessing = "DtdProcessing";
        public const string Parse = "Parse";
        public const string XmlResolver = "XmlResolver";
        public const string TrustedXslt = "TrustedXslt";
        public const string Default = "Default";
        public const string EnableDocumentFunction = "EnableDocumentFunction";
        public const string EnableScript = "EnableScript";
        public const string MaxCharactersFromEntities = "MaxCharactersFromEntities";
    }
}