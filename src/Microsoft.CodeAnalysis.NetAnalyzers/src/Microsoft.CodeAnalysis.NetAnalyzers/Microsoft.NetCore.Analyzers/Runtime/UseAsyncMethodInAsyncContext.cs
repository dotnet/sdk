// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Runtime
{
    /// <summary>
    /// This analyzer suggests using the async version of a method when inside a Task-returning method
    /// In addition, calling Task.Wait(), Task.Result or Task.GetAwaiter().GetResult() will produce a diagnostic
    /// </summary>
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class UseAsyncMethodInAsyncContext : DiagnosticAnalyzer
    {
        internal const string RuleId = "CA1849";
        internal const string AsyncMethodKeyName = "AsyncMethodName";
        internal const string MandatoryAsyncSuffix = "Async";

        private static readonly LocalizableString s_localizableTitle = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseAsyncMethodInAsyncContextTitle), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessage = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseAsyncMethodInAsyncContextMessage), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableDescription = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseAsyncMethodInAsyncContextDescription), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_localizableMessageNoAlternative = new LocalizableResourceString(nameof(MicrosoftNetCoreAnalyzersResources.UseAsyncMethodInAsyncContextMessage_NoAlternative), MicrosoftNetCoreAnalyzersResources.ResourceManager, typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Descriptor = DiagnosticDescriptorHelper.Create(RuleId,
                                                                                      s_localizableTitle,
                                                                                      s_localizableMessage,
                                                                                      DiagnosticCategory.Performance,
                                                                                      RuleLevel.IdeSuggestion,
                                                                                      s_localizableDescription,
                                                                                      isPortedFxCopRule: false,
                                                                                      isDataflowRule: false);

        internal static DiagnosticDescriptor DescriptorNoAlternativeMethod = DiagnosticDescriptorHelper.Create(RuleId,
                                                                              s_localizableTitle,
                                                                              s_localizableMessageNoAlternative,
                                                                              DiagnosticCategory.Performance,
                                                                              RuleLevel.IdeSuggestion,
                                                                              s_localizableDescription,
                                                                              isPortedFxCopRule: false,
                                                                              isDataflowRule: false);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Descriptor, DescriptorNoAlternativeMethod);

        public override void Initialize(AnalysisContext context)
        {

            context.EnableConcurrentExecution();
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
            context.RegisterOperationBlockStartAction(context =>
            {
                ConcurrentDictionary<string, string> qualifyingTypes = new();
                qualifyingTypes.AddOrUpdate("Task", WellKnownTypeNames.SystemThreadingTasksTask, (k, v) => v);
                qualifyingTypes.AddOrUpdate("TaskGeneric", WellKnownTypeNames.SystemThreadingTasksTask1, (k, v) => v);
                qualifyingTypes.AddOrUpdate("ValueTask", WellKnownTypeNames.SystemThreadingTasksValueTask, (k, v) => v);
                qualifyingTypes.AddOrUpdate("TaskAwaiter", WellKnownTypeNames.SystemRuntimeCompilerServicesTaskAwaiter, (k, v) => v);
                qualifyingTypes.AddOrUpdate("ValueTaskAwaiter", WellKnownTypeNames.SystemRuntimeCompilerServicesValueTaskAwaiter, (k, v) => v);
                qualifyingTypes.AddOrUpdate("IAsyncEnumberableGeneric", WellKnownTypeNames.SystemCollectionsGenericIAsyncEnumerable1, (k, v) => v);
                qualifyingTypes.AddOrUpdate("AsyncMethodBuilderAttribute", WellKnownTypeNames.SystemRuntimeCompilerServicesAsyncMethodBuilderAttribute, (k, v) => v);

                ConcurrentDictionary<string, ISymbol> syncBlockingTypes = GetSyncBlockingTypes(context.Compilation, qualifyingTypes);

                List<SyncBlockingSymbol> syncBlockingSymbols = new();
                syncBlockingSymbols.Add(new SyncBlockingSymbol("Wait", WellKnownTypeNames.SystemThreadingTasksTask, SymbolKind.Method));
                syncBlockingSymbols.Add(new SyncBlockingSymbol("WaitAll", WellKnownTypeNames.SystemThreadingTasksTask, SymbolKind.Method));
                syncBlockingSymbols.Add(new SyncBlockingSymbol("WaitAny", WellKnownTypeNames.SystemThreadingTasksTask, SymbolKind.Method));
                syncBlockingSymbols.Add(new SyncBlockingSymbol("Result", WellKnownTypeNames.SystemThreadingTasksTask1, SymbolKind.Property));
                syncBlockingSymbols.Add(new SyncBlockingSymbol("Result", WellKnownTypeNames.SystemThreadingTasksValueTask, SymbolKind.Property));
                syncBlockingSymbols.Add(new SyncBlockingSymbol("GetResult", WellKnownTypeNames.SystemRuntimeCompilerServicesTaskAwaiter, SymbolKind.Method));

                GetCompilationSymbols(context.Compilation, syncBlockingSymbols);

                context.Compilation.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemObsoleteAttribute, out INamedTypeSymbol? systemObsoleteAttribute);

                if (!syncBlockingTypes.Any())
                {
                    return;
                }

                context.RegisterOperationAction(context =>
                {
                    if (context.Operation is IInvocationOperation invocationOperation && IsInTaskReturningMethodOrDelegate(context, syncBlockingTypes))
                    {
                        if (InspectMemberAccess(context, syncBlockingSymbols, SymbolKind.Method))
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
                            string asyncMethodName = methodSymbol.Name + MandatoryAsyncSuffix;
                            IEnumerable<IMethodSymbol> methodSymbols = semanticModel.LookupSymbols(
                                context.Operation.Syntax.GetLocation().SourceSpan.Start,
                                methodSymbol.ContainingType,
                                asyncMethodName,
                                includeReducedExtensionMethods: true)
                                .OfType<IMethodSymbol>();

                            string containingMethodName = "";
                            if (context.ContainingSymbol is IMethodSymbol parentMethod)
                            {
                                containingMethodName = parentMethod.Name;
                            }

                            SyntaxNode invokedMethodName = context.Operation.Syntax;

                            foreach (IMethodSymbol method in methodSymbols)
                            {
                                if (!method.HasAttribute(systemObsoleteAttribute)
                                    && HasSupersetOfParameterTypes(method, methodSymbol)
                                    && method.Name != containingMethodName
                                    && HasAsyncCompatibleReturnType(method, syncBlockingTypes))
                                {
                                    // An async alternative exists.
                                    ImmutableDictionary<string, string?> properties = ImmutableDictionary<string, string?>.Empty
                                        .Add(AsyncMethodKeyName, asyncMethodName);

                                    Diagnostic diagnostic = invocationOperation.CreateDiagnostic(
                                        Descriptor,
                                        properties,
                                        invokedMethodName.ToString(),
                                        asyncMethodName);

                                    context.ReportDiagnostic(diagnostic);

                                    return;
                                }
                            }
                        }
                    }
                }, OperationKind.Invocation);

                context.RegisterOperationAction(context =>
                {
                    if (IsInTaskReturningMethodOrDelegate(context, syncBlockingTypes))
                    {
                        InspectMemberAccess(context, syncBlockingSymbols, SymbolKind.Property);
                    }
                }, OperationKind.PropertyReference);
            });
        }

        internal class SyncBlockingSymbol
        {
            public SyncBlockingSymbol(string Name, string Namespace, SymbolKind Kind)
            {
                this.Name = Name;
                this.Namespace = Namespace;
                this.Kind = Kind;
            }

            public string Name { get; set; }
            public string Namespace { get; set; }
            public SymbolKind Kind { get; set; }
            public ISymbol? Value { get; set; }
        }

        private static ConcurrentDictionary<string, ISymbol> GetSyncBlockingTypes(Compilation compilation, ConcurrentDictionary<string, string> types)
        {
            ConcurrentDictionary<string, ISymbol> compilationTypes = new();
            foreach (var item in types)
            {
                string typeNamespace = item.Value;
                if (compilation.TryGetOrCreateTypeByMetadataName(typeNamespace, out INamedTypeSymbol? typeValue))
                {
                    compilationTypes.AddOrUpdate(item.Key, typeValue, (k, v) => v);
                }
            }

            return compilationTypes;
        }

        private static void GetCompilationSymbols(Compilation compilation, List<SyncBlockingSymbol> symbols)
        {
            foreach (SyncBlockingSymbol symbol in symbols)
            {
                if (compilation.TryGetOrCreateTypeByMetadataName(symbol.Namespace, out INamedTypeSymbol? typeValue))
                {
                    ISymbol? symbolValue = typeValue
                       .GetMembers(symbol.Name)
                       .FirstOrDefault(p => p.Kind == symbol.Kind);

                    if (symbolValue is not null)
                    {
                        symbol.Value = symbolValue;
                    }
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

        private static bool HasAsyncCompatibleReturnType([NotNullWhen(true)] IMethodSymbol? methodSymbol, ConcurrentDictionary<string, ISymbol> syncBlockingTypes)
        {
            if (methodSymbol?.ReturnType is null)
            {
                return false;
            }

            ISymbol returnType = methodSymbol.ReturnType;

            static bool CheckReturnTypeMatch(ISymbol targetSymbol, ISymbol returnType)
                => targetSymbol.Equals(returnType.OriginalDefinition);

            bool isTask = (syncBlockingTypes.TryGetValue("Task", out ISymbol? taskTypeValue) && CheckReturnTypeMatch(taskTypeValue, returnType)) ||
                (syncBlockingTypes.TryGetValue("TaskGeneric", out ISymbol? taskTypeGenericValue) && CheckReturnTypeMatch(taskTypeGenericValue, returnType));

            bool isValueTask = syncBlockingTypes.TryGetValue("ValueTask", out ISymbol? valueTaskTypeValue) && CheckReturnTypeMatch(valueTaskTypeValue, returnType);

            bool isIAsyncEnumerable = syncBlockingTypes.TryGetValue("IAsyncEnumberableGeneric", out ISymbol? genericIAsyncEnumberableTypeValue) &&
                CheckReturnTypeMatch(genericIAsyncEnumberableTypeValue, returnType);

            bool isAsyncMethodBuilderAttribute = syncBlockingTypes.TryGetValue("AsyncMethodBuilderAttribute", out ISymbol? asyncMethodBuilderAttributeTypeValue) &&
                returnType.HasAttribute((INamedTypeSymbol)asyncMethodBuilderAttributeTypeValue);

            return isTask || isValueTask || isIAsyncEnumerable || isAsyncMethodBuilderAttribute;
        }

        private static IMethodSymbol? GetParentMethodOrDelegate(OperationAnalysisContext context)
        {
            var containingAnonymousFunction = context.Operation.TryGetContainingAnonymousFunctionOrLocalFunction();
            if (containingAnonymousFunction is not null)
            {
                return containingAnonymousFunction;
            }

            ISymbol containingSymbol = context.ContainingSymbol;
            while (containingSymbol is not null && containingSymbol is not IMethodSymbol)
            {
                containingSymbol = containingSymbol.ContainingSymbol;
            }

            IMethodSymbol? parentMethod = (IMethodSymbol?)containingSymbol;
            return parentMethod;
        }

        private static bool IsInTaskReturningMethodOrDelegate(OperationAnalysisContext context, ConcurrentDictionary<string, ISymbol> syncBlockingTypes)
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

        private static bool InspectMemberAccess(OperationAnalysisContext context, List<SyncBlockingSymbol> syncBlockingSymbols, SymbolKind kind)
        {
            ISymbol? memberSymbol = context.Operation.SemanticModel.GetSymbolInfo(context.Operation.Syntax, context.CancellationToken).Symbol;

            if (memberSymbol is object)
            {
                foreach (SyncBlockingSymbol symbol in syncBlockingSymbols)
                {
                    if (symbol.Value is null || symbol.Kind != kind) continue;
                    if (symbol.Value.Equals(memberSymbol.OriginalDefinition))
                    {
                        Location? location = context.Operation.Syntax.GetLocation();
                        ImmutableDictionary<string, string>? properties = ImmutableDictionary<string, string>.Empty;
                        DiagnosticDescriptor descriptor;
                        var messageArgs = new List<object>(2)
                            {
                                symbol.Value.Name
                            };

                        properties = properties.Add(AsyncMethodKeyName, string.Empty);
                        descriptor = DescriptorNoAlternativeMethod;

                        Diagnostic diagnostic = Diagnostic.Create(descriptor, location, properties, messageArgs.ToArray());
                        context.ReportDiagnostic(diagnostic);
                        return true;
                    }
                }
            }
            return false;
        }
    }
}
