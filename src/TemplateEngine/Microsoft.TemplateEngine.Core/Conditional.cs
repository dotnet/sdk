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

        // for comment / uncomment handling
        private readonly string _ifTokenFlagged;
        private readonly string _elseTokenFlagged;
        private readonly string _elseIfTokenFlagged;
        private readonly int? _operationToDisable;

        // the unusual order of these is historical, no special meaning
        private const int IfTokenIndex = 0;
        private const int EndTokenIndex = 1;
        private const int ElseIfTokenIndex = 2;
        private const int ElseTokenIndex = 3;

        private const int IfTokenFlaggedIndex = 4;
        private const int ElseIfTokenFlaggedIndex = 5;
        private const int ElseTokenFlaggedIndex = 6;

        public string IfToken => _ifToken;

        public string ElseIfToken => _elseIfToken;

        public string ElseToken => _elseToken;

        public string EndIfToken => _endIfToken;

        public bool WholeLine => _wholeLine;

        public bool TrimWhitespace => _trimWhitespace;

        public ConditionEvaluator Evaluator => _evaluator;

        // for comment / uncomment handling
        public string IfTokenFlagged => _ifTokenFlagged;

        public string ElseTokenFlagged => _elseTokenFlagged;

        public string ElseIfTokenFlagged => _elseIfTokenFlagged;

        public Conditional(string ifToken, string elseToken, string elseIfToken, string endIfToken, bool wholeLine, bool trimWhitespace, ConditionEvaluator evaluator)
            : this(ifToken, elseToken, elseIfToken, endIfToken, wholeLine, trimWhitespace, evaluator, null, null, null, null)
        {
            //_trimWhitespace = trimWhitespace;
            //_wholeLine = wholeLine;
            //_evaluator = evaluator;
            //_ifToken = ifToken;
            //_elseToken = elseToken;
            //_elseIfToken = elseIfToken;
            //_endIfToken = endIfToken;
        }

        public Conditional(string ifToken, string elseToken, string elseIfToken, string endIfToken, bool wholeLine, bool trimWhitespace, ConditionEvaluator evaluator, 
            string ifTokenFlagged, string elseTokenFlagged, string elseIfTokenFlagged, int? operationToDisable)
        {
            _trimWhitespace = trimWhitespace;
            _wholeLine = wholeLine;
            _evaluator = evaluator;
            _ifToken = ifToken;
            _elseToken = elseToken;
            _elseIfToken = elseIfToken;
            _endIfToken = endIfToken;
            _ifTokenFlagged = ifTokenFlagged;
            _elseTokenFlagged = elseTokenFlagged;
            _elseIfTokenFlagged = elseIfTokenFlagged;
            _operationToDisable = operationToDisable;
        }

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            byte[] ifToken = encoding.GetBytes(_ifToken);
            byte[] endToken = encoding.GetBytes(_endIfToken);

            List<byte[]> tokens = new List<byte[]>(ElseTokenFlaggedIndex + 1);
            //{
            //    ifToken,
            //    endToken
            //};
            for (int i = 0; i < tokens.Capacity; i++)
            {
                tokens.Add(null);
            }

            tokens[IfTokenIndex] = ifToken;
            tokens[EndTokenIndex] = endToken;

            TokenTrie trie = new TokenTrie();
            trie.AddToken(ifToken, IfTokenIndex);
            trie.AddToken(endToken, EndTokenIndex);

            //int elseIfTokenIndex = -1;

            if (!string.IsNullOrEmpty(_elseToken))
            {
                byte[] elseToken = encoding.GetBytes(_elseToken);
                //trie.AddToken(elseToken, tokens.Count);
                //tokens.Add(elseToken);
                trie.AddToken(elseToken, ElseTokenIndex);
                tokens[ElseTokenIndex] = elseToken;
            }

            if (!string.IsNullOrEmpty(_elseIfToken))
            {
                byte[] elseIfToken = encoding.GetBytes(_elseIfToken);
                //elseIfTokenIndex = tokens.Count;
                //trie.AddToken(elseIfToken, elseIfTokenIndex);
                //tokens.Add(elseIfToken);
                trie.AddToken(elseIfToken, ElseIfTokenIndex);
                tokens[ElseIfTokenIndex] = elseIfToken;
            }

            // setup the flagged versions
            // The ifTokenFlag must be defined for any to be defined
            // There is no reason to have a flagged end.
            //int elseIfTokenFlaggedIndex = -1;

            if (!string.IsNullOrEmpty(_ifTokenFlagged))
            {
                byte[] ifTokenFlagged = encoding.GetBytes(_ifTokenFlagged);
                tokens[IfTokenFlaggedIndex] = ifTokenFlagged;
                trie.AddToken(ifTokenFlagged, IfTokenFlaggedIndex);

                if (!string.IsNullOrEmpty(_elseIfTokenFlagged))
                {
                    byte[] elseIfTokenFlagged = encoding.GetBytes(_elseIfTokenFlagged);
                    tokens[ElseIfTokenFlaggedIndex] = elseIfTokenFlagged;
                    trie.AddToken(elseIfTokenFlagged, ElseIfTokenFlaggedIndex);
                }

                if (!string.IsNullOrEmpty(_elseTokenFlagged))
                {
                    byte[] elseTokenFlagged = encoding.GetBytes(_elseTokenFlagged);
                    tokens[ElseTokenFlaggedIndex] = elseTokenFlagged;
                    trie.AddToken(elseTokenFlagged, ElseTokenFlaggedIndex);
                }
            }

            // disable the flag operation if its defined
            if (_operationToDisable.HasValue)
            {
                string otherOptionDisableFlag = processorState.Config.OperationIdFlag(_operationToDisable.GetValueOrDefault());
                processorState.Config.Flags.Add(otherOptionDisableFlag, false);
            }

            //return new Impl(this, tokens, elseIfTokenIndex, trie);
            return new Impl(this, tokens, trie);
        }

        private class Impl : IOperation
        {
            private readonly Conditional _definition;
            private EvaluationState _current;
            //private readonly int _elseIfTokenIndex;
            private readonly Stack<EvaluationState> _pendingCompletion = new Stack<EvaluationState>();
            private readonly TokenTrie _trie;

            //public Impl(Conditional definition, IReadOnlyList<byte[]> tokens, int elseIfTokenIndex, TokenTrie trie)
            public Impl(Conditional definition, IReadOnlyList<byte[]> tokens, TokenTrie trie)
            {
                _trie = trie;
                //_elseIfTokenIndex = elseIfTokenIndex;
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

                // conditional has not started, or this is the "if"
                if (_current != null || token == IfTokenIndex || token == IfTokenFlaggedIndex)
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
                if (token == IfTokenIndex || token == IfTokenFlaggedIndex)
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

                        if (token == IfTokenFlaggedIndex)
                        {   // "flagged" if token, so enable the flag operation
                            // this may be wrong
                            _current.ToggleFlagOperation(true, processor);
                        }

                        // this is an endif return ???
                        return 0;
                    }

                    if (_definition.WholeLine)
                    {
                        processor.SeekForwardThrough(processor.EncodingConfig.LineEndings, ref bufferLength, ref currentBufferPosition);
                    }

                    SeekToTerminator(processor, ref bufferLength, ref currentBufferPosition, out token);

                    //Keep on scanning until we've hit a balancing token that belongs to us
                    while(token == IfTokenIndex || token == IfTokenFlaggedIndex)
                    {
                        int open = 1;
                        while(open != 0)
                        {
                            SeekToTerminator(processor, ref bufferLength, ref currentBufferPosition, out token);
                            if(token == IfTokenIndex || token == IfTokenFlaggedIndex)
                            {
                                ++open;
                            }
                            else if(token == EndTokenIndex)
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
                if (token == EndTokenIndex)
                {
                    if (_pendingCompletion.Count > 0)
                    {
                        _current = _pendingCompletion.Pop();
                        _current.ToggleFlagOperation(_current.FlagOperationEnabled, processor);
                    }
                    else
                    {
                        // disable the special case operation (note: it may already be disabled, but cheaper to do than check)
                        _current.ToggleFlagOperation(false, processor);
                        _current = null;
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
                    _current.ToggleFlagOperation(false, processor);
                    return 0;
                }

                //We have an "elseif" and haven't taken a previous branch
                if (token == ElseIfTokenIndex || token == ElseIfTokenFlaggedIndex)
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
                    else
                    {
                        if (token == ElseIfTokenFlaggedIndex)
                        {
                            // the elseif branch is taken. 
                            _current.ToggleFlagOperation(true, processor);
                        }
                    }

                    if (_definition.WholeLine)
                    {
                        processor.SeekForwardThrough(processor.EncodingConfig.LineEndings, ref bufferLength, ref currentBufferPosition);
                    }

                    // SCP 2016-08-15: The elseif branch may have been taken here. The original comment below is probably inaccurate.
                    //  if the elseif is not taken, and seek to terminator is true, we don't get here
                    //  if the elseif is taken, we get to here.
                    //
                    // Original comment:
                    //The "elseif" branch was not taken. Skip to the following else, elseif or endif token
                    return 0;
                }

                //We have an "else" token and haven't taken any other branches, return control
                //  after setting that a branch has been taken
                if (token == ElseTokenIndex || token == ElseTokenFlaggedIndex)
                {
                    if (token == ElseTokenFlaggedIndex)
                    {
                        _current.ToggleFlagOperation(true, processor);
                    }

                    _current.BranchTaken = true;
                    processor.WhitespaceHandler(ref bufferLength, ref currentBufferPosition, wholeLine: _definition._wholeLine, trim: _definition._trimWhitespace);
                    return 0;
                }
                else
                {
                    throw new InvalidDataException("Unknown token index: " + token);
                }
            }

            private void SkipToMatchingEndif(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, ref int token)
            {
                while (token != EndTokenIndex
                    && SeekToTerminator(processor, ref bufferLength, ref currentBufferPosition, out token) 
                    && token != EndTokenIndex)
                {
                    int balance = 1;

                    //We're in a nested if branch, wait until it balances closed
                    while (balance > 0 && SeekToTerminator(processor, ref bufferLength, ref currentBufferPosition, out token))
                    {
                        if (token == IfTokenIndex || token == IfTokenFlaggedIndex)
                        {
                            ++balance;
                        }
                        else if (token == EndTokenIndex)
                        {
                            --balance;
                        }
                    }
                }
            }

            private bool SeekToTerminator(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, out int token)
            {
                bool bufferAdvanceFailed = false;

                while (bufferLength >= _trie.MinLength)
                {
                    for (; currentBufferPosition < bufferLength - _trie.MinLength + 1; ++currentBufferPosition)
                    {
                        if (_trie.GetOperation(processor.CurrentBuffer, bufferLength, ref currentBufferPosition, out token))
                        {
                            if (bufferAdvanceFailed || (currentBufferPosition != bufferLength))
                            {
                                return true;
                            }
                        }
                    }

                    bufferAdvanceFailed = !processor.AdvanceBuffer(bufferLength - _trie.MaxLength + 1);
                    currentBufferPosition = processor.CurrentBufferPosition;
                    bufferLength = processor.CurrentBufferLength;
                }

                //If we run out of places to look, assert that the end of the buffer is the end
                token = EndTokenIndex;
                currentBufferPosition = bufferLength;
                return false;   // no terminator found
            }

            private class EvaluationState
            {
                private bool _branchTaken;
                private readonly Impl _impl;

                public EvaluationState(Impl impl)
                {
                    _impl = impl;
                    FlagOperationEnabled = false;
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

                public bool FlagOperationEnabled { get; private set; }

                public void ToggleFlagOperation(bool enabled, IProcessorState processor)
                {
                    FlagOperationEnabled = enabled;

                    if (_impl._definition._operationToDisable.HasValue)
                    {
                        string otherOptionDisableFlag = processor.Config.OperationIdFlag(_impl._definition._operationToDisable.GetValueOrDefault());

                        if (!processor.Config.Flags.ContainsKey(otherOptionDisableFlag))
                        {
                            processor.Config.Flags.Add(otherOptionDisableFlag, enabled);
                        }
                        else
                        {
                            processor.Config.Flags[otherOptionDisableFlag] = enabled;
                        }
                    }
                }
            }
        }
    }
}
