// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    using static MicrosoftNetCoreAnalyzersResources;

    /// <summary>
    /// CA1849: <inheritdoc cref="UseAsyncMethodInAsyncContextTitle"/>
    /// This analyzer suggests using the async version of a method when inside a Task-returning method
    /// In addition, calling Task.Wait(), Task.Result or Task.GetAwaiter().GetResult() will produce a diagnostic
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseAsyncMethodInAsyncContext : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1849";
        internal const string AsyncMethodKeyName = "AsyncMethodName";
        internal const string MandatoryAsyncSuffix = "Async";

        private static readonly LocalizableString s_localizableTitle = CreateLocalizableResourceString(nameof(UseAsyncMethodInAsyncContextTitle));
        private static readonly LocalizableString s_localizableDescription = CreateLocalizableResourceString(nameof(UseAsyncMethodInAsyncContextDescription));

        internal static readonly DiagnosticDescriptor Descriptor = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      CreateLocalizableResourceString(nameof(UseAsyncMethodInAsyncContextMessage)),
                                                                                      DiagnosticCategory.Performance,
                                                                                      RuleLevel.Disabled,
                                                                                      s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);

        internal static readonly DiagnosticDescriptor DescriptorNoAlternativeMethod = DiagnosticDescriptorHelper.Create(RuleId,
                                                                              s_localizableTitle,
                                                                              CreateLocalizableResourceString(nameof(UseAsyncMethodInAsyncContextMessage_NoAlternative)),
                                                                              DiagnosticCategory.Performance,
                                                                              RuleLevel.Disabled,
                                                                              s_localizableDescription,
                                                                              isPortedFxCopRule: false,
                                                                              isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics { get; } = ImmutableArray.Create(Descriptor, DescriptorNoAlternativeMethod);

        public override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterCompilationStartAction(context =>
            {
                ConcurrentDictionary<string, INamedTypeSymbol> syncBlockingTypes = new();
                GetTypeAndAddToDictionary("Task", WellKnownTypeNames.SystemThreadingTasksTask, syncBlockingTypes, context.Compilation);
                GetTypeAndAddToDictionary("TaskGeneric", WellKnownTypeNames.SystemThreadingTasksTask1, syncBlockingTypes, context.Compilation);
                GetTypeAndAddToDictionary("ValueTask", WellKnownTypeNames.SystemThreadingTasksValueTask, syncBlockingTypes, context.Compilation);
                GetTypeAndAddToDictionary("IAsyncEnumerableGeneric", WellKnownTypeNames.SystemCollectionsGenericIAsyncEnumerable1, syncBlockingTypes, context.Compilation);
                GetTypeAndAddToDictionary("AsyncMethodBuilderAttribute", WellKnownTypeNames.SystemRuntimeCompilerServicesAsyncMethodBuilderAttribute, syncBlockingTypes, context.Compilation);

                List<SyncBlockingSymbol> syncBlockingSymbols = new();
                GetSymbolAndAddToList("Wait", WellKnownTypeNames.SystemThreadingTasksTask, SymbolKind.Method, syncBlockingSymbols, context.Compilation);
                GetSymbolAndAddToList("WaitAll", WellKnownTypeNames.SystemThreadingTasksTask, SymbolKind.Method, syncBlockingSymbols, context.Compilation);
                GetSymbolAndAddToList("WaitAny", WellKnownTypeNames.SystemThreadingTasksTask, SymbolKind.Method, syncBlockingSymbols, context.Compilation);
                GetSymbolAndAddToList("Result", WellKnownTypeNames.SystemThreadingTasksTask1, SymbolKind.Property, syncBlockingSymbols, context.Compilation);
                GetSymbolAndAddToList("Result", WellKnownTypeNames.SystemThreadingTasksValueTask, SymbolKind.Property, syncBlockingSymbols, context.Compilation);
                GetSymbolAndAddToList("GetResult", WellKnownTypeNames.SystemRuntimeCompilerServicesTaskAwaiter, SymbolKind.Method, syncBlockingSymbols, context.Compilation);
                GetSymbolAndAddToList("GetResult", WellKnownTypeNames.SystemRuntimeCompilerServicesValueTaskAwaiter, SymbolKind.Method, syncBlockingSymbols, context.Compilation);

                if (!syncBlockingTypes.Any())
                {
                    return;
                }

                if (!context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemObsoleteAttribute, out INamedTypeSymbol? systemObsoleteAttribute))
                {
                    return;
                }

                context.RegisterOperationAction(context =>
                {
                    if (IsInTaskReturningMethodOrDelegate(context, syncBlockingTypes))
                    {
                        if (context.Operation is IInvocationOperation invocationOperation)
                        {
                            if (InspectAndReportBlockingMemberAccess(context, syncBlockingSymbols, SymbolKind.Method))
                            {
                                // Don't return double-diagnostics.
                                return;
                            }

                            // Also consider all method calls to check for Async-suffixed alternatives.
                            var semanticModel = context.Operation.SemanticModel;
                            SymbolInfo symbolInfo = semanticModel.GetSymbolInfo(context.Operation.Syntax, context.CancellationToken);

                            if (symbolInfo.Symbol is IMethodSymbol methodSymbol &&
                                !methodSymbol.Name.EndsWith(MandatoryAsyncSuffix, StringComparison.Ordinal) &&
                                !HasAsyncCompatibleReturnType(methodSymbol, syncBlockingTypes))
                            {
                                IEnumerable<IMethodSymbol> methodSymbols = semanticModel.LookupSymbols(
                                    context.Operation.Syntax.GetLocation().SourceSpan.Start,
                                    methodSymbol.ContainingType,
                                    methodSymbol.Name + MandatoryAsyncSuffix,
                                    includeReducedExtensionMethods: true)
                                    .OfType<IMethodSymbol>();

                                string containingMethodName = "";
                                if (context.ContainingSymbol is IMethodSymbol parentMethod)
                                {
                                    containingMethodName = parentMethod.Name;
                                }

                                foreach (IMethodSymbol method in methodSymbols)
                                {
                                    if (!method.HasAttribute(systemObsoleteAttribute)
                                        && HasSupersetOfParameterTypes(method, methodSymbol)
                                        && method.Name != containingMethodName
                                        && HasAsyncCompatibleReturnType(method, syncBlockingTypes))
                                    {
                                        Diagnostic diagnostic = invocationOperation.CreateDiagnostic(
                                            Descriptor,
                                            invocationOperation.TargetMethod.ToDisplayString(GetLanguageSpecificFormat(invocationOperation)),
                                            method.ToDisplayString(GetLanguageSpecificFormat(invocationOperation)));

                                        context.ReportDiagnostic(diagnostic);

                                        return;
                                    }
                                }
                            }
                        }
                        else
                        {
                            InspectAndReportBlockingMemberAccess(context, syncBlockingSymbols, SymbolKind.Property);
                        }
                    }
                }, OperationKind.Invocation, OperationKind.PropertyReference);
            });
        }

        private static SymbolDisplayFormat GetLanguageSpecificFormat(IOperation operation) =>
                operation.Language == LanguageNames.CSharp ? SymbolDisplayFormat.CSharpShortErrorMessageFormat : SymbolDisplayFormat.VisualBasicShortErrorMessageFormat;

        internal class SyncBlockingSymbol
        {
            public SyncBlockingSymbol(string Name, string Namespace, SymbolKind Kind, ISymbol Value)
            {
                this.Name = Name;
                this.Namespace = Namespace;
                this.Kind = Kind;
                this.Value = Value;
            }

            public string Name { get; set; }
            public string Namespace { get; set; }
            public SymbolKind Kind { get; set; }
            public ISymbol Value { get; set; }
        }

        private static void GetTypeAndAddToDictionary(string key, string typeName, ConcurrentDictionary<string, INamedTypeSymbol> syncBlockingTypes, Compilation compilation)
        {
            if (compilation.TryGetOrCreateTypeByMetadataName(typeName, out INamedTypeSymbol? typeValue))
            {
                syncBlockingTypes.AddOrUpdate(key, typeValue, (k, v) => v);
            }
        }

        private static void GetSymbolAndAddToList(string symbolName, string Namespace, SymbolKind kind, List<SyncBlockingSymbol> syncBlockingSymbols, Compilation compilation)
        {
            if (compilation.TryGetOrCreateTypeByMetadataName(Namespace, out INamedTypeSymbol? typeValue))
            {
                ISymbol? symbolValue = typeValue
                    .GetMembers(symbolName)
                    .FirstOrDefault(s => s.Kind == kind);

                if (symbolValue is not null)
                {
                    syncBlockingSymbols.Add(new SyncBlockingSymbol(symbolName, Namespace, kind, symbolValue));
                }
            }
        }

        /// <summary>
        /// Determines whether the given method has parameters to cover all the parameter types in another method.
        /// </summary>
        /// <param name="candidateMethod">The candidate method.</param>
        /// <param name="baselineMethod">The baseline method.</param>
        /// <returns>
        ///   <c>true</c> if <paramref name="candidateMethod"/> has a superset of parameter types found in <paramref name="baselineMethod"/>; otherwise <c>false</c>.
        /// </returns>
        private static bool HasSupersetOfParameterTypes(IMethodSymbol candidateMethod, IMethodSymbol baselineMethod)
        {
            return candidateMethod.Parameters.All(candidateParameter => baselineMethod.Parameters.Any(baselineParameter => baselineParameter.Type?.Equals(candidateParameter.Type) ?? false));
        }

        private static bool HasAsyncCompatibleReturnType(IMethodSymbol methodSymbol, ConcurrentDictionary<string, INamedTypeSymbol> syncBlockingTypes)
        {
            if (methodSymbol.ReturnType is null)
            {
                return false;
            }

            ISymbol returnType = methodSymbol.ReturnType;

            static bool CheckReturnTypeMatch(string targetType, ISymbol returnType, ConcurrentDictionary<string, INamedTypeSymbol> syncBlockingTypes)
                => syncBlockingTypes.TryGetValue(targetType, out INamedTypeSymbol? targetTypeValue)
                && targetTypeValue.Equals(returnType.OriginalDefinition);

            return CheckReturnTypeMatch("Task", returnType, syncBlockingTypes)
                || CheckReturnTypeMatch("TaskGeneric", returnType, syncBlockingTypes)
                || CheckReturnTypeMatch("ValueTask", returnType, syncBlockingTypes)
                || CheckReturnTypeMatch("IAsyncEnumerableGeneric", returnType, syncBlockingTypes)
                || (syncBlockingTypes.TryGetValue("AsyncMethodBuilderAttribute", out INamedTypeSymbol? asyncMethodBuilderAttributeTypeValue)
                && returnType.HasAttribute(asyncMethodBuilderAttributeTypeValue));
        }

        private static IMethodSymbol? GetParentMethodOrDelegate(OperationAnalysisContext context)
        {
            var containingAnonymousFunction = context.Operation.TryGetContainingAnonymousFunctionOrLocalFunction();
            if (containingAnonymousFunction is not null)
            {
                return containingAnonymousFunction;
            }

            var containingSymbol = context.ContainingSymbol;
            while (containingSymbol is not null)
            {
                if (containingSymbol is IMethodSymbol method)
                {
                    return method;
                }
                containingSymbol = containingSymbol.ContainingSymbol;
            }

            return null;
        }

        private static bool IsInTaskReturningMethodOrDelegate(OperationAnalysisContext context, ConcurrentDictionary<string, INamedTypeSymbol> syncBlockingTypes)
        {
            // We want to scan invocations that occur inside Task and Task<T>-returning delegates or methods.
            // That is: methods that either are or could be made async.
            IMethodSymbol? parentMethod = GetParentMethodOrDelegate(context);

            if (parentMethod == null)
            {
                return false;
            }

            return HasAsyncCompatibleReturnType(parentMethod, syncBlockingTypes);
        }

        private static bool InspectAndReportBlockingMemberAccess(OperationAnalysisContext context, List<SyncBlockingSymbol> syncBlockingSymbols, SymbolKind kind)
        {
            ISymbol? memberSymbol = context.Operation.SemanticModel.GetSymbolInfo(context.Operation.Syntax, context.CancellationToken).Symbol;
            if (memberSymbol is null)
            {
                return false;
            }

            foreach (SyncBlockingSymbol symbol in syncBlockingSymbols)
            {
                if (symbol.Kind != kind) continue;
                if (symbol.Value.Equals(memberSymbol.OriginalDefinition))
                {
                    Diagnostic diagnostic = context.Operation.Syntax.CreateDiagnostic(
                        DescriptorNoAlternativeMethod,
                        symbol.Value.ToDisplayString(GetLanguageSpecificFormat(context.Operation))
                    );

                    context.ReportDiagnostic(diagnostic);
                    return true;
                }
            }
            return false;
        }
    }
}
