// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.NetCore.Analyzers.Security.Helpers;

namespace Microsoft.NetCore.Analyzers.Security
{
    /// <summary>
    /// For detecting deserialization of <see cref="T:System.Data.DataSet"/> or <see cref="T:System.Data.DataTable"/> in an
    /// IFormatter deserialized object graph.
    /// </summary>
    [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
    public abstract class DataSetDataTableInIFormatterSerializableObjectGraphAnalyzer : DiagnosticAnalyzer
    {
        internal static readonly DiagnosticDescriptor ObjectGraphContainsDangerousTypeDescriptor =
            SecurityHelpers.CreateDiagnosticDescriptor(
                "CA2354",
                nameof(MicrosoftNetCoreAnalyzersResources.DataSetDataTableInRceDeserializableObjectGraphTitle),
                nameof(MicrosoftNetCoreAnalyzersResources.DataSetDataTableInRceDeserializableObjectGraphMessage),
                RuleLevel.Disabled,
                isPortedFxCopRule: false,
                isDataflowRule: false,
                isReportedAtCompilationEnd: false);

        public sealed override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create(ObjectGraphContainsDangerousTypeDescriptor);

        protected abstract string ToString(TypedConstant typedConstant);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartAnalysisContext) =>
                {
                    Compilation? compilation = compilationStartAnalysisContext.Compilation;
                    WellKnownTypeProvider wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilation);

                    if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                                WellKnownTypeNames.SystemDataDataSet,
                                out INamedTypeSymbol? dataSetTypeSymbol)
                        || !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                                WellKnownTypeNames.SystemDataDataTable,
                                out INamedTypeSymbol? dataTableTypeSymbol))
                    {
                        return;
                    }

                    INamedTypeSymbol? serializableAttributeTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemSerializableAttribute);
                    INamedTypeSymbol? nonSerializedAttributeTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemSerializableAttribute);
                    INamedTypeSymbol? binaryFormatterTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemRuntimeSerializationFormattersBinaryBinaryFormatter);
                    INamedTypeSymbol? netDataContractSerializerTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemRuntimeSerializationNetDataContractSerializer);
                    INamedTypeSymbol? objectStateFormatterTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemWebUIObjectStateFormatter);
                    INamedTypeSymbol? soapFormatterTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemRuntimeSerializationFormattersSoapSoapFormatter);

                    if (serializableAttributeTypeSymbol == null
                        || (binaryFormatterTypeSymbol == null
                            && netDataContractSerializerTypeSymbol == null
                            && objectStateFormatterTypeSymbol == null
                            && soapFormatterTypeSymbol == null))
                    {
                        return;
                    }

                    InsecureDeserializationTypeDecider decider = InsecureDeserializationTypeDecider.GetOrCreate(compilation);

                    compilationStartAnalysisContext.RegisterOperationAction(
                        (OperationAnalysisContext operationAnalysisContext) =>
                        {
                            IInvocationOperation invocationOperation =
                                (IInvocationOperation)operationAnalysisContext.Operation;
                            string methodName = invocationOperation.TargetMethod.MetadataName;
                            if (!(((invocationOperation.Instance?.Type?.DerivesFrom(binaryFormatterTypeSymbol) == true
                                            && SecurityHelpers.BinaryFormatterDeserializationMethods.Contains(methodName))
                                        || (invocationOperation.Instance?.Type?.DerivesFrom(netDataContractSerializerTypeSymbol) == true
                                            && SecurityHelpers.NetDataContractSerializerDeserializationMethods.Contains(methodName))
                                        || (invocationOperation.Instance?.Type?.DerivesFrom(objectStateFormatterTypeSymbol) == true
                                            && SecurityHelpers.ObjectStateFormatterDeserializationMethods.Contains(methodName))
                                        || (invocationOperation.Instance?.Type?.DerivesFrom(soapFormatterTypeSymbol) == true
                                            && SecurityHelpers.SoapFormatterDeserializationMethods.Contains(methodName)))
                                    && invocationOperation.Parent?.Kind == OperationKind.Conversion
                                    && invocationOperation.Parent is IConversionOperation conversionOperation))
                            {
                                return;
                            }

                            ITypeSymbol deserializedType = conversionOperation.Type;

                            ObjectGraphOptions options;
                            if (invocationOperation.Instance?.Type?.DerivesFrom(netDataContractSerializerTypeSymbol) == true)
                            {
                                options = ObjectGraphOptions.DataContractOptions;
                            }
                            else
                            {
                                options = ObjectGraphOptions.BinarySerializationOptions;
                            }

                            if (decider.IsObjectGraphInsecure(
                                    deserializedType,
                                    options,
                                    out ImmutableArray<InsecureObjectGraphResult> results))
                            {
                                foreach (InsecureObjectGraphResult result in results)
                                {
                                    operationAnalysisContext.ReportDiagnostic(
                                        Diagnostic.Create(
                                            ObjectGraphContainsDangerousTypeDescriptor,
                                            invocationOperation.Parent.Syntax.GetLocation(),
                                            result.InsecureType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                                            result.GetDisplayString(typedConstant => ToString(typedConstant))));
                                }
                            }
                        },
                        OperationKind.Invocation);
                });
        }
    }
}
