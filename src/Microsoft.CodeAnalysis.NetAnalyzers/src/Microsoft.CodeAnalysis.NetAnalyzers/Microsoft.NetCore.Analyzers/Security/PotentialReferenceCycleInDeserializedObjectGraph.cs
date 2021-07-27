// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities;
using Analyzer.Utilities.Extensions;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;

namespace Microsoft.NetCore.Analyzers.Security
{
    [DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
    public sealed class PotentialReferenceCycleInDeserializedObjectGraph : DiagnosticAnalyzer
    {
        internal const string DiagnosticId = "CA5362";
        private static readonly LocalizableString s_Title = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.PotentialReferenceCycleInDeserializedObjectGraphTitle),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Message = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.PotentialReferenceCycleInDeserializedObjectGraphMessage),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));
        private static readonly LocalizableString s_Description = new LocalizableResourceString(
            nameof(MicrosoftNetCoreAnalyzersResources.PotentialReferenceCycleInDeserializedObjectGraphDescription),
            MicrosoftNetCoreAnalyzersResources.ResourceManager,
            typeof(MicrosoftNetCoreAnalyzersResources));

        internal static DiagnosticDescriptor Rule = DiagnosticDescriptorHelper.Create(
                DiagnosticId,
                s_Title,
                s_Message,
                DiagnosticCategory.Security,
                RuleLevel.Disabled,
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
                    var serializableAttributeTypeSymbol = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemSerializableAttribute);

                    if (serializableAttributeTypeSymbol == null)
                    {
                        return;
                    }

                    var nonSerializedAttribute = compilation.GetOrCreateTypeByMetadataName(WellKnownTypeNames.SystemNonSerializedAttribute);

                    if (nonSerializedAttribute == null)
                    {
                        return;
                    }

                    ConcurrentDictionary<ISymbol, ConcurrentDictionary<ISymbol, bool>> forwardGraph = new ConcurrentDictionary<ISymbol, ConcurrentDictionary<ISymbol, bool>>();
                    ConcurrentDictionary<ISymbol, ConcurrentDictionary<ISymbol, bool>> invertedGraph = new ConcurrentDictionary<ISymbol, ConcurrentDictionary<ISymbol, bool>>();

                    // It keeps the out Degree of every vertex in the invertedGraph, which is corresponding to the in Degree of the vertex in forwardGraph.
                    ConcurrentDictionary<ISymbol, int> inDegree = new ConcurrentDictionary<ISymbol, int>();

                    // It Keeps the out degree of every vertex in the forwardGraph, which is corresponding to the in Degree of the vertex in invertedGraph.
                    ConcurrentDictionary<ISymbol, int> outDegree = new ConcurrentDictionary<ISymbol, int>();

                    compilationStartAnalysisContext.RegisterSymbolAction(
                        (SymbolAnalysisContext symbolAnalysisContext) =>
                        {
                            DrawGraph((INamedTypeSymbol)symbolAnalysisContext.Symbol);
                        }, SymbolKind.NamedType);

                    compilationStartAnalysisContext.RegisterCompilationEndAction(
                        (CompilationAnalysisContext compilationAnalysisContext) =>
                        {
                            ModifyDegree(inDegree, forwardGraph);
                            ModifyDegree(outDegree, invertedGraph);

                            // If the degree of a vertex is greater than 0 both in the forward graph and inverted graph after topological sorting,
                            // the vertex must belong to a loop.
                            var leftVertices = inDegree.Where(s => s.Value > 0).Select(s => s.Key).ToImmutableHashSet();
                            var invertedLeftVertices = outDegree.Where(s => s.Value > 0).Select(s => s.Key).ToImmutableHashSet();
                            var verticesInLoop = leftVertices.Intersect(invertedLeftVertices);

                            foreach (var vertex in verticesInLoop)
                            {
                                if (vertex is IFieldSymbol fieldInLoop)
                                {
                                    var associatedSymbol = fieldInLoop.AssociatedSymbol;
                                    compilationAnalysisContext.ReportDiagnostic(
                                        fieldInLoop.CreateDiagnostic(
                                            Rule,
                                            associatedSymbol == null ? vertex.Name : associatedSymbol.Name));
                                }
                            }
                        });

                    // Traverse from point to its descendants, save the information into a directed graph.
                    //
                    // point: The initial point
                    void DrawGraph(ITypeSymbol point)
                    {
                        // If the point has been visited, return;
                        // otherwise, add it to the graph and mark it as visited.
                        if (!AddPointToBothGraphs(point))
                        {
                            return;
                        }

                        foreach (var associatedTypePoint in GetAssociatedTypes(point))
                        {
                            if (associatedTypePoint == null ||
                                associatedTypePoint.Equals(point))
                            {
                                continue;
                            }

                            AddLineToBothGraphs(point, associatedTypePoint);
                            DrawGraph(associatedTypePoint);
                        }

                        if (point.IsInSource() &&
                            point.HasAttribute(serializableAttributeTypeSymbol))
                        {
                            var fieldPoints = point.GetMembers().OfType<IFieldSymbol>().Where(s => !s.HasAttribute(nonSerializedAttribute) &&
                                                                                                        !s.IsStatic);

                            foreach (var fieldPoint in fieldPoints)
                            {
                                var fieldTypePoint = fieldPoint.Type;
                                AddLineToBothGraphs(point, fieldPoint);
                                AddLineToBothGraphs(fieldPoint, fieldTypePoint);
                                DrawGraph(fieldTypePoint);
                            }
                        }
                    }

                    static HashSet<ITypeSymbol> GetAssociatedTypes(ITypeSymbol type)
                    {
                        var result = new HashSet<ITypeSymbol>();

                        if (type is INamedTypeSymbol namedTypeSymbol)
                        {
                            // 1. Type arguments of generic type.
                            if (namedTypeSymbol.IsGenericType)
                            {
                                foreach (var arg in namedTypeSymbol.TypeArguments)
                                {
                                    result.Add(arg);
                                }
                            }

                            // 2. The type it constructed from.
                            var constructedFrom = namedTypeSymbol.ConstructedFrom;
                            result.Add(constructedFrom);
                        }
                        else if (type is IArrayTypeSymbol arrayTypeSymbol)
                        {
                            // 3. Element type of the array.
                            result.Add(arrayTypeSymbol.ElementType);
                        }

                        // 4. Base type.
                        result.Add(type.BaseType);

                        return result;
                    }

                    // Add a line to the graph.
                    //
                    // from: The start point of the line
                    // to: The end point of the line
                    // degree: The out degree of all vertices in the graph
                    // graph: The graph
                    void AddLine(ISymbol from, ISymbol to, ConcurrentDictionary<ISymbol, int> degree, ConcurrentDictionary<ISymbol, ConcurrentDictionary<ISymbol, bool>> graph)
                    {
                        graph.AddOrUpdate(from, new ConcurrentDictionary<ISymbol, bool> { [to] = true }, (k, v) => { v[to] = true; return v; });
                        degree.AddOrUpdate(from, 1, (k, v) => v + 1);
                    }

                    // Add a point to the graph.
                    //
                    // point: The point to be added
                    // degree: The out degree of all vertices in the graph
                    // graph: The graph
                    static bool AddPoint(ISymbol point, ConcurrentDictionary<ISymbol, int> degree, ConcurrentDictionary<ISymbol, ConcurrentDictionary<ISymbol, bool>> graph)
                    {
                        degree.TryAdd(point, 0);
                        return graph.TryAdd(point, new ConcurrentDictionary<ISymbol, bool>());
                    }

                    // Add a line to the forward graph and inverted graph unconditionally.
                    //
                    // from: The start point of the line
                    // to: The end point of the line
                    void AddLineToBothGraphs(ISymbol from, ISymbol to)
                    {
                        AddLine(from, to, outDegree, forwardGraph);
                        AddLine(to, from, inDegree, invertedGraph);
                    }

                    // Add a point to the forward graph and inverted graph unconditionally.
                    //
                    // point: The point to be added
                    // return: `true` if `point` is added to the forward graph successfully; otherwise `false`.
                    bool AddPointToBothGraphs(ISymbol point)
                    {
                        AddPoint(point, inDegree, invertedGraph);
                        return AddPoint(point, outDegree, forwardGraph);
                    }

                    // According to topological sorting, modify the degree of every vertex in the graph.
                    //
                    // degree: The in degree of all vertices in the graph
                    // graph: The graph
                    static void ModifyDegree(ConcurrentDictionary<ISymbol, int> degree, ConcurrentDictionary<ISymbol, ConcurrentDictionary<ISymbol, bool>> graph)
                    {
                        var stack = new Stack<ISymbol>(degree.Where(s => s.Value == 0).Select(s => s.Key));

                        while (stack.Count != 0)
                        {
                            var start = stack.Pop();
                            degree.AddOrUpdate(start, -1, (k, v) => v - 1);

                            foreach (var vertex in graph[start].Keys)
                            {
                                degree.AddOrUpdate(vertex, -1, (k, v) => v - 1);

                                if (degree[vertex] == 0)
                                {
                                    stack.Push(vertex);
                                }
                            }
                        }
                    }
                });
        }
    }
}
