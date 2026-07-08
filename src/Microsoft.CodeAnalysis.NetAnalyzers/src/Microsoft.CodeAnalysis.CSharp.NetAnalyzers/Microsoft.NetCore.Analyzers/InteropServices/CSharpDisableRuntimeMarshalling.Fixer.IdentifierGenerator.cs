// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.NetCore.Analyzers.InteropServices
{
    public sealed partial class CSharpDisableRuntimeMarshallingFixer
    {
        private class IdentifierGenerator
        {
            private int? _nextIdentifier;

            public IdentifierGenerator(SemanticModel model, int offsetForSpeculativeSymbolResolution)
            {
                _nextIdentifier = FindFirstUnusedIdentifierIndex(model, offsetForSpeculativeSymbolResolution, "ptr");
            }
            public IdentifierGenerator(SemanticModel model, IBlockOperation block)
            {
                _nextIdentifier = FindFirstUnusedIdentifierIndex(model, block.Syntax.SpanStart, "ptr");
                HashSet<string> localNames = new HashSet<string>(block.Locals.Select(x => x.Name));
                string? identifier = NextIdentifier();
                while (identifier is not null && localNames.Contains(identifier))
                {
                    identifier = NextIdentifier();
                }

                if (identifier is not null)
                {
                    // The last identifier was not in use, so go back one to use it the next call.
                    _nextIdentifier--;
                }
            }

            public string? NextIdentifier()
            {
                if (_nextIdentifier is null or int.MaxValue)
                {
                    return null;
                }

                if (_nextIdentifier == 0)
                {
                    _nextIdentifier++;
                    return "ptr";
                }

                return $"ptr{_nextIdentifier++}";
            }
        }
    }
}
