// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Security
{
    public abstract class UseXmlReaderBase : DiagnosticAnalyzer
    {
        /// <summary>
        /// Metadata name of the type which is recommended to use method take XmlReader as parameter.
        /// </summary>
        protected abstract string TypeMetadataName { get; }

        /// <summary>
        /// Metadata name of the method which is recommended to use XmlReader as parameter.
        /// </summary>
        protected abstract string MethodMetadataName { get; }

        protected abstract DiagnosticDescriptor Rule { get; }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        protected static LocalizableString Description { get; } = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.UseXmlReaderDescription),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        protected static LocalizableString Message { get; } = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.UseXmlReaderMessage),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(compilationStartAnalysisContext =>
            {
                var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilationStartAnalysisContext.Compilation);

                if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                            TypeMetadataName,
                            out INamedTypeSymbol? xmlSchemaTypeSymbol))
                {
                    return;
                }

                INamedTypeSymbol? xmlReaderTypeSymbol = wellKnownTypeProvider.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemXmlXmlReader);

                compilationStartAnalysisContext.RegisterOperationAction(operationAnalysisContext =>
                {
                    var operation = operationAnalysisContext.Operation;
                    IMethodSymbol? methodSymbol = null;
                    string? methodName = null;

                    switch (operation.Kind)
                    {
                        case OperationKind.Invocation:
                            methodSymbol = ((IInvocationOperation)operation).TargetMethod;
                            methodName = methodSymbol.Name;
                            break;

                        case OperationKind.ObjectCreation:
                            methodSymbol = ((IObjectCreationOperation)operation).Constructor;
                            methodName = methodSymbol.ContainingType.Name;
                            break;

                        default:
                            return;
                    }

                    if (methodName.StartsWith(MethodMetadataName, StringComparison.Ordinal) &&
                        methodSymbol.IsOverrideOrVirtualMethodOf(xmlSchemaTypeSymbol))
                    {
                        if (xmlReaderTypeSymbol != null &&
                            !methodSymbol.Parameters.IsEmpty &&
                            methodSymbol.Parameters[0].Type.Equals(xmlReaderTypeSymbol))
                        {
                            return;
                        }

                        operationAnalysisContext.ReportDiagnostic(
                            operation.CreateDiagnostic(
                                Rule,
                                methodSymbol.ContainingType.Name,
                                methodName));
                    }
                }, OperationKind.Invocation, OperationKind.ObjectCreation);
            });
        }
    }
}
