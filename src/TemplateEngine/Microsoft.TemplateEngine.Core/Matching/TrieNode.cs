using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Core.Matching
{
    public class TrieNode<T> : Trie<T>
        where T : TerminalBase
    {
        public readonly byte Match;

        public List<T> Terminals;

        public TrieNode(byte match)
        {
            Match = match;
        }

        public bool IsTerminal => Terminals != null;
    }
}