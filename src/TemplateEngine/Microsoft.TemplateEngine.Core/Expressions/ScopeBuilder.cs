// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Util;

namespace Microsoft.TemplateEngine.Core.Expressions
{
    public class ScopeBuilder<TOperator, TToken>
        where TOperator : struct
        where TToken : struct
    {
        private readonly ISet<TToken> _badSyntaxTokens;
        private readonly TToken _closeGroup;
        private readonly TOperator _identity;
        private readonly bool _isSymbolDereferenceInLiteralSequenceRequired;
        private readonly int _knownTokensCount;
        private readonly TToken _literal;
        private readonly ISet<TToken> _literalSequenceBoundsMarkers;
        private readonly ISet<TToken> _noops;
        private readonly TToken _openGroup;
        private readonly IReadOnlyDictionary<TOperator, Func<IEvaluable, IEvaluable>> _operatorScopeFactory;
        private readonly IProcessorState _processor;
        private readonly IReadOnlyList<object> _symbolValues;
        private readonly ISet<TToken> _terminators;
        private readonly ITokenTrie _tokens;
        private readonly IReadOnlyDictionary<TToken, TOperator> _tokenToOperatorMap;
        private readonly Func<string, string> _valueDecoder;
        private readonly Func<string, string> _valueEncoder;

        public ScopeBuilder(IProcessorState processor, ITokenTrie tokens, IOperatorMap<TOperator, TToken> operatorMap, bool dereferenceInLiterals)
        {
            TokenTrie trie = new TokenTrie();
            trie.Append(tokens);

            _badSyntaxTokens = operatorMap.BadSyntaxTokens;
            _noops = operatorMap.NoOpTokens;
            _openGroup = operatorMap.OpenGroupToken;
            _closeGroup = operatorMap.CloseGroupToken;
            _literal = operatorMap.LiteralToken;
            _identity = operatorMap.Identity;
            _literalSequenceBoundsMarkers = operatorMap.LiteralSequenceBoundsMarkers;
            _terminators = operatorMap.Terminators;
            _isSymbolDereferenceInLiteralSequenceRequired = dereferenceInLiterals;
            _processor = processor;
            _knownTokensCount = tokens.Count;
            _valueEncoder = operatorMap.Encode;
            _valueDecoder = operatorMap.Decode;

            List<object> symbolValues = new List<object>();

            foreach (KeyValuePair<string, object> variable in processor.Config.Variables)
            {
                trie.AddToken(processor.Encoding.GetBytes(string.Format(processor.Config.VariableFormatString, variable.Key)));
                symbolValues.Add(variable.Value);
            }

            _symbolValues = symbolValues;
            _tokenToOperatorMap = operatorMap.TokensToOperatorsMap;
            _operatorScopeFactory = operatorMap.OperatorScopeLookupFactory;
            _tokens = trie;
        }

        public IEvaluable Build(ref int bufferLength, ref int bufferPosition, Action<IReadOnlyList<byte>> onFault)
        {
            Stack<ScopeIsolator> parents = new Stack<ScopeIsolator>();
            ScopeIsolator isolator = new ScopeIsolator
            {
                Root = new UnaryScope<TOperator>(null, _identity, o => o)
            };
            isolator.Active = isolator.Root;
            TToken? activeLiteralSequenceBoundsMarker = null;
            List<byte> currentLiteral = new List<byte>();
            List<byte> allData = new List<byte>();

            while (bufferLength > 0)
            {
                int targetLen = Math.Min(bufferLength, _tokens.MaxLength);
                for (; bufferPosition < bufferLength - targetLen + 1;)
                {
                    int oldBufferPos = bufferPosition;
                    if (_tokens.GetOperation(_processor.CurrentBuffer, bufferLength, ref bufferPosition, out int token))
                    {
                        allData.AddRange(_tokens.Tokens[token].Value);
                        TToken mappedToken = (TToken)(object)token;

                        if (_badSyntaxTokens.Contains(mappedToken))
                        {
                            onFault(allData);
                            return null;
                        }

                        //Hit a terminator? Return the root
                        if (_terminators.Contains(mappedToken))
                        {
                            bufferPosition = oldBufferPos;

                            //If we had an active literal, it has to be over now - all literal types have already been processed
                            if (currentLiteral.Count > 0)
                            {
                                string value = _processor.Encoding.GetString(currentLiteral.ToArray());
                                value = _valueDecoder(value);
                                currentLiteral.Clear();
                                Token<TToken> t = new Token<TToken>(_literal, value);
                                TokenScope<TToken> scope = new TokenScope<TToken>(isolator.Active, t);

                                while (isolator.Active != null && !isolator.Active.TryAccept(scope))
                                {
                                    isolator.Active = isolator.Active.Parent;
                                }

                                if (isolator.Active == null)
                                {
                                    onFault(allData);
                                    return null;
                                }
                            }

                            return isolator.Root;
                        }

                        //Start or end of a literal sequence (string)?
                        if (_literalSequenceBoundsMarkers.Contains(mappedToken))
                        {
                            //Don't add the literal start/end, otherwise we'll have to deal with them
                            //  in strings, guessing whether they're supposed to be there or not

                            if (activeLiteralSequenceBoundsMarker.HasValue)
                            {
                                if (Equals(activeLiteralSequenceBoundsMarker.Value, mappedToken))
                                {
                                    activeLiteralSequenceBoundsMarker = null;
                                    string value = _processor.Encoding.GetString(currentLiteral.ToArray());
                                    value = _valueDecoder(value);
                                    currentLiteral.Clear();
                                    Token<TToken> t = new Token<TToken>(_literal, value);
                                    TokenScope<TToken> scope = new TokenScope<TToken>(isolator.Active, t)
                                    {
                                        IsQuoted = true
                                    };

                                    while (isolator.Active != null && !isolator.Active.TryAccept(scope))
                                    {
                                        isolator.Active = isolator.Active.Parent;
                                    }

                                    if (isolator.Active == null)
                                    {
                                        onFault(allData);
                                        return null;
                                    }
                                }
                            }
                            else
                            {
                                activeLiteralSequenceBoundsMarker = mappedToken;
                            }
                        }
                        //In a literal sequence (string)?
                        else if (activeLiteralSequenceBoundsMarker.HasValue)
                        {
                            //Have a symbol & dereferencing in literal sequences is on?
                            if (_knownTokensCount <= token && _isSymbolDereferenceInLiteralSequenceRequired)
                            {
                                object val = _symbolValues[token - _knownTokensCount];
                                string valText = (val ?? "null").ToString();
                                valText = _valueEncoder(valText);
                                byte[] data = _processor.Encoding.GetBytes(valText);
                                currentLiteral.AddRange(data);
                            }
                            else
                            {
                                currentLiteral.AddRange(_tokens.Tokens[token].Value);
                            }
                        }
                        else
                        {
                            //If we had an active literal, it has to be over now - all literal types have already been processed
                            if (currentLiteral.Count > 0)
                            {
                                activeLiteralSequenceBoundsMarker = null;
                                string value = _processor.Encoding.GetString(currentLiteral.ToArray());
                                value = _valueDecoder(value);
                                currentLiteral.Clear();
                                Token<TToken> t = new Token<TToken>(_literal, value);
                                TokenScope<TToken> scope = new TokenScope<TToken>(isolator.Active, t);

                                while (isolator.Active != null && !isolator.Active.TryAccept(scope))
                                {
                                    isolator.Active = isolator.Active.Parent;
                                }

                                if (isolator.Active == null)
                                {
                                    onFault(allData);
                                    return null;
                                }
                            }

                            //Start of a group?
                            if (Equals(_openGroup, mappedToken))
                            {
                                parents.Push(isolator);
                                isolator = new ScopeIsolator
                                {
                                    Root = new UnaryScope<TOperator>(null, _identity, o => o)
                                };
                                isolator.Active = isolator.Root;
                            }
                            //End of a group?
                            else if (Equals(_closeGroup, mappedToken))
                            {
                                ScopeIsolator tmp = parents.Pop();
                                tmp.Active.TryAccept(isolator.Root);
                                isolator.Root = tmp.Active;
                                isolator = tmp;
                            }
                            //Is it a variable?
                            else if (_knownTokensCount <= token)
                            {
                                string value = (_symbolValues[token - _knownTokensCount] ?? "null").ToString();
                                Token<TToken> t = new Token<TToken>(_literal, value);
                                TokenScope<TToken> scope = new TokenScope<TToken>(isolator.Active, t);

                                while (isolator.Active != null && !isolator.Active.TryAccept(scope))
                                {
                                    isolator.Active = isolator.Active.Parent;
                                }

                                if (isolator.Active == null)
                                {
                                    onFault(allData);
                                    return null;
                                }
                            }
                            //Discardable tokens?
                            else if (_noops.Contains(mappedToken))
                            {
                            }
                            //All the special possibilities have been exhausted, try to process operations
                            else
                            {
                                //We got a token we understand, but it's not an operator
                                if (_tokenToOperatorMap.TryGetValue(mappedToken, out TOperator op))
                                {
                                    if (_operatorScopeFactory.TryGetValue(op, out Func<IEvaluable, IEvaluable> factory))
                                    {
                                        IEvaluable oldActive = isolator.Active;
                                        isolator.Active = factory(isolator.Active);

                                        if (oldActive.Parent == isolator.Active
                                            && (oldActive == isolator.Root
                                                || (isolator.Root is UnaryScope<TOperator> o
                                                    && Equals(o.Operator, _identity)
                                                    && o.Operand == oldActive)))
                                        {
                                            isolator.Root = isolator.Active;
                                        }
                                    }
                                }
                            }
                        }
                    }
                    //If we've encountered a literal after fully filling the tree, return
                    else if (isolator.Active.IsFull)
                    {
                        IEvaluable parent = isolator.Active.Parent;

                        while (parent != null && parent.IsFull)
                        {
                            parent = parent.Parent;
                        }

                        if (parent == null)
                        {
                            return isolator.Root;
                        }
                    }
                    else
                    {
                        allData.Add(_processor.CurrentBuffer[bufferPosition]);
                        currentLiteral.Add(_processor.CurrentBuffer[bufferPosition]);
                        ++bufferPosition;
                    }
                }

                _processor.AdvanceBuffer(bufferPosition);
                bufferPosition = _processor.CurrentBufferPosition;
                bufferLength = _processor.CurrentBufferLength;
            }

            //If we had an active literal, it has to be over now - all literal types have already been processed
            if (currentLiteral.Count > 0)
            {
                string value = _processor.Encoding.GetString(currentLiteral.ToArray());
                value = _valueDecoder(value);
                currentLiteral.Clear();
                Token<TToken> t = new Token<TToken>(_literal, value);
                TokenScope<TToken> scope = new TokenScope<TToken>(isolator.Active, t);

                while (isolator.Active != null && !isolator.Active.TryAccept(scope))
                {
                    isolator.Active = isolator.Active.Parent;
                }

                if (isolator.Active == null)
                {
                    onFault(allData);
                    return null;
                }
            }

            return isolator.Root;
        }

        private class ScopeIsolator
        {
            public IEvaluable Active { get; set; }

            public IEvaluable Root { get; set; }
        }
    }
}

