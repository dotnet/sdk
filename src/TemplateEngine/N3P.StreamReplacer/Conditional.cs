using System.Collections.Generic;
using System.IO;
using System.Text;

namespace N3P.StreamReplacer
{
    public delegate bool ConditionEvaluator(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, IReadOnlyDictionary<string, object> args);

    public class Conditional : IOperationProvider
    {
        private readonly IReadOnlyDictionary<string, object> _args;
        private readonly string _elseIfToken;
        private readonly string _elseToken;
        private readonly string _endIfToken;
        private readonly string _ifToken;
        private readonly ConditionEvaluator _evaluator;

        public Conditional(string ifToken, string elseToken, string elseIfToken, string endIfToken, ConditionEvaluator evaluator, IReadOnlyDictionary<string, object> args)
        {
            _evaluator = evaluator;
            _ifToken = ifToken;
            _elseToken = elseToken;
            _elseIfToken = elseIfToken;
            _endIfToken = endIfToken;
            _args = args;
        }

        public IOperation GetOperation(Encoding encoding)
        {
            byte[] ifToken = encoding.GetBytes(_ifToken);
            byte[] endToken = encoding.GetBytes(_endIfToken);

            List<byte[]> tokens = new List<byte[]>
            {
                ifToken,
                endToken
            };

            SimpleTrie trie = new SimpleTrie();
            trie.AddToken(ifToken, 0);
            trie.AddToken(endToken, 1);

            int elseIfTokenIndex = -1;

            if (!string.IsNullOrEmpty(_elseToken))
            {
                byte[] elseToken = encoding.GetBytes(_elseToken);
                trie.AddToken(elseToken, tokens.Count);
                tokens.Add(elseToken);
            }

            if (!string.IsNullOrEmpty(_elseIfToken))
            {
                byte[] elseIfToken = encoding.GetBytes(_elseIfToken);
                elseIfTokenIndex = tokens.Count;
                trie.AddToken(elseIfToken, elseIfTokenIndex);
                tokens.Add(elseIfToken);
            }

            byte[][] eolMarkers = new byte[2][];
            eolMarkers[0] = encoding.GetBytes("\r");
            eolMarkers[1] = encoding.GetBytes("\n");

            return new Impl(this, tokens, elseIfTokenIndex, trie, eolMarkers);
        }

        private class Impl : IOperation
        {
            private readonly Conditional _definition;
            private EvaluationState _current;
            private readonly int _elseIfTokenIndex;
            private readonly Stack<EvaluationState> _pendingCompletion = new Stack<EvaluationState>();
            private readonly SimpleTrie _trie;
            private readonly byte[][] _eolMarkers;

            public Impl(Conditional definition, IReadOnlyList<byte[]> tokens, int elseIfTokenIndex, SimpleTrie trie, byte[][] eolMarkers)
            {
                _trie = trie;
                _elseIfTokenIndex = elseIfTokenIndex;
                _definition = definition;
                Tokens = tokens;
                _eolMarkers = eolMarkers;
            }

            public IOperationProvider Definition => _definition;

            public IReadOnlyList<byte[]> Tokens { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                TrimBackToPreviousEOL(target);
BEGIN:
                //Got the "if" token...
                if (token == 0)
                {
                    if (_current == null)
                    {
                        _current = new EvaluationState(this);
                    }
                    else
                    {
                        _pendingCompletion.Push(_current);
                        _current = new EvaluationState(this);
                    }

                    //If the "if" branch is taken, all else and elseif blocks will be omitted, return
                    //  control to the processor so nested "if"s/mutations can be processed. Note that
                    //  this block will not be terminated until the corresponding endif is found
                    if (_current.Evaluate(processor, ref bufferLength, ref currentBufferPosition))
                    {
                        return 0;
                    }

                    while (SeekToTerminator(processor, ref bufferLength, ref currentBufferPosition, out token) && token == 0)
                    {
                        int balance = 1;

                        //We're in a nested if branch, wait until it balances closed
                        while (balance > 0 && SeekToTerminator(processor, ref bufferLength, ref currentBufferPosition, out token))
                        {
                            if (token == 0)
                            {
                                ++balance;
                            }
                            else if (token == 1)
                            {
                                --balance;
                            }
                        }
                    }

                    if (token >= 0)
                    {
                        goto BEGIN;
                    }

                    return 0;
                }

                //If we've got an unbalanced statement, emit the token
                if (_current == null)
                {
                    byte[] tokenValue = Tokens[token];
                    target.Write(tokenValue, 0, tokenValue.Length);
                    return tokenValue.Length;
                }

                //Got the endif token, exit to the parent "if" scope if it exists
                if (token == 1)
                {
                    _current = null;

                    if (_pendingCompletion.Count > 0)
                    {
                        _current = _pendingCompletion.Pop();
                    }

                    ConsumeToEndOfLine(processor, ref bufferLength, ref currentBufferPosition);
                    return 0;
                }

                if (_current.BranchTaken)
                {
                    int depth = 0;
                    //A previous branch was taken. Skip to the endif token.
                    while (SeekToTerminator(processor, ref bufferLength, ref currentBufferPosition, out token) && (depth > 0 || token != 1))
                    {
                        if (token == 0)
                        {
                            ++depth;
                        }
                        else if (token == 1)
                        {
                            --depth;
                        }
                    }

                    goto BEGIN;
                }

                //We have an "elseif" and haven't taken a previous branch
                if (token == _elseIfTokenIndex)
                {
                    //If the elseif branch is taken, return control for replacements to be done as usual
                    if (_current.Evaluate(processor, ref bufferLength, ref currentBufferPosition))
                    {
                        return 0;
                    }

                    //The "elseif" branch was not taken. Skip to the following else, elseif or endif token
                    if (SeekToTerminator(processor, ref bufferLength, ref currentBufferPosition, out token))
                    {
                        goto BEGIN;
                    }

                    return 0;
                }

                //We have an "else" token and haven't taken any other branches, return control
                //  after setting that a branch has been taken
                _current.BranchTaken = true;
                ConsumeToEndOfLine(processor, ref bufferLength, ref currentBufferPosition);
                return 0;
            }

