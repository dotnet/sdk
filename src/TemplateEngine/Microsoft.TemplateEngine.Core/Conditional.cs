using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions.Engine;
using System;
using System.Runtime.CompilerServices;

namespace Microsoft.TemplateEngine.Core
{
    public class Conditional : IOperationProvider
    {
        private readonly ConditionEvaluator _evaluator;
        private readonly bool _wholeLine;
        private readonly bool _trimWhitespace;

        private readonly IList<string> _ifTokens = new List<string>();
        private readonly IList<string> _elseIfTokens = new List<string>();
        private readonly IList<string> _elseTokens = new List<string>();
        private readonly IList<string> _endIfTokens = new List<string>();

        private readonly IList<string> _ifTokensActionable = new List<string>();
        private readonly IList<string> _elseIfTokensActionable = new List<string>();
        private readonly IList<string> _elseTokensActionable = new List<string>();

        private readonly IList<string> _actionableOnlyTokens = new List<string>();

        // the unusual order of these is historical, no special meaning
        // if actual_token_index % 10 == baseTokenIndex
        // then actual_token_index is of the baseTokenIndex type
        // these are now "Base indexes"
        private const int IfTokenBaseIndex = 0;
        private const int EndTokenBaseIndex = 1;
        private const int ElseIfTokenBaseIndex = 2;
        private const int ElseTokenBaseIndex = 3;

        private const int IfTokenActionableBaseIndex = 4;
        private const int ElseIfTokenActionableBaseIndex = 5;
        private const int ElseTokenActionableBaseIndex = 6;

        // must be > the highest token type index
        private const int TokenTypeModulus = 10;

        // The other operations to toggle when the actionable tokens get handled
        // They're disabled during initial setup of conditional.
        // When the actionable token start is encountered, these are enabled.
        // When the actionable token end is encountered, they're disabled.
        //  it gets a bit more complex with embedded actionables.
        private readonly IList<int> _actionableOperations;

        public bool WholeLine => _wholeLine;

        public bool TrimWhitespace => _trimWhitespace;

        public ConditionEvaluator Evaluator => _evaluator;

        // standard versions of the tokens.
        public IList<string> IfTokens => _ifTokens;

        public IList<string> ElseIfTokens => _elseIfTokens;

        public IList<string> ElseTokens => _elseTokens;

        public IList<string> EndIfTokens => _endIfTokens;

        // for comment / uncomment handling
        public IList<string> IfTokensActionable => _ifTokensActionable;

        public IList<string> ElseTokensActionable => _elseTokensActionable;

        public IList<string> ElseIfTokensActionable => _elseIfTokensActionable;

        // lists of regular conditionals
        // no special conditionals
        public Conditional(IEnumerable<string> ifTokens, IEnumerable<string> elseTokens, IEnumerable<string> elseIfTokens, IEnumerable<string> endIfTokens,
                bool wholeLine, bool trimWhitespace, ConditionEvaluator evaluator)
            :this(ifTokens, elseTokens, elseIfTokens, endIfTokens, wholeLine, trimWhitespace, evaluator, new List<string>(), new List<string>(), new List<string>(), new List<int>())
        {
        }

        // lists of regular conditionals
        // lists of specials conditionals
        // single operation to toggle
        public Conditional(IEnumerable<string> ifTokens, IEnumerable<string> elseTokens, IEnumerable<string> elseIfTokens, IEnumerable<string> endIfTokens,
            bool wholeLine, bool trimWhitespace, ConditionEvaluator evaluator,
            IEnumerable<string> ifTokensActionable, IEnumerable<string> elseTokensActionable, IEnumerable<string> elseIfTokensActionable, IList<int> actionableOperations)
        {
            _trimWhitespace = trimWhitespace;
            _wholeLine = wholeLine;
            _evaluator = evaluator;

            _ifTokens = new List<string>(ifTokens);
            _elseTokens = new List<string>(elseTokens);
            _elseIfTokens = new List<string>(elseIfTokens);
            _endIfTokens = new List<string>(endIfTokens);
            _ifTokensActionable = new List<string>(ifTokensActionable);
            _elseTokensActionable = new List<string>(elseTokensActionable);
            _elseIfTokensActionable = new List<string>(elseIfTokensActionable);

            _actionableOperations = actionableOperations;
        }

