// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;

namespace Microsoft.TemplateEngine.Utils
{
    public class DirectedGraph<T>
    {
        private static readonly DirectedGraph<T> Empty = new(new Dictionary<T, HashSet<T>>());

        private readonly Dictionary<T, HashSet<T>> _dependenciesMap;
        private readonly Lazy<Dictionary<T, HashSet<T>>> _dependentsMap;
        private readonly IReadOnlyList<T> _vertices;

        /// <summary>
        /// Initializes a new instance of the <see cref="DirectedGraph{T}"/> class.
        /// </summary>
        /// <param name="dependenciesMap">Lookup of child nodes for all nodes (or at lest nodes that have children).</param>
        public DirectedGraph(Dictionary<T, HashSet<T>> dependenciesMap)
        {
            _dependenciesMap = DeepCopy(dependenciesMap);
            _vertices = GetVertices(_dependenciesMap);
            _dependentsMap = new Lazy<Dictionary<T, HashSet<T>>>(() => GetDependentsMap(_dependenciesMap, _vertices));
        }

        internal IReadOnlyDictionary<T, HashSet<T>> DependenciesMap => _dependenciesMap;

        private bool IsEmpty => _dependenciesMap.Count == 0;

        public static implicit operator DirectedGraph<T>(Dictionary<T, HashSet<T>> dependenciesMap) => new(dependenciesMap);

        /// <summary>
        /// Attempts to perform a topological sort of a given acyclic graph.
        /// </summary>
        /// <returns>True if topological sort can be performed, false otherwise (means graph contains cycle(s)).</returns>
        public bool TryGetTopologicalSort(out IReadOnlyList<T> sortedElements)
        {
            List<T> result = new();
            sortedElements = result;

            // short circuit for empty graph
            if (IsEmpty)
            {
                return true;
            }

            var inDegreeLookup = _vertices.ToDictionary(v => v, v => 0);
            foreach (var depPair in _dependenciesMap)
            {
                inDegreeLookup[depPair.Key] = depPair.Value?.Count ?? 0;
            }

            Queue<T> noDependenciesQueue = new(inDegreeLookup.Where(kp => kp.Value == 0).Select(kp => kp.Key));
            var dependentsMap = _dependentsMap.Value;

            while (noDependenciesQueue.Count != 0)
            {
                T item = noDependenciesQueue.Dequeue();
                result.Add(item);

                foreach (T dependent in dependentsMap[item])
                {
                    if (--inDegreeLookup[dependent] == 0)
                    {
                        noDependenciesQueue.Enqueue(dependent);
                    }
                }
            }

            // if we haven't traverse everything then cycle exist in given graph
            return result.Count == _vertices.Count;
        }

        public IEnumerable<T> GetDependents(IEnumerable<T> vertices)
        {
            var dependentsMap = _dependentsMap.Value;
            return vertices.Select(v => dependentsMap[v]).SelectMany(v => v);
        }

        /// <summary>
        /// Gets the subset of the graph that contains all the dependencies mapping depending on given vertices.
        /// This is useful when we want to determine a subset of the graph that needs to be reevaluated if given nodes change value.
        /// </summary>
        /// <param name="vertices"></param>
        /// <param name="includeSeedVertices">
        /// Indication whether the given vertices should be included in the output graph.
        /// If set to false, (some of) the given nodes may still be part of output graph, in case they transitively depend on any node from input set.
        /// </param>
        /// <returns></returns>
        public DirectedGraph<T> GetSubGraphDependentOnVertices(IReadOnlyList<T> vertices, bool includeSeedVertices = false)
        {
            // Short circuit for empty graphs
            if (IsEmpty || vertices.Count == 0)
            {
                return Empty;
            }

            HashSet<T> dependentVertices = includeSeedVertices ? new HashSet<T>(vertices) : new HashSet<T>();
            Queue<T> directDependents = new(vertices);
            var dependentsMap = _dependentsMap.Value;

            while (directDependents.Count > 0)
            {
                T parent = directDependents.Dequeue();
                if (dependentsMap.ContainsKey(parent))
                {
                    directDependents.AddRange(dependentsMap[parent].Where(dependentVertices.Add));
                }
            }

            return _dependenciesMap.Where(p => dependentVertices.Contains(p.Key))
                .ToDictionary(p => p.Key, p => new HashSet<T>(p.Value.Where(dependentVertices.Contains)));
        }