            private static void ConsumeToEndOfLine(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition)
            {
                while (bufferLength > processor.EOLMarkers.Length)
                {
                    for (; currentBufferPosition < bufferLength - processor.EOLMarkers.Length + 1; ++currentBufferPosition)
                    {
                        if (bufferLength == 0)
                        {
                            currentBufferPosition = 0;
                            return;
                        }

                        int token;
                        if (processor.EOLMarkers.GetOperation(processor.CurrentBuffer, bufferLength, ref currentBufferPosition, out token))
                        {
                            return;
                        }
                    }

                    processor.AdvanceBuffer(bufferLength - processor.EOLMarkers.Length + 1);
                    currentBufferPosition = processor.CurrentBufferPosition;
                    bufferLength = processor.CurrentBufferLength;
                }
            }

            private void TrimBackToPreviousEOL(Stream target)
            {
                int maxEOLLength = 0;
                for (int i = 0; i < _eolMarkers.Length; ++i)
                {
                    if (_eolMarkers[i].Length > maxEOLLength)
                    {
                        maxEOLLength = _eolMarkers[i].Length;
                    }
                }

                while (target.Position > 0)
                {
                    byte[] buffer = new byte[maxEOLLength];
                    target.Position -= maxEOLLength;
                    target.Read(buffer, 0, buffer.Length);

                    for (int i = 0; i < _eolMarkers.Length; ++i)
                    {
                        for (int j = 0; j <= buffer.Length - _eolMarkers[i].Length; ++j)
                        {
                            bool allMatch = true;
                            for (int k = 0; allMatch && k < _eolMarkers[i].Length; ++k)
                            {
                                if (_eolMarkers[i][k] != buffer[j + k])
                                {
                                    allMatch = false;
                                }
                            }

                            if (allMatch)
                            {
                                target.Position -= j;
                                target.SetLength(target.Position);
                                return;
                            }
                        }
                    }

                    //Back up the amount we already read to get a new window of data in
                    target.Position -= maxEOLLength;
                }
            }

            private bool SeekToTerminator(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, out int token)
            {
                while (bufferLength > _trie.Length)
                {
                    for (; currentBufferPosition < bufferLength - _trie.Length + 1; ++currentBufferPosition)
                    {
                        if (bufferLength == 0)
                        {
                            token = -1;
                            currentBufferPosition = 0;
                            return false;
                        }

                        if (_trie.GetOperation(processor.CurrentBuffer, bufferLength, ref currentBufferPosition, out token))
                        {
                            return true;
                        }
                    }

                    processor.AdvanceBuffer(bufferLength - _trie.Length + 1);
                    currentBufferPosition = processor.CurrentBufferPosition;
                    bufferLength = processor.CurrentBufferLength;
                }

                token = -1;
                return false;
            }

            private class EvaluationState
            {
                private bool _branchTaken;
                private readonly Impl _impl;

                public EvaluationState(Impl impl)
                {
                    _impl = impl;
                }

                internal bool Evaluate(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition)
                {
                    BranchTaken = _impl._definition._evaluator(processor, ref bufferLength, ref currentBufferPosition, _impl._definition._args);
                    return BranchTaken;
                }

                public bool BranchTaken
                {
                    get { return _branchTaken; }
                    set { _branchTaken |= value; }
                }
            }
        }
    }
}
