using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Core.Matching
{
    internal class TriePath<T>
        where T : TerminalBase
    {
        public readonly List<T> EncounteredTerminals;

        public readonly int StartSequenceNumber;

        public TrieNode<T> CurrentNode;

        public TriePath(int startSequenceNumber)
        {
            StartSequenceNumber = startSequenceNumber;
            EncounteredTerminals = new List<T>();
        }
    }
}