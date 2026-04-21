// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Immutable;
using System.Diagnostics;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Security
{
    /// <summary>
    /// Base class for insecure deserializer analyzers.
    /// </summary>
    /// <remarks>This aids in implementing:
    /// 1. Detecting potentially insecure deserialization method calls.
    /// 2. Detecting references to potentially insecure methods.
    /// </remarks>
    public abstract class DoNotUseInsecureDeserializerMethodsBase : DiagnosticAnalyzer
    {
        /// <summary>
        /// Metadata name of the potentially insecure deserializer type.
        /// </summary>
        protected abstract string DeserializerTypeMetadataName { get; }

        /// <summary>
        /// Metadata names of potentially insecure methods.
        /// </summary>
        /// <remarks>Use <see cref="StringComparer.Ordinal"/>.</remarks>
        protected abstract ImmutableHashSet<string> DeserializationMethodNames { get; }

        /// <summary>
        /// <see cref="DiagnosticDescriptor"/> for when a potentially insecure method is invoked
        /// or referenced (e.g. used as a delegate).
        /// </summary>
        /// <remarks>The string format message argument is the method signature.</remarks>
        protected abstract DiagnosticDescriptor MethodUsedDescriptor { get; }

        /// <summary>
        /// Allows the inheritor to choose different diagnostics based on the operation that will get reported.
        /// </summary>
        /// <param name="operationAnalysisContext">Context for the operation to be reported.</param>
        /// <param name="wellKnownTypeProvider"><see cref="WellKnownTypeProvider"/> for the operation's compilation.</param>
        /// <returns>Diagnostic descriptor to report, or null if no diagnostic should be reported.</returns>
        /// <remarks>If you override this to choose among multiple diagnostic descriptors, you'll also need to override
        /// <see cref="SupportedDiagnostics"/> to contain all possible diagnostic descriptors.</remarks>
        protected virtual DiagnosticDescriptor? ChooseDiagnosticDescriptor(
            OperationAnalysisContext operationAnalysisContext,
            WellKnownTypeProvider wellKnownTypeProvider)
        {
            return MethodUsedDescriptor;
        }

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
            ImmutableArray.Create<DiagnosticDescriptor>(
                this.MethodUsedDescriptor);

        public sealed override void Initialize(AnalysisContext context)
        {
            ImmutableHashSet<string> cachedDeserializationMethodNames = this.DeserializationMethodNames;

            Debug.Assert(!cachedDeserializationMethodNames.IsEmpty);

            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartAnalysisContext) =>
                {
                    WellKnownTypeProvider wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(
                        compilationStartAnalysisContext.Compilation);
                    INamedTypeSymbol? deserializerTypeSymbol =
                        wellKnownTypeProvider.GetOrCreateTypeByMetadataName(this.DeserializerTypeMetadataName);
                    if (deserializerTypeSymbol == null)
                    {
                        return;
                    }

                    compilationStartAnalysisContext.RegisterOperationAction(
                        (OperationAnalysisContext operationAnalysisContext) =>
                        {
                            IInvocationOperation invocationOperation =
                                (IInvocationOperation)operationAnalysisContext.Operation;
                            if (invocationOperation.Instance?.Type?.DerivesFrom(deserializerTypeSymbol) == true
                                && cachedDeserializationMethodNames.Contains(invocationOperation.TargetMethod.MetadataName))
                            {
                                DiagnosticDescriptor? chosenDiagnostic =
                                    this.ChooseDiagnosticDescriptor(operationAnalysisContext, wellKnownTypeProvider);
                                if (chosenDiagnostic != null)
                                {
                                    operationAnalysisContext.ReportDiagnostic(
                                        invocationOperation.CreateDiagnostic(
                                            chosenDiagnostic,
                                            invocationOperation.TargetMethod.ToDisplayString(
                                                SymbolDisplayFormat.MinimallyQualifiedFormat)));
                                }
                            }
                        },
                        OperationKind.Invocation);

                    compilationStartAnalysisContext.RegisterOperationAction(
                        (OperationAnalysisContext operationAnalysisContext) =>
                        {
                            IMethodReferenceOperation methodReferenceOperation =
                                (IMethodReferenceOperation)operationAnalysisContext.Operation;
                            if (methodReferenceOperation.Instance?.Type?.DerivesFrom(deserializerTypeSymbol) == true
                                && cachedDeserializationMethodNames.Contains(methodReferenceOperation.Method.MetadataName))
                            {
                                DiagnosticDescriptor? chosenDiagnostic =
                                    this.ChooseDiagnosticDescriptor(operationAnalysisContext, wellKnownTypeProvider);
                                if (chosenDiagnostic != null)
                                {
                                    operationAnalysisContext.ReportDiagnostic(
                                        methodReferenceOperation.CreateDiagnostic(
                                            chosenDiagnostic,
                                            methodReferenceOperation.Method.ToDisplayString(
                                                SymbolDisplayFormat.MinimallyQualifiedFormat)));
                                }
                            }
                        },
                        OperationKind.MethodReference);
                });
        }
    }
}
