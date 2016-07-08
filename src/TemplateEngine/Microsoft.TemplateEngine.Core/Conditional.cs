using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions.Engine;

namespace Microsoft.TemplateEngine.Core
{
    public class Conditional : IOperationProvider
    {
        private readonly string _elseIfToken;
        private readonly string _elseToken;
        private readonly string _endIfToken;
        private readonly string _ifToken;
        private readonly ConditionEvaluator _evaluator;
        private readonly bool _wholeLine;
        private readonly bool _trimWhitespace;

        public string IfToken => _ifToken;

        public string ElseIfToken => _elseIfToken;

        public string ElseToken => _elseToken;

        public string EndIfToken => _endIfToken;

        public bool WholeLine => _wholeLine;

        public bool TrimWhitespace => _trimWhitespace;

        public ConditionEvaluator Evaluator => _evaluator;

        public Conditional(string ifToken, string elseToken, string elseIfToken, string endIfToken, bool wholeLine, bool trimWhitespace, ConditionEvaluator evaluator)
        {
            _trimWhitespace = trimWhitespace;
            _wholeLine = wholeLine;
            _evaluator = evaluator;
            _ifToken = ifToken;
            _elseToken = elseToken;
            _elseIfToken = elseIfToken;
            _endIfToken = endIfToken;
        }

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            byte[] ifToken = encoding.GetBytes(_ifToken);
            byte[] endToken = encoding.GetBytes(_endIfToken);

            List<byte[]> tokens = new List<byte[]>
            {
                ifToken,
                endToken
            };

            TokenTrie trie = new TokenTrie();
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
            private readonly TokenTrie _trie;

            public Impl(Conditional definition, IReadOnlyList<byte[]> tokens, int elseIfTokenIndex, TokenTrie trie)
            {
                _trie = trie;
                _elseIfTokenIndex = elseIfTokenIndex;
                _definition = definition;
                Tokens = tokens;
            }

            public IReadOnlyList<byte[]> Tokens { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                bool flag;
                if (processor.Config.Flags.TryGetValue("conditionals", out flag) && !flag)
                {
                    byte[] tokenValue = Tokens[token];
                    target.Write(tokenValue, 0, tokenValue.Length);
                    return tokenValue.Length;
                }

                if (_current != null || token == 0)
                {
                    if (_definition._wholeLine)
                    {
                        processor.SeekBackUntil(processor.EncodingConfig.LineEndings);
                    }
                    else if (_definition._trimWhitespace)
                    {
                        processor.TrimWhitespace(false, true, ref bufferLength, ref currentBufferPosition);
                    }
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
                        if (_definition.WholeLine)
                        {
                            processor.SeekForwardThrough(processor.EncodingConfig.LineEndings, ref bufferLength, ref currentBufferPosition);
                        }

                        return 0;
                    }

                    if (_definition.WholeLine)
                    {
                        processor.SeekForwardThrough(processor.EncodingConfig.LineEndings, ref bufferLength, ref currentBufferPosition);
                    }

                    SeekToTerminator(processor, ref bufferLength, ref currentBufferPosition, out token);

                    //Keep on scanning until we've hit a balancing token that belongs to us
                    while(token == 0)
                    {
                        int open = 1;
                        while(open != 0)
                        {
                            SeekToTerminator(processor, ref bufferLength, ref currentBufferPosition, out token);
                            if(token == 0)
                            {
                                ++open;
                            }
                            else if(token == 1)
                            {
                                --open;
                            }
                        }

                        SeekToTerminator(processor, ref bufferLength, ref currentBufferPosition, out token);
                    }

                    goto BEGIN;
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
                        processor.SeekForwardThrough(processor.EncodingConfig.LineEndings, ref bufferLength, ref currentBufferPosition);
                    }
                    else if (_definition._trimWhitespace)
                    {
                        processor.TrimWhitespace(true, false, ref bufferLength, ref currentBufferPosition);
                    }

                    return 0;
                }

                if (_current.BranchTaken)
                {
                    processor.SeekBackUntil(processor.EncodingConfig.LineEndings, true);
                    //A previous branch was taken. Skip to the endif token.
                    SkipToMatchingEndif(processor, ref bufferLength, ref currentBufferPosition, ref token);
                    return 0;
                }

                //We have an "elseif" and haven't taken a previous branch
                if (token == _elseIfTokenIndex)
                {
                    //If the elseif branch is taken, return control for replacements to be done as usual
                    if (!_current.Evaluate(processor, ref bufferLength, ref currentBufferPosition))
                    {
                        if (_definition.WholeLine)
                        {
                            processor.SeekForwardThrough(processor.EncodingConfig.LineEndings, ref bufferLength, ref currentBufferPosition);
                        }

                        if (SeekToTerminator(processor, ref bufferLength, ref currentBufferPosition, out token))
                        {
                            goto BEGIN;
                        }
                    }

                    if (_definition.WholeLine)
                    {
                        processor.SeekForwardThrough(processor.EncodingConfig.LineEndings, ref bufferLength, ref currentBufferPosition);
                    }

                    //The "elseif" branch was not taken. Skip to the following else, elseif or endif token
                    return 0;
                }

                //We have an "else" token and haven't taken any other branches, return control
                //  after setting that a branch has been taken
                _current.BranchTaken = true;
                processor.WhitespaceHandler(ref bufferLength, ref currentBufferPosition, wholeLine: _definition._wholeLine, trim: _definition._trimWhitespace);
                return 0;
            }

            private void SkipToMatchingEndif(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, ref int token)
            {
                while (token != 1 && SeekToTerminator(processor, ref bufferLength, ref currentBufferPosition, out token) && token != 1)
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
            }

            private bool SeekToTerminator(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, out int token)
            {
                while (bufferLength >= _trie.MinLength)
                {
                    for (; currentBufferPosition < bufferLength - _trie.MinLength + 1; ++currentBufferPosition)
                    {
                        if (_trie.GetOperation(processor.CurrentBuffer, bufferLength, ref currentBufferPosition, out token))
                        {
                            return true;
                        }
                    }

                    processor.AdvanceBuffer(bufferLength - _trie.MaxLength + 1);
                    currentBufferPosition = processor.CurrentBufferPosition;
                    bufferLength = processor.CurrentBufferLength;
                }

                //If we run out of places to look, assert that the end of the buffer is the end
                token = 1;
                currentBufferPosition = bufferLength;
                return true;
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
                    BranchTaken = _impl._definition._evaluator(processor, ref bufferLength, ref currentBufferPosition);
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
