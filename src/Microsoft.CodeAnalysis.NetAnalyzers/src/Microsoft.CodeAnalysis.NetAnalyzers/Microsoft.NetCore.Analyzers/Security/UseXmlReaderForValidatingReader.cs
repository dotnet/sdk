// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseXmlReaderForValidatingReader : UseXmlReaderBase
    {
        internal const string DiagnosticId = "CA5370";
        private static readonly LocalizableString s_Title = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.UseXmlReaderForValidatingReader),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor RealRule = DiagnosticDescriptorHelper.Create(
                DiagnosticId,
                s_Title,
                Message,
                DiagnosticCategory.Security,
                RuleLevel.IdeHidden_BulkConfigurable,
                description: Description,
                isPortedFxCopRule: false,
                isDataflowRule: false);

        protected override string TypeMetadataName => WellKnownTypeNames.SystemXmlXmlValidatingReader;

        protected override string MethodMetadataName => "XmlValidatingReader";

        protected override DiagnosticDescriptor Rule => RealRule;
    }
}
