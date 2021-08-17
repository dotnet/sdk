// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    /// <summary>
    /// For detecting deserialization with <see cref="T:System.Runtime.Serialization.Formatters.Binary.BinaryFormatter"/> when its Binder property is not set.
    /// </summary>
    [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public class DoNotUseInsecureDeserializerBinaryFormatterWithoutBinder : DoNotUseInsecureDeserializerWithoutBinderBase
    {
        internal static readonly DiagnosticDescriptor RealBinderDefinitelyNotSetDescriptor =
            SecurityHelpers.CreateDiagnosticDescriptor(
                "CA2301",
                nameof(MicrosoftNetCoreAnalyzersResources.BinaryFormatterDeserializeWithoutBinderSetTitle),
                nameof(MicrosoftNetCoreAnalyzersResources.BinaryFormatterDeserializeWithoutBinderSetMessage),
                RuleLevel.Disabled,
                isPortedFxCopRule: false,
                isDataflowRule: true,
                isReportedAtCompilationEnd: true);
        internal static readonly DiagnosticDescriptor RealBinderMaybeNotSetDescriptor =
            SecurityHelpers.CreateDiagnosticDescriptor(
                "CA2302",
                nameof(MicrosoftNetCoreAnalyzersResources.BinaryFormatterDeserializeMaybeWithoutBinderSetTitle),
                nameof(MicrosoftNetCoreAnalyzersResources.BinaryFormatterDeserializeMaybeWithoutBinderSetMessage),
                RuleLevel.Disabled,
                isPortedFxCopRule: false,
                isDataflowRule: true,
                isReportedAtCompilationEnd: true);

        protected override string DeserializerTypeMetadataName =>
            WellKnownTypeNames.SystemRuntimeSerializationFormattersBinaryBinaryFormatter;

        protected override string SerializationBinderPropertyMetadataName => "Binder";

        protected override ImmutableHashSet<string> DeserializationMethodNames =>
            SecurityHelpers.BinaryFormatterDeserializationMethods;

        protected override DiagnosticDescriptor BinderDefinitelyNotSetDescriptor => RealBinderDefinitelyNotSetDescriptor;

        protected override DiagnosticDescriptor BinderMaybeNotSetDescriptor => RealBinderMaybeNotSetDescriptor;
    }
}
