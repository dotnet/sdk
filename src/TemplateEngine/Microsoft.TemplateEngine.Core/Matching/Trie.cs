// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Core.Matching
{
    public class Trie<T>
        where T : TerminalBase
    {
        private byte _singleNextNodeMatch;
        private TrieNode<T>? _singleNextNode;
        private Dictionary<byte, TrieNode<T>>? _nextNodes;

        public Trie()
        {
        }

        public Dictionary<byte, TrieNode<T>> NextNodes
        {
            get
            {
                Dictionary<byte, TrieNode<T>>? nextNodes = _nextNodes;
                if (nextNodes != null)
                {
                    return nextNodes;
                }

                nextNodes = new Dictionary<byte, TrieNode<T>>();
                if (_singleNextNode != null)
                {
                    nextNodes.Add(_singleNextNodeMatch, _singleNextNode);
                }

                return Interlocked.CompareExchange(ref _nextNodes, nextNodes, null) ?? nextNodes;
            }
        }

        public int MaxRemainingLength { get; private set; }

        public void AddPath(byte[] path, T terminal)
        {
            if (path.Length > MaxRemainingLength)
            {
                MaxRemainingLength = path.Length;
            }

            int remainingLength = path.Length - 1;
            Trie<T> current = this;
            for (int i = 0; i < path.Length; ++i, --remainingLength)
            {
                TrieNode<T> next = current.GetOrAddNextNode(path[i], remainingLength);

                if (i == path.Length - 1)
                {
                    next.Terminals ??= new List<T>();

                    int sameMatcherIndex = next.Terminals.FindIndex(t => t.Start == terminal.Start && t.End == terminal.End);

                    if (sameMatcherIndex > -1)
                    {
                        // this matching is identical to another terminal already added to the trie. Overwrite it.
                        next.Terminals[sameMatcherIndex] = terminal;
                    }
                    else
                    {
                        next.Terminals.Add(terminal);
                    }
                }

                current = next;
            }
        }

        internal bool TryGetNextNode(byte match, out TrieNode<T> next)
        {
            if (_nextNodes != null)
            {
                return _nextNodes.TryGetValue(match, out next!);
            }

            if (_singleNextNode != null && _singleNextNodeMatch == match)
            {
                next = _singleNextNode;
                return true;
            }

            next = null!;
            return false;
        }

        private TrieNode<T> GetOrAddNextNode(byte match, int remainingLength)
        {
            TrieNode<T> next;
            if (_nextNodes != null)
            {
                if (!_nextNodes.TryGetValue(match, out next!))
                {
                    _nextNodes.Add(match, next = new TrieNode<T>(match));
                }
            }
            else if (_singleNextNode == null)
            {
                _singleNextNodeMatch = match;
                _singleNextNode = next = new TrieNode<T>(match);
            }
            else if (_singleNextNodeMatch == match)
            {
                next = _singleNextNode;
            }
            else
            {
                next = new TrieNode<T>(match);
                NextNodes.Add(match, next);
            }

            if (next.MaxRemainingLength < remainingLength)
            {
                next.MaxRemainingLength = remainingLength;
            }

            return next;
        }
    }
}