        // Probably need to hold onto these original constructors for backwards compatibility as this is developed.
        //
        // single regular conditionals
        // no special conditionals
        public Conditional(string ifToken, string elseToken, string elseIfToken, string endIfToken, bool wholeLine, bool trimWhitespace, ConditionEvaluator evaluator)
            : this(new List<string>() { ifToken },
                  new List<string>() { elseToken },
                  new List<string>() { elseIfToken },
                  new List<string>() { endIfToken },
                  wholeLine,
                  trimWhitespace,
                  evaluator,
                  new List<string>(),
                  new List<string>(),
                  new List<string>(),
                  new List<int>())
        {
        }

        // single regular conditionals
        // single special conditionals
        // single operation to toggle
        public Conditional(string ifToken, string elseToken, string elseIfToken, string endIfToken, bool wholeLine, bool trimWhitespace, ConditionEvaluator evaluator,
            string ifTokenActionable, string elseTokenActionable, string elseIfTokenActionable, IList<int> actionableOperations)
            : this(new List<string>() { ifToken },
                  new List<string>() { elseToken },
                  new List<string>() { elseIfToken },
                  new List<string>() { endIfToken },
                  wholeLine,
                  trimWhitespace,
                  evaluator,
                  new List<string>() { ifTokenActionable },
                  new List<string>() { elseTokenActionable },
                  new List<string>() { elseIfTokenActionable },
                  actionableOperations)
        {
        }

        /// <summary>
        /// Returns the numner of elements in the longest of the token variant lists.
        /// </summary>
        private int LongestTokenVariantListSize
        {
            get
            {
                int maxListSize = Math.Max(_ifTokens.Count, _elseTokens.Count);
                maxListSize = Math.Max(maxListSize, _elseIfTokens.Count);
                maxListSize = Math.Max(maxListSize, _endIfTokens.Count);
                maxListSize = Math.Max(maxListSize, _ifTokensActionable.Count);
                maxListSize = Math.Max(maxListSize, _elseTokensActionable.Count);
                maxListSize = Math.Max(maxListSize, _elseIfTokensActionable.Count);

                return maxListSize;
            }
        }

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            TokenTrie trie = new TokenTrie();

            List<byte[]> tokens = new List<byte[]>(TokenTypeModulus * LongestTokenVariantListSize);
            for (int i = 0; i < tokens.Capacity; i++)
            {
                tokens.Add(null);
            }

            AddTokensOfTypeToTokenListAndTrie(trie, tokens, _ifTokens, IfTokenBaseIndex, encoding);
            AddTokensOfTypeToTokenListAndTrie(trie, tokens, _elseTokens, ElseTokenBaseIndex, encoding);
            AddTokensOfTypeToTokenListAndTrie(trie, tokens, _elseIfTokens, ElseIfTokenBaseIndex, encoding);
            AddTokensOfTypeToTokenListAndTrie(trie, tokens, _endIfTokens, EndTokenBaseIndex, encoding);
            AddTokensOfTypeToTokenListAndTrie(trie, tokens, _ifTokensActionable, IfTokenActionableBaseIndex, encoding);
            AddTokensOfTypeToTokenListAndTrie(trie, tokens, _elseTokensActionable, ElseTokenActionableBaseIndex, encoding);
            AddTokensOfTypeToTokenListAndTrie(trie, tokens, _elseIfTokensActionable, ElseIfTokenActionableBaseIndex, encoding);

            // disable the actionable operations if there are any
            foreach (int operationId in _actionableOperations)
            {
                string otherOptionDisableFlag = processorState.Config.OperationIdFlag(operationId);
                processorState.Config.Flags.Add(otherOptionDisableFlag, false);
            }

            return new Impl(this, tokens, trie);
        }

