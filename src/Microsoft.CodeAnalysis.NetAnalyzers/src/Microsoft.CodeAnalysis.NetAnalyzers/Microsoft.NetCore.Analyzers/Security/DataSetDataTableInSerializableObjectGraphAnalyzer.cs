// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
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
    /// deserialized object graph for certain serializers.
    /// </summary>
    /// <remarks>
    /// Serializers:
    /// - DataContractSerializer
    /// - DataContractJsonSerializer
    /// - JavaScriptSerializer
    /// - XmlSerializer
    /// - Newtonsoft Json.NET (partial)
    /// </remarks>
    [SuppressMessage("Documentation", "CA1200:Avoid using cref tags with a prefix", Justification = "The comment references a type that is not referenced by this compilation.")]
    public abstract class DataSetDataTableInSerializableObjectGraphAnalyzer : DiagnosticAnalyzer
    {
        internal static readonly DiagnosticDescriptor ObjectGraphContainsDangerousTypeDescriptor =
            SecurityHelpers.CreateDiagnosticDescriptor(
                "CA2355",
                nameof(MicrosoftNetCoreAnalyzersResources.DataSetDataTableInDeserializableObjectGraphTitle),
                nameof(MicrosoftNetCoreAnalyzersResources.DataSetDataTableInDeserializableObjectGraphMessage),
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

                    INamedTypeSymbol? dataContractSerializerTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemRuntimeSerializationDataContractSerializer);
                    INamedTypeSymbol? dataContractJsonSerializerTypeSymbol =
                        wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                            WellKnownTypeNames.SystemRuntimeSerializationJsonDataContractJsonSerializer);
                    INamedTypeSymbol? javaScriptSerializerTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemWebScriptSerializationJavaScriptSerializer);
                    INamedTypeSymbol? typeTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemType);
                    INamedTypeSymbol? xmlSerializerTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemXmlSerializationXmlSerializer);
                    INamedTypeSymbol? jsonNetJsonSerializerTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.NewtonsoftJsonJsonSerializer);
                    INamedTypeSymbol? jsonNetJsonConvertTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.NewtonsoftJsonJsonConvert);

                    if (dataContractSerializerTypeSymbol == null
                        && dataContractJsonSerializerTypeSymbol == null
                        && javaScriptSerializerTypeSymbol == null
                        && xmlSerializerTypeSymbol == null
                        && jsonNetJsonSerializerTypeSymbol == null
                        && jsonNetJsonConvertTypeSymbol == null)
                    {
                        return;
                    }

                    InsecureDeserializationTypeDecider decider = InsecureDeserializationTypeDecider.GetOrCreate(compilation);

                    compilationStartAnalysisContext.RegisterOperationAction(
                        (OperationAnalysisContext operationAnalysisContext) =>
                        {
                            IInvocationOperation invocationOperation =
                                (IInvocationOperation)operationAnalysisContext.Operation;
                            if (!IsDeserializationMethod(
                                    invocationOperation,
                                    out ObjectGraphOptions? optionsToUse,
                                    out IEnumerable<(ITypeSymbol DeserializedTypeSymbol, IOperation OperationForLocation)>? deserializedTypes))
                            {
                                return;
                            }

                            RoslynDebug.Assert(optionsToUse != null);
                            RoslynDebug.Assert(deserializedTypes != null);

                            ReportDiagnosticsForInsecureTypes(operationAnalysisContext, optionsToUse, deserializedTypes);
                        },
                        OperationKind.Invocation);

                    compilationStartAnalysisContext.RegisterOperationAction(
                        (OperationAnalysisContext operationAnalysisContext) =>
                        {
                            IObjectCreationOperation objectCreationOperation =
                                (IObjectCreationOperation)operationAnalysisContext.Operation;
                            if (!IsDeserializationConstructor(
                                    objectCreationOperation,
                                    out ObjectGraphOptions? optionsToUse,
                                    out IEnumerable<(ITypeSymbol DeserializedTypeSymbol, IOperation OperationForLocation)>? deserializedTypes))
                            {
                                return;
                            }

                            RoslynDebug.Assert(optionsToUse != null);
                            RoslynDebug.Assert(deserializedTypes != null);

                            ReportDiagnosticsForInsecureTypes(operationAnalysisContext, optionsToUse, deserializedTypes);
                        },
                        OperationKind.ObjectCreation);

                    return;

                    // Local functions.

                    // Determines if the invoked method is for deserialization, and what type of deserialization.
                    bool IsDeserializationMethod(
                        IInvocationOperation invocationOperation,
                        out ObjectGraphOptions? optionsToUse,
                        out IEnumerable<(ITypeSymbol DeserializedTypeSymbol, IOperation OperationForLocation)>? deserializedTypes)
                    {
                        optionsToUse = null;
                        deserializedTypes = null;

                        IMethodSymbol targetMethod = invocationOperation.TargetMethod;
                        if (invocationOperation.Instance?.Type?.DerivesFrom(javaScriptSerializerTypeSymbol) == true)
                        {
                            if (targetMethod.MetadataName == "DeserializeObject"
                                && invocationOperation.Parent?.Kind == OperationKind.Conversion
                                && invocationOperation.Parent is IConversionOperation javaScriptConversionOperation)
                            {
                                optionsToUse = ObjectGraphOptions.JavaScriptSerializerOptions;
                                deserializedTypes = new[]
                                {
                                    (javaScriptConversionOperation.Type, (IOperation)javaScriptConversionOperation)
                                };
                            }
                            else if (targetMethod.MetadataName == "Deserialize")
                            {
                                if (targetMethod.IsGenericMethod
                                    && targetMethod.Arity == 1
                                    && targetMethod.Parameters.Length == 1)
                                {
                                    optionsToUse = ObjectGraphOptions.JavaScriptSerializerOptions;
                                    deserializedTypes = new[]
                                    {
                                        (targetMethod.TypeArguments[0], (IOperation)invocationOperation)
                                    };
                                }
                                else if (!targetMethod.IsGenericMethod
                                    && targetMethod.Parameters.Length == 2
                                    && targetMethod.Parameters[1].Type.Equals(typeTypeSymbol)
                                    && invocationOperation.HasArgument(out ITypeOfOperation? typeOfOperation))
                                {
                                    optionsToUse = ObjectGraphOptions.JavaScriptSerializerOptions;
                                    deserializedTypes = new[]
                                    {
                                        (typeOfOperation.TypeOperand, (IOperation)typeOfOperation)
                                    };
                                }
                            }
                        }
                        else if (targetMethod.ContainingType.Equals(xmlSerializerTypeSymbol)
                            && targetMethod.IsStatic
                            && targetMethod.MetadataName == "FromTypes"
                            && targetMethod.Parameters.Length == 1
                            && targetMethod.Parameters[0].Type is IArrayTypeSymbol arrayTypeSymbol
                            && arrayTypeSymbol.ElementType.Equals(typeTypeSymbol))
                        {
                            optionsToUse = ObjectGraphOptions.XmlSerializerOptions;
                            deserializedTypes =
                                invocationOperation
                                    .Arguments[0]
                                    .Descendants()
                                    .OfType<ITypeOfOperation>()
                                    .Select(t => (t.TypeOperand!, (IOperation)t));
                        }
                        else if ((invocationOperation.Instance?.Type.DerivesFrom(jsonNetJsonSerializerTypeSymbol) == true
                                    && targetMethod.MetadataName == "Deserialize")
                            || (targetMethod.ContainingType.Equals(jsonNetJsonConvertTypeSymbol)
                                    && targetMethod.MetadataName == "DeserializeObject"))
                        {
                            if (targetMethod.IsGenericMethod && targetMethod.Arity == 1)
                            {
                                optionsToUse = ObjectGraphOptions.NewtonsoftJsonNetOptions;
                                deserializedTypes = new[]
                                {
                                    (targetMethod.TypeArguments[0], (IOperation)invocationOperation)
                                };
                            }
                            else if (targetMethod.Parameters.Length == 2
                                && targetMethod.Parameters[1].Type.Equals(typeTypeSymbol)
                                && invocationOperation.HasArgument(out ITypeOfOperation? typeOfOperation))
                            {
                                optionsToUse = ObjectGraphOptions.NewtonsoftJsonNetOptions;
                                deserializedTypes = new[]
                                {
                                    (typeOfOperation.TypeOperand, (IOperation)typeOfOperation)
                                };
                            }
                            else if (invocationOperation.Parent?.Kind == OperationKind.Conversion
                                && invocationOperation.Parent is IConversionOperation conversionOperation)
                            {
                                optionsToUse = ObjectGraphOptions.NewtonsoftJsonNetOptions;
                                deserializedTypes = new[]
                                {
                                    (conversionOperation.Type, (IOperation)conversionOperation)
                                };
                            }
                        }

                        return optionsToUse != null && deserializedTypes != null;
                    }

                    // Determines if the object instantiation is for deserialization, and the type of deserialization.
                    bool IsDeserializationConstructor(
                        IObjectCreationOperation objectCreationOperation,
                        out ObjectGraphOptions? optionsToUse,
                        out IEnumerable<(ITypeSymbol DeserializedTypeSymbol, IOperation OperationForLocation)>? deserializedTypes)
                    {
                        optionsToUse = null;
                        deserializedTypes = null;

                        IMethodSymbol constructor = objectCreationOperation.Constructor;
                        if (objectCreationOperation.Type?.Equals(dataContractSerializerTypeSymbol) == true
                            || objectCreationOperation.Type?.Equals(dataContractJsonSerializerTypeSymbol) == true)
                        {
                            optionsToUse = ObjectGraphOptions.DataContractOptions;
                            deserializedTypes =
                                objectCreationOperation
                                    .Arguments
                                    .SelectMany(a => a.Descendants())
                                    .OfType<ITypeOfOperation>()
                                    .Select(t => (t.TypeOperand!, (IOperation)t));
                        }
                        else if (objectCreationOperation.Type?.Equals(xmlSerializerTypeSymbol) == true)
                        {
                            optionsToUse = ObjectGraphOptions.XmlSerializerOptions;
                            deserializedTypes =
                                objectCreationOperation
                                    .Arguments
                                    .SelectMany(a => a.Descendants())
                                    .OfType<ITypeOfOperation>()
                                    .Select(t => (t.TypeOperand!, (IOperation)t));
                        }

                        return optionsToUse != null && deserializedTypes != null;
                    }

                    // For each deserialized type, determine if its object graph potentially contains an insecure type, and if
                    // report a diagnostic if so.
                    void ReportDiagnosticsForInsecureTypes(
                        OperationAnalysisContext operationAnalysisContext,
                        ObjectGraphOptions optionsToUse,
                        IEnumerable<(ITypeSymbol DeserializedTypeSymbol, IOperation OperationForLocation)> deserializedTypes)
                    {
                        foreach ((ITypeSymbol deserializedTypeSymbol, IOperation operationForLocation) in deserializedTypes)
                        {
                            if (decider.IsObjectGraphInsecure(
                                    deserializedTypeSymbol,
                                    optionsToUse,
                                    out ImmutableArray<InsecureObjectGraphResult> results))
                            {
                                foreach (InsecureObjectGraphResult result in results)
                                {
                                    operationAnalysisContext.ReportDiagnostic(
                                        Diagnostic.Create(
                                            ObjectGraphContainsDangerousTypeDescriptor,
                                            operationForLocation.Syntax.GetLocation(),
                                            result.InsecureType.ToDisplayString(SymbolDisplayFormat.MinimallyQualifiedFormat),
                                            result.GetDisplayString(typedConstant => ToString(typedConstant))));
                                }
                            }
                        }
                    }
                });
        }
    }
}
