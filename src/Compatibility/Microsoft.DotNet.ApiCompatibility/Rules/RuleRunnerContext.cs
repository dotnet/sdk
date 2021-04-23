// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
//

using System;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using Microsoft.DotNet.ApiCompatibility.Abstractions;

namespace Microsoft.DotNet.ApiCompatibility.Rules
{
    public class RuleRunnerContext
    {
        private readonly List<Action<IAssemblySymbol, IAssemblySymbol, IList<CompatDifference>>> _onAssemblySymbolActions = new();
        private readonly List<Action<ITypeSymbol, ITypeSymbol, IList<CompatDifference>>> _onTypeSymbolActions = new();
        private readonly List<Action<ISymbol, ISymbol, IList<CompatDifference>>> _onMemberSymbolActions = new();

        public void RegisterOnAssemblySymbolAction(Action<IAssemblySymbol, IAssemblySymbol, IList<CompatDifference>> action)
        {
            _onAssemblySymbolActions.Add(action);
        }

        public void RegisterOnTypeSymbolAction(Action<ITypeSymbol, ITypeSymbol, IList<CompatDifference>> action)
        {
            _onTypeSymbolActions.Add(action);
        }

        public void RegisterOnMemberSymbolAction(Action<ISymbol, ISymbol, IList<CompatDifference>> action)
        {
            _onMemberSymbolActions.Add(action);
        }

        internal void RunOnAssemblySymbolActions(IAssemblySymbol left, IAssemblySymbol right, List<CompatDifference> differences)
        {
            foreach (Action<IAssemblySymbol, IAssemblySymbol, IList<CompatDifference>> action in _onAssemblySymbolActions)
            {
                action(left, right, differences);
            }
        }

        internal void RunOnTypeSymbolActions(ITypeSymbol left, ITypeSymbol right, List<CompatDifference> differences)
        {
            foreach (Action<ITypeSymbol, ITypeSymbol, IList<CompatDifference>> action in _onTypeSymbolActions)
            {
                action(left, right, differences);
            }
        }

        internal void RunOnMemberSymbolActions(ISymbol left, ISymbol right, List<CompatDifference> differences)
        {
            foreach (Action<ISymbol, ISymbol, IList<CompatDifference>> action in _onMemberSymbolActions)
            {
                action(left, right, differences);
            }
        }
    }
}
