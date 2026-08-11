// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Mapping;

namespace Microsoft.DotNet.ApiCompatibility
{
    /// <summary>
    /// A visitor that traverses the mapping tree and stores found differences as <see cref="CompatDifference" /> items.
    /// </summary>
    public class DifferenceVisitor : IDifferenceVisitor
    {
        private readonly HashSet<CompatDifference> _compatDifferences = [];

        /// <inheritdoc />
        public IEnumerable<CompatDifference> CompatDifferences => _compatDifferences;

        /// <inheritdoc />
        public void Visit<T>(IElementMapper<T> mapper)
        {
            if (mapper is IAssemblySetMapper assemblySetMapper)
            {
                Visit(assemblySetMapper);
            }
            else if (mapper is IAssemblyMapper assemblyMapper)
            {
                Visit(assemblyMapper);
            }
            else if (mapper is INamespaceMapper nsMapper)
            {
                Visit(nsMapper);
            }
            else if (mapper is ITypeMapper typeMapper)
            {
                Visit(typeMapper);
            }
            else if (mapper is IMemberMapper memberMapper)
            {
                Visit(memberMapper);
            }
        }

        /// <inheritdoc />
        public void Visit(IAssemblySetMapper mapper)
        {
            foreach (IAssemblyMapper assembly in mapper.GetAssemblies())
            {
                Visit(assembly);
            }
        }

        /// <inheritdoc />
        public void Visit(IAssemblyMapper assembly)
        {
            AddSymbolDifferences(assembly.GetDifferences());

            foreach (INamespaceMapper @namespace in assembly.GetNamespaces())
            {
                Visit(@namespace);
            }
        }

        /// <inheritdoc />
        public void Visit(INamespaceMapper @namespace)
        {
            foreach (ITypeMapper type in @namespace.GetTypes())
            {
                Visit(type);
            }
        }

        /// <inheritdoc />
        public void Visit(ITypeMapper type)
        {
            AddSymbolDifferences(type);

            if (type.ShouldDiffMembers)
            {
                foreach (ITypeMapper nestedType in type.GetNestedTypes())
                {
                    Visit(nestedType);
                }

                foreach (IMemberMapper member in type.GetMembers())
                {
                    Visit(member);
                }
            }
        }

        /// <inheritdoc />
        public void Visit(IMemberMapper member)
        {
            AddSymbolDifferences(member);
        }

        private void AddSymbolDifferences<T>(IElementMapper<T> mapper)
            where T : ISymbol
            => AddSymbolDifferences(mapper.GetDifferences());

        private void AddSymbolDifferences(IEnumerable<CompatDifference> differences)
        {
            foreach (CompatDifference item in differences)
            {
                ApiStability leftStability = ApiStabilityClassifier.Classify(item.LeftSymbol);
                ApiStability rightStability = ApiStabilityClassifier.Classify(item.RightSymbol);

                bool isExperimentalDifference =
                    (item.LeftSymbol is null && rightStability == ApiStability.Experimental) ||
                    (item.RightSymbol is null && leftStability == ApiStability.Experimental) ||
                    (leftStability == ApiStability.Experimental && rightStability == ApiStability.Experimental);

                _compatDifferences.Add(isExperimentalDifference
                    ? item.WithSeverity(DifferenceSeverity.Informational)
                    : item);
            }
        }
    }
}
