using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Mutant.Chicken
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
        private readonly bool _wholeLine;
        private readonly bool _trimWhitespace;

        public Conditional(string ifToken, string elseToken, string elseIfToken, string endIfToken, bool wholeLine, bool trimWhitespace, ConditionEvaluator evaluator, IReadOnlyDictionary<string, object> args)
        {
            _trimWhitespace = trimWhitespace;
            _wholeLine = wholeLine;
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

            return new Impl(this, tokens, elseIfTokenIndex, trie);
        }

        private class Impl : IOperation
        {
            private readonly Conditional _definition;
            private EvaluationState _current;
            private readonly int _elseIfTokenIndex;
            private readonly Stack<EvaluationState> _pendingCompletion = new Stack<EvaluationState>();
            private readonly SimpleTrie _trie;

            public Impl(Conditional definition, IReadOnlyList<byte[]> tokens, int elseIfTokenIndex, SimpleTrie trie)
            {
                _trie = trie;
                _elseIfTokenIndex = elseIfTokenIndex;
                _definition = definition;
                Tokens = tokens;
            }

            public IOperationProvider Definition => _definition;

            public IReadOnlyList<byte[]> Tokens { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                if (_definition._wholeLine)
                {
                    processor.TrimBackToPreviousEOL();
                }
                else if (_definition._trimWhitespace)
                {
                    processor.TrimBackWhitespace();
                }

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

                    if (_definition._wholeLine)
                    {
                        processor.ConsumeToEndOfLine(ref bufferLength, ref currentBufferPosition);
                    }
                    else if (_definition._trimWhitespace)
                    {
                        processor.TrimForwardWhitespace();
                    }

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

                if (_definition._wholeLine)
                {
                    processor.ConsumeToEndOfLine(ref bufferLength, ref currentBufferPosition);
                }
                else if (_definition._trimWhitespace)
                {
                    processor.TrimBackWhitespace();
                }

                return 0;
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
