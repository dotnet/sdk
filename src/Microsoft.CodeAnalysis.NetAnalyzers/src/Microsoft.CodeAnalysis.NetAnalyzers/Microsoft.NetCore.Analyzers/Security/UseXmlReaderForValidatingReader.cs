// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Security
{
    using static MicrosoftNetCoreAnalyzersResources;

    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseXmlReaderForValidatingReader : UseXmlReaderBase
    {
        internal const string DiagnosticId = "CA5370";

        internal static readonly DiagnosticDescriptor RealRule = DiagnosticDescriptorHelper.Create(
            DiagnosticId,
            CreateLocalizableResourceString(nameof(UseXmlReaderForValidatingReader)),
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
