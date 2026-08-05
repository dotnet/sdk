// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Security
{
    using static MicrosoftNetCoreAnalyzersResources;

    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseXmlReaderForDeserialize : UseXmlReaderBase
    {
        internal const string DiagnosticId = "CA5369";

        internal static readonly DiagnosticDescriptor RealRule = DiagnosticDescriptorHelper.Create(
            DiagnosticId,
            CreateLocalizableResourceString(nameof(UseXmlReaderForDeserialize)),
            Message,
            DiagnosticCategory.Security,
            RuleLevel.IdeHidden_BulkConfigurable,
            description: Description,
            isPortedFxCopRule: false,
            isDataflowRule: false);

        protected override string TypeMetadataName => WellKnownTypeNames.SystemXmlSerializationXmlSerializer;

        protected override string MethodMetadataName => "Deserialize";

        protected override DiagnosticDescriptor Rule => RealRule;
    }
}
