// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Core.Matching
{
    public class TrieNode<T> : Trie<T>
        where T : TerminalBase
    {
        public TrieNode(byte match)
        {
            Match = match;
        }

        public byte Match { get; }

        public List<T> Terminals { get; set; }

        public bool IsTerminal => Terminals != null;
    }
}
