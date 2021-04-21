// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Util;

namespace Microsoft.TemplateEngine.Core.Operations
{
    public class Conditional : IOperationProvider
    {
        public static readonly string OperationName = "conditional";

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

        private readonly ConditionEvaluator _evaluator;
        private readonly bool _wholeLine;
        private readonly bool _trimWhitespace;
        private readonly ConditionalTokens _tokens;
        private readonly string _id;
        private bool _initialState;

        public Conditional(ConditionalTokens tokenVariants, bool wholeLine, bool trimWhitespace, ConditionEvaluator evaluator, string id, bool initialState)
        {
            _trimWhitespace = trimWhitespace;
            _wholeLine = wholeLine;
            _evaluator = evaluator;
            _tokens = tokenVariants;
            _id = id;
            _initialState = initialState;
        }

        public string Id => _id;

        public bool WholeLine => _wholeLine;

        public bool TrimWhitespace => _trimWhitespace;

        public ConditionEvaluator Evaluator => _evaluator;

        public ConditionalTokens Tokens => _tokens;

        /// <summary>
        /// Returns the numner of elements in the longest of the token variant lists.
        /// </summary>
        private int LongestTokenVariantListSize
        {
            get
            {
                int maxListSize = Math.Max(Tokens.IfTokens.Count, Tokens.ElseTokens.Count);
                maxListSize = Math.Max(maxListSize, Tokens.ElseIfTokens.Count);
                maxListSize = Math.Max(maxListSize, Tokens.EndIfTokens.Count);
                maxListSize = Math.Max(maxListSize, Tokens.ActionableIfTokens.Count);
                maxListSize = Math.Max(maxListSize, Tokens.ActionableElseTokens.Count);
                maxListSize = Math.Max(maxListSize, Tokens.ActionableElseIfTokens.Count);

                return maxListSize;
            }
        }

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            TokenTrie trie = new TokenTrie();

            List<IToken> tokens = new List<IToken>(TokenTypeModulus * LongestTokenVariantListSize);
            for (int i = 0; i < tokens.Capacity; i++)
            {
                tokens.Add(null);
            }

            AddTokensOfTypeToTokenListAndTrie(trie, tokens, Tokens.IfTokens, IfTokenBaseIndex, encoding);
            AddTokensOfTypeToTokenListAndTrie(trie, tokens, Tokens.ElseTokens, ElseTokenBaseIndex, encoding);
            AddTokensOfTypeToTokenListAndTrie(trie, tokens, Tokens.ElseIfTokens, ElseIfTokenBaseIndex, encoding);
            AddTokensOfTypeToTokenListAndTrie(trie, tokens, Tokens.EndIfTokens, EndTokenBaseIndex, encoding);
            AddTokensOfTypeToTokenListAndTrie(trie, tokens, Tokens.ActionableIfTokens, IfTokenActionableBaseIndex, encoding);
            AddTokensOfTypeToTokenListAndTrie(trie, tokens, Tokens.ActionableElseTokens, ElseTokenActionableBaseIndex, encoding);
            AddTokensOfTypeToTokenListAndTrie(trie, tokens, Tokens.ActionableElseIfTokens, ElseIfTokenActionableBaseIndex, encoding);

            return new Impl(this, tokens, trie, _id, _initialState);
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

        /// <summary>
        /// Puts the tokensOfType into the tokenMasterList at indexes which are congruent to typeRemainder mod TokenTypeModulus.
        /// </summary>
        /// <param name="trie"></param>
        /// <param name="tokenMasterList"></param>
        /// <param name="tokensOfType"></param>
        /// <param name="typeRemainder"></param>
        /// <param name="encoding"></param>
        private void AddTokensOfTypeToTokenListAndTrie(ITokenTrie trie, List<IToken> tokenMasterList, IReadOnlyList<ITokenConfig> tokensOfType, int typeRemainder, Encoding encoding)
        {
            int tokenIndex = typeRemainder;

            for (int i = 0; i < tokensOfType.Count; i++)
            {
                tokenMasterList[tokenIndex] = tokensOfType[i].ToToken(encoding);
                trie.AddToken(tokenMasterList[tokenIndex], typeRemainder);
                tokenIndex += TokenTypeModulus;
            }
        }