        /// <summary>
        /// Detects a cycle in directed (possibly disconnected) graph and returns first found cycle in cycle variable.
        /// </summary>
        /// <param name="cycle">First cycle if any found.</param>
        /// <returns>True if cycle found, false otherwise.</returns>
        public bool HasCycle(out IReadOnlyList<T> cycle)
        {
            cycle = new List<T>();

            if (IsEmpty)
            {
                return false;
            }

            HashSet<T> visitedVertices = new();
            RecursionStack recursionStack = new();

            // detect cycles for any vertex (as we can have disconnected graph here)
            foreach (T vertex in _vertices)
            {
                if (IsCyclicUtil(vertex, visitedVertices, recursionStack))
                {
                    cycle = recursionStack.GetCycle();
                    return true;
                }
            }

            return false;
        }

        private static IReadOnlyList<T> GetVertices(Dictionary<T, HashSet<T>> dependenciesMap)
            => dependenciesMap.Keys.Union(dependenciesMap.Values.SelectMany(v => v)).Distinct().ToList();

        private static Dictionary<T, HashSet<T>> GetDependentsMap(Dictionary<T, HashSet<T>> dependenciesMap, IReadOnlyList<T> vertices)
        {
            var dependentsMap = vertices.ToDictionary(v => v, v => new HashSet<T>());
            foreach (KeyValuePair<T, HashSet<T>> keyValuePair in dependenciesMap)
            {
                foreach (T dependency in keyValuePair.Value!)
                {
                    _ = dependentsMap[dependency].Add(keyValuePair.Key);
                }
            }

            return dependentsMap;
        }

        private static Dictionary<T, HashSet<T>> DeepCopy(Dictionary<T, HashSet<T>> dependenciesMap)
        {
            return dependenciesMap
                .ToDictionary(kp => kp.Key, kp => kp.Value == null ? new() : new HashSet<T>(kp.Value));
        }

        private bool IsCyclicUtil(T vertex, HashSet<T> visitedVertices, RecursionStack recursionStack)
        {
            // Mark the current node as visited and part of recursion stack
            if (!recursionStack.TryPush(vertex))
            {
                return true;
            }

            if (!visitedVertices.Add(vertex))
            {
                recursionStack.Pop();
                return false;
            }

            if (_dependenciesMap.TryGetValue(vertex, out var children) && children != null && children.Count != 0)
            {
                foreach (T child in children)
                {
                    if (IsCyclicUtil(child, visitedVertices, recursionStack))
                    {
                        return true;
                    }
                }
            }

            recursionStack.Pop();

            return false;
        }

        private class RecursionStack
        {
            private readonly HashSet<T> _lookup = new();
            private readonly Stack<T> _stack = new();

            public bool TryPush(T item)
            {
                _stack.Push(item);
                return _lookup.Add(item);
            }

            public void Pop()
            {
                _ = _lookup.Remove(_stack.Pop());
            }

            public IReadOnlyList<T> GetCycle()
            {
                List<T> items = new();
                HashSet<T> visited = new();

                bool hasCycle = false;

                while (_stack.Count != 0)
                {
                    T current = _stack.Pop();
                    items.Add(current);
                    if (!visited.Add(current))
                    {
                        hasCycle = true;
                        break;
                    }
                }

                if (hasCycle)
                {
                    items.Reverse();
                }
                else
                {
                    items.Clear();
                }

                return items;
            }
        }
    }
}
