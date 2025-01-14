// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Rules;

namespace Microsoft.DotNet.ApiCompatibility.Mapping
{
    /// <summary>
    /// Object that represents a mapping between multiple <see cref="IAssemblySymbol"/> objects.
    /// This also holds a list of <see cref="INamespaceMapper"/> to represent the mapping of namespaces in between
    /// <see cref="IElementMapper{T}.Left"/> and <see cref="IElementMapper{T}.Right"/>.
    /// </summary>
    /// <param name="ruleRunner">The <see cref="IRuleRunner"/> that compares the assembly mapper elements.</param>
    /// <param name="settings">The <see cref="IMapperSettings"/> used to compare the assembly mapper elements.</param>
    /// <param name="rightSetSize">The number of elements in the right set to compare.</param>
    /// <param name="containingAssemblySet">The containing <see cref="IAssemblySetMapper"/>. <see langword="null" />, if the assembly isn't part of a set.</param>
    public class AssemblyMapper(IRuleRunner ruleRunner,
        IMapperSettings settings,
        int rightSetSize,
        IAssemblySetMapper? containingAssemblySet = null) : ElementMapper<ElementContainer<IAssemblySymbol>>(ruleRunner, settings, rightSetSize), IAssemblyMapper
    {
        private Dictionary<INamespaceSymbol, INamespaceMapper>? _namespaces;
        private readonly List<CompatDifference> _assemblyLoadErrors = [];

        /// <inheritdoc />
        public IAssemblySetMapper? ContainingAssemblySet { get; } = containingAssemblySet;

        /// <inheritdoc />
        public IEnumerable<CompatDifference> AssemblyLoadErrors => _assemblyLoadErrors;

        /// <inheritdoc />
        public IEnumerable<INamespaceMapper> GetNamespaces()
        {
            if (_namespaces == null)
            {
                _namespaces = new Dictionary<INamespaceSymbol, INamespaceMapper>(Settings.SymbolEqualityComparer);
                AddOrCreateMappers(Left, ElementSide.Left);

                for (int i = 0; i < Right.Length; i++)
                {
                    AddOrCreateMappers(Right[i], ElementSide.Right, i);
                }

                void AddOrCreateMappers(ElementContainer<IAssemblySymbol>? assemblyContainer, ElementSide side, int setIndex = 0)
                {
                    // Silently return if the element hasn't been added yet.
                    if (assemblyContainer == null)
                    {
                        return;
                    }

                    Dictionary<INamespaceSymbol, List<INamedTypeSymbol>> typeForwards = ResolveTypeForwards(assemblyContainer, side, Settings.SymbolEqualityComparer, setIndex);

                    Queue<INamespaceSymbol> queue = new();
                    queue.Enqueue(assemblyContainer.Element.GlobalNamespace);
                    while (queue.Count > 0)
                    {
                        INamespaceSymbol nsSymbol = queue.Dequeue();
                        bool hasTypeForwards = typeForwards.TryGetValue(nsSymbol, out List<INamedTypeSymbol>? types);
                        if (hasTypeForwards || nsSymbol.GetTypeMembers().Length > 0)
                        {
                            INamespaceMapper mapper = AddMapper(nsSymbol);
                            if (hasTypeForwards)
                            {
                                mapper.AddForwardedTypes(types, side, setIndex);

                                // remove the type forwards for this namespace as we did
                                // find and instance of the namespace on the current assembly
                                // and we don't want to create a mapper with the namespace in
                                // the assembly where the forwarded type is defined.
                                typeForwards.Remove(nsSymbol);
                            }
                        }

                        foreach (INamespaceSymbol child in nsSymbol.GetNamespaceMembers())
                            queue.Enqueue(child);
                    }

                    // If the current assembly didn't have a namespace symbol for the resolved type forwards
                    // use the namespace symbol in the assembly where the forwarded type is defined.
                    // But create the mapper with typeForwardsOnly setting to not visit types defined in the target assembly.
                    foreach (KeyValuePair<INamespaceSymbol, List<INamedTypeSymbol>> kvp in typeForwards)
                    {
                        INamespaceMapper mapper = AddMapper(kvp.Key, checkIfExists: true, typeForwardsOnly: true);
                        mapper.AddForwardedTypes(kvp.Value, side, setIndex);
                    }

                    INamespaceMapper AddMapper(INamespaceSymbol ns, bool checkIfExists = false, bool typeForwardsOnly = false)
                    {
                        if (!_namespaces.TryGetValue(ns, out INamespaceMapper? mapper))
                        {
                            mapper = new NamespaceMapper(RuleRunner, Settings, Right.Length, this, typeForwardsOnly: typeForwardsOnly);
                            _namespaces.Add(ns, mapper);
                        }
                        else if (checkIfExists && mapper.GetElement(side, setIndex) != null)
                        {
                            return mapper;
                        }

                        mapper.AddElement(ns, side, setIndex);
                        return mapper;
                    }
                }

                Dictionary<INamespaceSymbol, List<INamedTypeSymbol>> ResolveTypeForwards(ElementContainer<IAssemblySymbol> assembly,
                    ElementSide side,
                    IEqualityComparer<ISymbol> comparer,
                    int index)
                {
                    Dictionary<INamespaceSymbol, List<INamedTypeSymbol>> typeForwards = new(comparer);
                    foreach (INamedTypeSymbol symbol in assembly.Element.GetForwardedTypes())
                    {
                        if (symbol.TypeKind != TypeKind.Error)
                        {
                            if (!typeForwards.TryGetValue(symbol.ContainingNamespace, out List<INamedTypeSymbol>? types))
                            {
                                types = [];
                                typeForwards.Add(symbol.ContainingNamespace, types);
                            }

                            types.Add(symbol);
                        }
                    }

                    return typeForwards;
                }
            }

            return _namespaces.Values;
        }
    }
}
