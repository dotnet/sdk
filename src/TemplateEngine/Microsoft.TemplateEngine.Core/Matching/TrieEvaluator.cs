// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Core.Matching
{
    public class TrieEvaluator<T>
        where T : TerminalBase
    {
        private readonly List<TriePath<T>> _activePaths;
        private readonly Trie<T> _trie;
        private int _expectedSequenceNumber;
        private int _lastReturnedTerminalEndSequenceNumber;
        private int _lastSeenSequenceNumber;
        private bool _waitForSequenceNumberCatchUp;

        public TrieEvaluator(Trie<T> trie)
        {
            _trie = trie;
            _activePaths = new List<TriePath<T>>();
            MaxLength = trie.MaxRemainingLength;
        }

        public int MaxLength { get; }

        public int OldestRequiredSequenceNumber => _activePaths.Count == 0 ? _expectedSequenceNumber : _activePaths[0].StartSequenceNumber;

        //TODO: Figure out how to do readahead detection better - tracking the last seen sequence number
        //  is too slow
        public bool Accept(byte data, ref int sequenceNumber, out TerminalLocation<T> terminal)
        {
            //If we're getting data from closer to the end of the stream instead
            //  of the next byte we're expecting, things that start before the new
            //  sequence number can be discarded (as they won't have read what they
            //  think they've read
            if (sequenceNumber > _expectedSequenceNumber)
            {
                for (int i = 0; i < _activePaths.Count; ++i)
                {
                    if (_activePaths[i].StartSequenceNumber < sequenceNumber)
                    {
                        _activePaths.RemoveAt(i--);
                    }
                }
            }
            //The sequence number could also have lessened (an early terminal in
            //  a path has finally been deemed OK to return, the buffer position
            //  returns to the position after the matched token). If the buffer
            //  position advances into data where a match has already been read
            //  we need to remove those paths. For paths that start after the
            //  new sequence number, they can simply wait to be processed again
            //  until the sequence number catches up to the expected sequence
            //  number
            else if (sequenceNumber < _expectedSequenceNumber)
            {
                if (!_waitForSequenceNumberCatchUp || sequenceNumber < _lastSeenSequenceNumber)
                {
                    //Remove entries that have been impacted by a readahead
                    if (_lastReturnedTerminalEndSequenceNumber != sequenceNumber)
                    {
                        for (int i = 0; i < _activePaths.Count; ++i)
                        {
                            if (_activePaths[i].StartSequenceNumber < sequenceNumber)
                            {
                                _activePaths.RemoveAt(i--);
                            }
                        }
                    }

                    _waitForSequenceNumberCatchUp = true;
                }

                //We may be walking through an overlapped match
                //  here, check to see if we've made it to the
                //  end of an already completed path
                _lastSeenSequenceNumber = sequenceNumber;
                return TryGetNext(false, ref sequenceNumber, out terminal);
            }

            _lastSeenSequenceNumber = sequenceNumber;

            //If we've made it here (we didn't end up quitting early due to
            //  sequence numbers being too low), we can process matches.
            //  Bump up the next expected byte and make sure we don't think
            //  we're in waiting mode.
            _expectedSequenceNumber = sequenceNumber + 1;
            _waitForSequenceNumberCatchUp = false;
            TrieNode<T> next;

            //Process paths in progress
            for (int i = 0; i < _activePaths.Count; ++i)
            {
                TriePath<T> path = _activePaths[i];

                //If the current path can advance...
                if (path.CurrentNode != null)
                {
                    //If we matched another byte, advance the current node in
                    //  the path and log the encountered terminal (if applicable)
                    if (path.CurrentNode.NextNodes.TryGetValue(data, out next))
                    {
                        path.CurrentNode = next;

                        if (next.IsTerminal)
                        {
                            path.EncounteredTerminals.AddRange(next.Terminals);
                        }
                    }
                    //If we didn't match and no terminals were found in the
                    //  path, remove the path from tracking
                    else if (path.EncounteredTerminals.Count == 0)
                    {
                        _activePaths.RemoveAt(i--);
                    }
                    //If we didn't match, but did find terminals, indicate
                    //  that the path can no longer advance
                    else
                    {
                        path.CurrentNode = null;
                    }
                }
            }

            //Try to start a new path in the trie
            if (_trie.NextNodes.TryGetValue(data, out next))
            {
                TriePath<T> path = new TriePath<T>(sequenceNumber)
                {
                    CurrentNode = next
                };

                if (next.IsTerminal)
                {
                    path.EncounteredTerminals.AddRange(next.Terminals);
                }

                _activePaths.Add(path);
            }

            return TryGetNext(false, ref sequenceNumber, out terminal);
        }

        public void FinalizeMatchesInProgress(ref int sequenceNumber, out TerminalLocation<T> terminals)
        {
            TryGetNext(true, ref sequenceNumber, out terminals);
        }

        public bool TryGetNext(bool isFinal, ref int sequenceNumber, out TerminalLocation<T> terminalLocation)
        {
            //See if there's anything we can return yet using the following
            //  conditions
            //  1) There must be at least one active path
            //  2) The 0th path must have terminated
            //  3) Any terminal being returned must be the leftmost available
            //  4) The same terminal should never be returned more than once
            if (_activePaths.Count > 0)
            {
                int endedAt = 0;
                T best = null;
                int bestPath = -1;
                int minNonTerminatedPathStart = int.MaxValue;
                int minTerminalStart = int.MaxValue;

                for (int i = 0; i < _activePaths.Count; ++i)
                {
                    TriePath<T> path = _activePaths[i];

                    if (path.CurrentNode != null && !isFinal)
                    {
                        if (path.StartSequenceNumber < minNonTerminatedPathStart)
                        {
                            minNonTerminatedPathStart = path.StartSequenceNumber;

                            if (best != null && minNonTerminatedPathStart < minTerminalStart)
                            {
                                best = null;
                            }
                        }
                    }
                    else
                    {
                        int ssn = path.StartSequenceNumber;
                        for (int j = 0; j < path.EncounteredTerminals.Count; ++j)
                        {
                            T terminal = path.EncounteredTerminals[j];
                            int start = terminal.Start + ssn;

                            if (start >= _lastReturnedTerminalEndSequenceNumber)
                            {
                                if (start < minNonTerminatedPathStart && (best == null || start < minTerminalStart || start == minTerminalStart && terminal.End > best.End))
                                {
                                    bestPath = i;
                                    best = terminal;
                                    minTerminalStart = start;
                                    endedAt = terminal.End + ssn;
                                }
                            }
                            else
                            {
                                path.EncounteredTerminals.RemoveAt(j--);
                            }
                        }

                        if (path.EncounteredTerminals.Count == 0)
                        {
                            _activePaths.RemoveAt(i--);
                        }
                    }
                }

                if (best != null)
                {
                    terminalLocation = new TerminalLocation<T>(best, minTerminalStart);
                    _lastReturnedTerminalEndSequenceNumber = endedAt + 1;
                    sequenceNumber = endedAt;

                    if (bestPath > -1 && bestPath < _activePaths.Count && _activePaths[bestPath].EncounteredTerminals.Contains(best))
                    {
                        _activePaths[bestPath].EncounteredTerminals.Remove(best);

                        if (_activePaths[bestPath].EncounteredTerminals.Count == 0)
                        {
                            _activePaths.RemoveAt(bestPath);
                        }
                    }

                    return true;
                }
            }

            terminalLocation = null;
            return false;
        }
    }
}
