// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Analyzer.Utilities;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// For detecting deserialization with <see cref="T:System.Runtime.Serialization.Formatters.Binary.NetDataContractSerializer"/>.
    /// </summary>
    [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    internal class DoNotUseInsecureDeserializerNetDataContractSerializerMethods : DoNotUseInsecureDeserializerMethodsBase
    {
        internal static readonly DiagnosticDescriptor RealMethodUsedDescriptor =
            SecurityHelpers.CreateDiagnosticDescriptor(
                "CA2310",
                nameof(NetDataContractSerializerMethodUsedTitle),
                nameof(NetDataContractSerializerMethodUsedMessage),
                RuleLevel.Disabled,
                isPortedFxCopRule: false,
                isDataflowRule: false,
                isReportedAtCompilationEnd: false,
                descriptionResourceStringName: nameof(NetDataContractSerializerMethodUsedDescription));

        protected override string DeserializerTypeMetadataName =>
            WellKnownTypeNames.SystemRuntimeSerializationNetDataContractSerializer;

        protected override ImmutableHashSet<string> DeserializationMethodNames =>
            SecurityHelpers.NetDataContractSerializerDeserializationMethods;

        protected override DiagnosticDescriptor MethodUsedDescriptor => RealMethodUsedDescriptor;
    }
}
