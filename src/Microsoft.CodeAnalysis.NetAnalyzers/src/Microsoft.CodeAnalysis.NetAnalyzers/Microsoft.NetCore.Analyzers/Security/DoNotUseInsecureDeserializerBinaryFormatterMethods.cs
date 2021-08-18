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
    /// For detecting deserialization with <see cref="T:System.Runtime.Serialization.Formatters.Binary.BinaryFormatter"/>.
    /// </summary>
    [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    internal class DoNotUseInsecureDeserializerBinaryFormatterMethods : DoNotUseInsecureDeserializerMethodsBase
    {
        internal static readonly DiagnosticDescriptor RealMethodUsedDescriptor =
            SecurityHelpers.CreateDiagnosticDescriptor(
                "CA2300",
                nameof(MicrosoftNetCoreAnalyzersResources.BinaryFormatterMethodUsedTitle),
                nameof(MicrosoftNetCoreAnalyzersResources.BinaryFormatterMethodUsedMessage),
                RuleLevel.Disabled,
                isPortedFxCopRule: false,
                isDataflowRule: false,
                isReportedAtCompilationEnd: false,
                descriptionResourceStringName: nameof(MicrosoftNetCoreAnalyzersResources.BinaryFormatterMethodUsedDescription));

        protected override string DeserializerTypeMetadataName =>
            WellKnownTypeNames.SystemRuntimeSerializationFormattersBinaryBinaryFormatter;

        protected override ImmutableHashSet<string> DeserializationMethodNames =>
            SecurityHelpers.BinaryFormatterDeserializationMethods;

        protected override DiagnosticDescriptor MethodUsedDescriptor => RealMethodUsedDescriptor;
    }
}
