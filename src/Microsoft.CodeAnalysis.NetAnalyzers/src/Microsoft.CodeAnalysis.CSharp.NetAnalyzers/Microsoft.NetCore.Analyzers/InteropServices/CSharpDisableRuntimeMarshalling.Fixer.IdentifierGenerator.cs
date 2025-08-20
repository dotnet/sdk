// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the MIT license.  See License.txt in the project root for license information.

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