        /// <summary>
        /// Puts the tokensOfType into the tokenMasterList at indexes which are congruent to typeRemainder mod TokenTypeModulus
        /// </summary>
        /// <param name="trie"></param>
        /// <param name="tokenMasterList"></param>
        /// <param name="tokensOfType"></param>
        /// <param name="typeRemainder"></param>
        /// <param name="encoding"></param>
        private void AddTokensOfTypeToTokenListAndTrie(TokenTrie trie, List<byte[]> tokenMasterList, IList<string> tokensOfType, int typeRemainder, Encoding encoding)
        {
            int tokenIndex = typeRemainder;

            for (int i = 0; i < tokensOfType.Count; i++)
            {
                byte[] byteToken = encoding.GetBytes(tokensOfType[i]);
                tokenMasterList[tokenIndex] = byteToken;
                trie.AddToken(byteToken, typeRemainder);
                tokenIndex += TokenTypeModulus;
            }
        }

        /// <summary>
        /// Returns true if the tokenIndex indicates the token is a variant of its base type,
        /// false otherwise.
        /// </summary>
        /// <param name="tokenIndex"></param>
        /// <param name="baseTypeIndex"></param>
        /// <returns></returns>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        private static bool IsTokenIndexOfType(int tokenIndex, int baseTypeIndex)
        {
            return (tokenIndex % TokenTypeModulus) == baseTypeIndex;
        }

        private class Impl : IOperation
        {
            private readonly Conditional _definition;
            private EvaluationState _current;
            private readonly Stack<EvaluationState> _pendingCompletion = new Stack<EvaluationState>();
            private readonly TokenTrie _trie;

            public Impl(Conditional definition, IReadOnlyList<byte[]> tokens, TokenTrie trie)
            {
                _trie = trie;
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
                if (_current != null || IsTokenIndexOfType(token, IfTokenBaseIndex) || IsTokenIndexOfType(token, IfTokenActionableBaseIndex))
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
                if (IsTokenIndexOfType(token, IfTokenBaseIndex) || IsTokenIndexOfType(token, IfTokenActionableBaseIndex))
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

                        if (IsTokenIndexOfType(token, IfTokenActionableBaseIndex))
                        {   // "Actionable" if token, so enable the flag operation
                            _current.ToggleActionableOperations(true, processor);
                        }

                        // this is an endif return ???
                        return 0;
                    }
                    else
                    {
                        SeekToNextTokenAtSameLevel(processor, ref bufferLength, ref currentBufferPosition, out token);
                        goto BEGIN;
                    }
                }

                //If we've got an unbalanced statement, emit the token
                if (_current == null)
                {
                    byte[] tokenValue = Tokens[token];
                    target.Write(tokenValue, 0, tokenValue.Length);
                    return tokenValue.Length;
                }