        private class Impl : IOperation
        {
            private readonly Conditional _definition;
            private readonly Stack<EvaluationState> _pendingCompletion = new Stack<EvaluationState>();
            private readonly ITokenTrie _trie;
            private readonly string _id;
            private EvaluationState _current;

            public Impl(Conditional definition, IReadOnlyList<IToken> tokens, ITokenTrie trie, string id, bool initialState)
            {
                _trie = trie;
                _definition = definition;
                Tokens = tokens;
                _id = id;
                IsInitialStateOn = string.IsNullOrEmpty(id) || initialState;
            }

            public string Id => _id;

            public IReadOnlyList<IToken> Tokens { get; }

            public bool IsInitialStateOn { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                bool flag;
                if (processor.Config.Flags.TryGetValue(OperationName, out flag) && !flag)
                {
                    target.Write(Tokens[token].Value, Tokens[token].Start, Tokens[token].Length);
                    return Tokens[token].Length;
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
                        {
                            // "Actionable" if token, so enable the flag operation(s)
                            _current.ToggleActionableOperations(true, processor);
                        }

                        // if (true_condition) was found.
                        return 0;
                    }
                    else
                    {
                        // if (false_condition) was found. Skip to the next token of the if-elseif-elseif-...elseif-else-endif
                        SeekToNextTokenAtSameLevel(processor, ref bufferLength, ref currentBufferPosition, out token);
                        goto BEGIN;
                    }
                }

                // If we've got an unbalanced statement, emit the token
                if (_current == null)
                {
                    target.Write(Tokens[token].Value, Tokens[token].Start, Tokens[token].Length);
                    return Tokens[token].Length;
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
                        // disable the special case operations (note: they may already be disabled, but cheaper to do than check)
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

                    if (_definition._wholeLine)
                    {
                        processor.SeekForwardUntil(processor.EncodingConfig.LineEndings, ref bufferLength, ref currentBufferPosition);
                    }
                    else if (_definition._trimWhitespace)
                    {
                        processor.TrimWhitespace(true, false, ref bufferLength, ref currentBufferPosition);
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
                    Debug.Assert(true, "Unknown token index: " + token);
                    return 0;   // TODO: revisit. Not sure what's best here.
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

                    if (!seekSucceeded)
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

                // this may be irrelevant. If it happens, the template is malformed (i think)
                return seekSucceeded;
            }

            // moves to the next token
            // returns false if the end of the buffer was reached without finding a token.
            private bool SeekToToken(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition, out int token)
            {
                bool bufferAdvanceFailed = false;
                ITokenTrieEvaluator evaluator = _trie.CreateEvaluator();

                while (true)
                {
                    for (; currentBufferPosition < bufferLength; ++currentBufferPosition)
                    {
                        if (evaluator.Accept(processor.CurrentBuffer[currentBufferPosition], ref currentBufferPosition, out token))
                        {
                            if (bufferAdvanceFailed || (currentBufferPosition != bufferLength))
                            {
                                return true;
                            }
                        }
                    }

                    if (bufferAdvanceFailed)
                    {
                        if (evaluator.TryFinalizeMatchesInProgress(ref currentBufferPosition, out token))
                        {
                            return true;
                        }

                        break;
                    }

                    bufferAdvanceFailed = !processor.AdvanceBuffer(bufferLength - evaluator.BytesToKeepInBuffer);
                    currentBufferPosition = evaluator.BytesToKeepInBuffer;
                    bufferLength = processor.CurrentBufferLength;
                }

                //If we run out of places to look, assert that the end of the buffer is the end
                token = EndTokenBaseIndex;
                currentBufferPosition = bufferLength;
                return false;   // no terminator found
            }

            private class EvaluationState
            {
                private readonly Impl _impl;
                private bool _branchTaken;

                public EvaluationState(Impl impl)
                {
                    _impl = impl;
                    ActionableOperationsEnabled = false;
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

                    foreach (string otherOptionDisableFlag in _impl._definition.Tokens.ActionableOperations)
                    {
                        processor.Config.Flags[otherOptionDisableFlag] = enabled;
                    }
                }

                internal bool Evaluate(IProcessorState processor, ref int bufferLength, ref int currentBufferPosition)
                {
                    bool faulted;
                    BranchTaken = _impl._definition._evaluator(processor, ref bufferLength, ref currentBufferPosition, out faulted);
                    return BranchTaken;
                }
            }
        }
    }
}
