// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class DoNotCallDangerousMethodsInDeserialization : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5360";
        private static readonly LocalizableString s_Title = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotCallDangerousMethodsInDeserialization),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Message = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotCallDangerousMethodsInDeserializationMessage),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Description = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.DoNotCallDangerousMethodsInDeserializationDescription),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        private ImmutableArray<(string, string[])> DangerousCallable = ImmutableArray.Create<(string, string[])>
            (
                (WellKnownTypeNames.SystemIOFile, new[] { "WriteAllBytes", "WriteAllLines", "WriteAllText", "Copy", "Move", "AppendAllLines", "AppendAllText", "AppendText", "Delete" }),
                (WellKnownTypeNames.SystemIODirectory, new[] { "Delete" }),
                (WellKnownTypeNames.SystemIOFileInfo, new[] { "Delete" }),
                (WellKnownTypeNames.SystemIODirectoryInfo, new[] { "Delete" }),
                (WellKnownTypeNames.SystemIOLogLogStore, new[] { "Delete" }),
                (WellKnownTypeNames.SystemReflectionAssembly, new[] { "GetLoadedModules", "Load", "LoadFile", "LoadFrom", "LoadModule", "LoadWithPartialName", "ReflectionOnlyLoad", "ReflectionOnlyLoadFrom", "UnsafeLoadFrom" })
            );

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
                DiagnosticId,
                s_Title,
                s_Message,
                DiagnosticCategory.Security,
                RuleLevel.IdeHidden_BulkConfigurable,
                description: s_Description,
                isPortedFxCopRule: false,
                isDataflowRule: false,
                isReportedAtCompilationEnd: true);

        public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

        public sealed override void Initialize(AnalysisContext context)
        {
            context.EnableConcurrentExecution();

            // Security analyzer - analyze and report diagnostics on generated code.
            context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.ReportDiagnostics);

            context.RegisterCompilationStartAction(
                (CompilationStartAnalysisContext compilationStartAnalysisContext) =>
                {
                    var compilation = compilationStartAnalysisContext.Compilation;
                    var wellKnownTypeProvider = WellKnownTypeProvider.GetOrCreate(compilation);

                    if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemSerializableAttribute,
                        out INamedTypeSymbol? serializableAttributeTypeSymbol))
                    {
                        return;
                    }

                    var dangerousMethodSymbolsBuilder = ImmutableHashSet.CreateBuilder<IMethodSymbol>();

                    foreach (var (typeName, methodNames) in DangerousCallable)
                    {
                        if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                            typeName,
                            out INamedTypeSymbol? typeSymbol))
                        {
                            continue;
                        }

                        foreach (var methodName in methodNames)
                        {
                            dangerousMethodSymbolsBuilder.UnionWith(
                                typeSymbol.GetMembers()
                                    .OfType<IMethodSymbol>()
                                    .Where(
                                        s => s.Name == methodName));
                        }
                    }

                    if (!dangerousMethodSymbolsBuilder.Any())
                    {
                        return;
                    }

                    var dangerousMethodSymbols = dangerousMethodSymbolsBuilder.ToImmutableHashSet();
                    var attributeTypeSymbolsBuilder = ImmutableArray.CreateBuilder<INamedTypeSymbol>();

                    if (wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemRuntimeSerializationOnDeserializingAttribute,
                        out INamedTypeSymbol? onDeserializingAttributeTypeSymbol))
                    {
                        attributeTypeSymbolsBuilder.Add(onDeserializingAttributeTypeSymbol);
                    }

                    if (wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(
                        WellKnownTypeNames.SystemRuntimeSerializationOnDeserializedAttribute,
                        out INamedTypeSymbol? onDeserializedAttributeTypeSymbol))
                    {
                        attributeTypeSymbolsBuilder.Add(onDeserializedAttributeTypeSymbol);
                    }

                    var attributeTypeSymbols = attributeTypeSymbolsBuilder.ToImmutable();

                    if (!wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationStreamingContext, out INamedTypeSymbol? streamingContextTypeSymbol) ||
                        !wellKnownTypeProvider.TryGetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemRuntimeSerializationIDeserializationCallback, out INamedTypeSymbol? IDeserializationCallbackTypeSymbol))
                    {
                        return;
                    }

                    // A dictionary from method symbol to set of methods invoked by it directly.
                    // The bool value in the sub ConcurrentDictionary is not used, use ConcurrentDictionary rather than HashSet just for the concurrency security.
                    var callGraph = new ConcurrentDictionary<ISymbol, ConcurrentDictionary<ISymbol, bool>>();

                    compilationStartAnalysisContext.RegisterOperationBlockStartAction(
                        (OperationBlockStartAnalysisContext operationBlockStartAnalysisContext) =>
                        {
                            var owningSymbol = operationBlockStartAnalysisContext.OwningSymbol;
                            ConcurrentDictionary<ISymbol, bool> calledMethods;

                            if (owningSymbol is IMethodSymbol methodSymbol ||
                                (owningSymbol is IFieldSymbol fieldSymbol &&
                                fieldSymbol.Type.TypeKind == TypeKind.Delegate))
                            {
                                // Delegate member could be added already, so use GetOrAdd().
                                calledMethods = callGraph.GetOrAdd(owningSymbol, new ConcurrentDictionary<ISymbol, bool>());
                            }
                            else
                            {
                                return;
                            }

                            operationBlockStartAnalysisContext.RegisterOperationAction(operationContext =>
                            {
                                ISymbol? calledSymbol = null;
                                ITypeSymbol? possibleDelegateSymbol = null;

                                switch (operationContext.Operation)
                                {
                                    case IInvocationOperation invocationOperation:
                                        calledSymbol = invocationOperation.TargetMethod.OriginalDefinition;
                                        possibleDelegateSymbol = calledSymbol.ContainingType; // Invoke().

                                        break;

                                    case IFieldReferenceOperation fieldReferenceOperation:
                                        var fieldSymbol = fieldReferenceOperation.Field;
                                        possibleDelegateSymbol = fieldSymbol.Type; // Delegate field.

                                        if (possibleDelegateSymbol.TypeKind != TypeKind.Delegate)
                                        {
                                            return;
                                        }
                                        else
                                        {
                                            calledSymbol = fieldSymbol;
                                        }

                                        break;

                                    default:
                                        throw new NotImplementedException();
                                }

                                calledMethods.TryAdd(calledSymbol, true);

                                // calledSymbol.ContainingSymbol.Kind == SymbolKind.Method => local function
                                // For the purposes of this rule, we'll treat invocations inside the local function as part of
                                // the containing method's set of invocations.
                                if (!calledSymbol.IsInSource() ||
                                    calledSymbol.ContainingType.TypeKind == TypeKind.Interface ||
                                    calledSymbol.ContainingSymbol.Kind == SymbolKind.Method ||
                                    calledSymbol.IsAbstract ||
                                    possibleDelegateSymbol.TypeKind == TypeKind.Delegate)
                                {
                                    callGraph.TryAdd(calledSymbol, new ConcurrentDictionary<ISymbol, bool>());
                                }
                            }, OperationKind.Invocation, OperationKind.FieldReference);
                        });

                    compilationStartAnalysisContext.RegisterCompilationEndAction(
                        (CompilationAnalysisContext compilationAnalysisContext) =>
                        {
                            var visited = new HashSet<ISymbol>();
                            var results = new Dictionary<ISymbol, HashSet<(ISymbol DangerousMethod, ArrayBuilder<ISymbol> IntermediateMethods)>>();
                            var symbolDisplayStringCache = SymbolDisplayStringCache.GetOrCreate(
                                compilation,
                                SymbolDisplayFormat.MinimallyQualifiedFormat);
                            var methodSymbolArray = new ISymbol[1];    // So we can call .Concat() without allocating new arrays.

                            try
                            {
                                foreach (var methodSymbol in callGraph.Keys.OfType<IMethodSymbol>())
                                {
                                    // Determine if the method is called automatically when an object is deserialized.
                                    // This includes methods with OnDeserializing attribute, method with OnDeserialized attribute, deserialization callbacks as well as cleanup/dispose calls.
                                    var flagSerializable = methodSymbol.ContainingType.HasAttribute(serializableAttributeTypeSymbol);
                                    var parameters = methodSymbol.GetParameters();
                                    var flagHasDeserializeAttributes = !attributeTypeSymbols.IsEmpty
                                        && attributeTypeSymbols.Any(s => methodSymbol.HasAttribute(s))
                                        && parameters.Length == 1
                                        && parameters[0].Type.Equals(streamingContextTypeSymbol);
                                    var flagImplementOnDeserializationMethod = methodSymbol.IsOnDeserializationImplementation(IDeserializationCallbackTypeSymbol);
                                    var flagImplementDisposeMethod = methodSymbol.IsDisposeImplementation(compilation);
                                    var flagIsFinalizer = methodSymbol.IsFinalizer();

                                    if (!flagSerializable || !flagHasDeserializeAttributes && !flagImplementOnDeserializationMethod && !flagImplementDisposeMethod && !flagIsFinalizer)
                                    {
                                        continue;
                                    }

                                    FindCalledDangerousMethod(methodSymbol, visited, results);

                                    foreach (var (DangerousMethod, IntermediateMethods) in results[methodSymbol])
                                    {
                                        methodSymbolArray[0] = methodSymbol;
                                        compilationAnalysisContext.ReportDiagnostic(
                                            methodSymbol.CreateDiagnostic(
                                                Rule,
                                                methodSymbol.ContainingType.Name,
                                                methodSymbol.MetadataName,
                                                DangerousMethod.MetadataName,
                                                string.Join(
                                                    " -> ",
                                                    methodSymbolArray
                                                        .Concat(IntermediateMethods)
                                                        .Concat(DangerousMethod)
                                                        .Select(
                                                            s => symbolDisplayStringCache.GetDisplayString(s)))));
                                    }
                                }
                            }
                            finally
                            {
                                foreach (var entry in results)
                                {
                                    if (entry.Value == null)
                                    {
                                        continue;
                                    }

                                    foreach (var (DangerousMethod, IntermediateMethods) in entry.Value)
                                    {
                                        IntermediateMethods?.Dispose();
                                    }
                                }
                            }
                        });

                    // <summary>
                    // Analyze the method to find all the dangerous method it calls.
                    // </summary>
                    // <param name="methodSymbol">The symbol of the method to be analyzed</param>
                    // <param name="visited">All the method has been analyzed</param>
                    // <param name="results">The result is organized by &lt;method to be analyzed, dangerous method it calls
                    // and the intermediate methods to reach the dangerous method&gt;</param>
                    void FindCalledDangerousMethod(
                        ISymbol methodSymbol,
                        HashSet<ISymbol> visited,
                        Dictionary<ISymbol, HashSet<(ISymbol, ArrayBuilder<ISymbol>)>> results)
                    {
                        if (visited.Add(methodSymbol))
                        {
                            results.Add(methodSymbol, new HashSet<(ISymbol, ArrayBuilder<ISymbol>)>());

                            if (!callGraph.TryGetValue(methodSymbol, out var calledMethods))
                            {
                                Debug.Fail(methodSymbol.Name + " was not found in callGraph");

                                return;
                            }

                            foreach (var child in calledMethods.Keys)
                            {
                                if (dangerousMethodSymbols.Contains(child))
                                {
                                    results[methodSymbol].Add((child, ArrayBuilder<ISymbol>.GetInstance()));
                                }

                                if (Equals(child, methodSymbol))
                                {
                                    continue;
                                }

                                FindCalledDangerousMethod(child, visited, results);

                                if (results.TryGetValue(child, out var result))
                                {
                                    // If we find results in the calling method
                                    foreach ((ISymbol dangerousMethod, ArrayBuilder<ISymbol> intermediateCalls) in result)
                                    {
                                        var newIntermediateCalls = ArrayBuilder<ISymbol>.GetInstance();
                                        newIntermediateCalls.Add(child);
                                        newIntermediateCalls.AddRange(intermediateCalls);
                                        results[methodSymbol].Add((dangerousMethod, newIntermediateCalls));
                                    }
                                }
                                else
                                {
                                    Debug.Fail(child.Name + " was not found in results");
                                }
                            }
                        }
                    }
                });
        }
    }
}