                //Got the endif token, exit to the parent "if" scope if it exists
                if (IsTokenIndexOfType(token, EndTokenBaseIndex))
                {
                    if (_pendingCompletion.Count > 0)
                    {
                        _current = _pendingCompletion.Pop();
                        _current.ToggleActionableOperations(_current.ActionableOperationsEnabled, processor);
                    }
                    else
                    {
                        // disable the special case operation (note: it may already be disabled, but cheaper to do than check)
                        _current.ToggleActionableOperations(false, processor);
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
                    // NOTE: this can probably use the new method SeekToNextTokenAtSameLevel() - they do almost the same thing.
                    SkipToMatchingEndif(processor, ref bufferLength, ref currentBufferPosition, ref token);

                    if (_pendingCompletion.Count > 0)
                    {
                        _current = _pendingCompletion.Pop();
                        _current.ToggleActionableOperations(_current.ActionableOperationsEnabled, processor);
                    }
                    else
                    {
                        // disable the special case operation (note: it may already be disabled, but cheaper to do than check)
                        _current.ToggleActionableOperations(false, processor);
                        _current = null;
                    }

                    return 0;
                }

                //We have an "elseif" and haven't taken a previous branch
                if (IsTokenIndexOfType(token, ElseIfTokenBaseIndex) || IsTokenIndexOfType(token, ElseIfTokenActionableBaseIndex))
                {
                    // 8-19 attempt to make the same as if() handling
                    //
                    if (_current.Evaluate(processor, ref bufferLength, ref currentBufferPosition))
                    {
                        if (_definition.WholeLine)
                        {
                            processor.SeekForwardThrough(processor.EncodingConfig.LineEndings, ref bufferLength, ref currentBufferPosition);
                        }

                        if (IsTokenIndexOfType(token, ElseIfTokenActionableBaseIndex))
                        {
                            // the elseif branch is taken. 
                            _current.ToggleActionableOperations(true, processor);
                        }

                        return 0;
                    }
                    else
                    {
                        SeekToNextTokenAtSameLevel(processor, ref bufferLength, ref currentBufferPosition, out token);

                        // In the original version this was conditional on SeekToToken() succeeding.
                        // Not sure if it should be conditional. It should never fail, unless the template is malformed.
                        goto BEGIN;
                    }
                }

                //We have an "else" token and haven't taken any other branches, return control
                //  after setting that a branch has been taken
                if (IsTokenIndexOfType(token, ElseTokenBaseIndex) || IsTokenIndexOfType(token, ElseTokenActionableBaseIndex))
                {
                    if (IsTokenIndexOfType(token, ElseTokenActionableBaseIndex))
                    {
                        _current.ToggleActionableOperations(true, processor);
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

            // moves the buffer to the next token at the same level.
            // Returns false if no end token can be found at the same level. 
            //      this is probably indicative of a template authoring problem, or possibly a buffer problem.
            private bool SkipToMatchingEndif(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, ref int token)
            {
                while (!IsTokenIndexOfType(token, EndTokenBaseIndex))
                {
                    bool seekSucceeded = SeekToNextTokenAtSameLevel(processor, ref bufferLength, ref currentBufferPosition, out token);

                    if (! seekSucceeded)
                    {
                        return false;
                    }
                }

                return true;
            }

            // Moves the buffer to the next token at the same level of nesting as the current token.
            // Should never be called if we're on an end token!!!
            // Returns false if no next token can be found at the same level. 
            //      this is probably indicative of a template authoring problem, or possibly a buffer problem.
            private bool SeekToNextTokenAtSameLevel(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, out int token)
            {
                if (_definition.WholeLine)
                {
                    processor.SeekForwardThrough(processor.EncodingConfig.LineEndings, ref bufferLength, ref currentBufferPosition);
                }

                bool seekSucceeded = SeekToToken(processor, ref bufferLength, ref currentBufferPosition, out token);

                //Keep on scanning until we've hit a balancing token that belongs to us
                // each "if" found opens a new level of nesting
                while (IsTokenIndexOfType(token, IfTokenBaseIndex) || IsTokenIndexOfType(token, IfTokenActionableBaseIndex))
                {
                    int open = 1;
                    while (open != 0)
                    {
                        seekSucceeded &= SeekToToken(processor, ref bufferLength, ref currentBufferPosition, out token);

                        if (IsTokenIndexOfType(token, IfTokenBaseIndex) || IsTokenIndexOfType(token, IfTokenActionableBaseIndex))
                        {
                            ++open;
                        }
                        else if (IsTokenIndexOfType(token, EndTokenBaseIndex))
                        {
                            --open;
                        }
                    }

                    seekSucceeded &= SeekToToken(processor, ref bufferLength, ref currentBufferPosition, out token);
                }

                // temporary for debugging.
                if (! seekSucceeded)
                {
                    Console.WriteLine("seek to token failed in SeekToNextBalancedToken()");
                }

                // this may be irrelevant. If it happens, the template is malformed (i think)
                return seekSucceeded;
            }

            // moves to the next token
            // returns false if the end of the buffer was reached without finding a token.
            private bool SeekToToken(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, out int token)
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
                token = EndTokenBaseIndex;
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
                    ActionableOperationsEnabled = false;
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

                public bool ActionableOperationsEnabled { get; private set; }

                public void ToggleActionableOperations(bool enabled, IProcessorState processor)
                {
                    ActionableOperationsEnabled = enabled;

                    foreach (int operationId in _impl._definition._actionableOperations)
                    {
                        string otherOptionDisableFlag = processor.Config.OperationIdFlag(operationId);

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
