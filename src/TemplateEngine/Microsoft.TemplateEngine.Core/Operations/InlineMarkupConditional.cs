// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Util;

namespace Microsoft.TemplateEngine.Core.Operations
{
    public class InlineMarkupConditional : IOperationProvider
    {
        private readonly string _id;
        private readonly bool _initialState;

        public InlineMarkupConditional(MarkupTokens tokens, bool wholeLine, bool trimWhitespace, ConditionEvaluator evaluator, string variableFormat, string id, bool initialState)
        {
            Tokens = tokens;
            _id = id;
            Evaluator = evaluator;
            WholeLine = wholeLine;
            TrimWhitespace = trimWhitespace;
            VariableFormat = variableFormat;
            _initialState = initialState;
        }

        public ConditionEvaluator Evaluator { get; }

        public string Id => _id;

        public MarkupTokens Tokens { get; }

        public bool TrimWhitespace { get; }

        public string VariableFormat { get; }

        public bool WholeLine { get; }

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            TokenTrie structureTrie = new TokenTrie();
            TokenTrie closeConditionTrie = new TokenTrie();
            TokenTrie scanBackTrie = new TokenTrie();

            IToken openOpenElementTokenBytes = Tokens.OpenOpenElementToken.ToToken(processorState.Encoding);
            scanBackTrie.AddToken(openOpenElementTokenBytes);
            int openOpenElementToken = structureTrie.AddToken(openOpenElementTokenBytes);
            int openCloseElementToken = structureTrie.AddToken(Tokens.OpenCloseElementToken.ToToken(processorState.Encoding));
            int closeCloseElementToken = structureTrie.AddToken(Tokens.CloseElementTagToken.ToToken(processorState.Encoding));

            int selfClosingElementEndToken = -1;
            if (Tokens.SelfClosingElementEndToken != null)
            {
                selfClosingElementEndToken = structureTrie.AddToken(Tokens.SelfClosingElementEndToken.ToToken(processorState.Encoding));
            }

            closeConditionTrie.AddToken(Tokens.CloseConditionExpression.ToToken(processorState.Encoding));
            MarkupTokenMapping mapping = new MarkupTokenMapping(
                openOpenElementToken,
                openCloseElementToken,
                closeCloseElementToken,
                selfClosingElementEndToken
            );

            IReadOnlyList<IToken> start = new[] { Tokens.OpenConditionExpression.ToToken(processorState.Encoding) };
            return new Impl(this, start, structureTrie, closeConditionTrie, scanBackTrie, mapping, _id, _initialState);
        }

        public class Impl : IOperation
        {
            private readonly ITokenTrie _closeConditionTrie;
            private readonly InlineMarkupConditional _definition;
            private readonly MarkupTokenMapping _mapping;
            private readonly ITokenTrie _scanBackTrie;
            private readonly ITokenTrie _structureTrie;

            public Impl(InlineMarkupConditional definition, IReadOnlyList<IToken> tokens, ITokenTrie structureTrie, ITokenTrie closeConditionTrie, ITokenTrie scanBackTrie, MarkupTokenMapping mapping, string id, bool initialState)
            {
                _definition = definition;
                Id = id;
                Tokens = tokens;
                _mapping = mapping;
                _structureTrie = structureTrie;
                _scanBackTrie = scanBackTrie;
                _closeConditionTrie = closeConditionTrie;
                IsInitialStateOn = string.IsNullOrEmpty(id) || initialState;
            }

            public string Id { get; }

            public IReadOnlyList<IToken> Tokens { get; }

            public bool IsInitialStateOn { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                bool flag;
                if (processor.Config.Flags.TryGetValue(Conditional.OperationName, out flag) && !flag)
                {
                    target.Write(Tokens[token].Value, Tokens[token].Start, Tokens[token].Length);
                    return Tokens[token].Length;
                }

                List<byte> conditionBytes = new List<byte>();
                ScanToCloseCondition(processor, conditionBytes, ref bufferLength, ref currentBufferPosition);
                byte[] condition = conditionBytes.ToArray();
                EngineConfig adjustedConfig = new EngineConfig(processor.Config.EnvironmentSettings, processor.Config.Whitespaces, processor.Config.LineEndings, processor.Config.Variables, _definition.VariableFormat);
                IProcessorState localState = new ProcessorState(new MemoryStream(condition), new MemoryStream(), conditionBytes.Count, int.MaxValue, adjustedConfig, Array.Empty<IOperationProvider>());
                int pos = 0;
                int len = conditionBytes.Count;

                bool faulted;
                bool value = _definition.Evaluator(localState, ref len, ref pos, out faulted);

                if (faulted)
                {
                    target.Write(Tokens[0].Value, Tokens[0].Start, Tokens[0].Length);
                    MemoryStream fragment = new MemoryStream();
                    fragment.Write(condition, 0, condition.Length);
                    fragment.Write(_closeConditionTrie.Tokens[0].Value, _closeConditionTrie.Tokens[0].Start, _closeConditionTrie.Tokens[0].Length);
                    fragment.Write(processor.CurrentBuffer, currentBufferPosition, bufferLength - currentBufferPosition);
                    fragment.Position = 0;
                    processor.Inject(fragment);
                    currentBufferPosition = processor.CurrentBufferPosition;
                    int written = Tokens[0].Length;
                    return written;
                }

                if (value)
                {
                    processor.WhitespaceHandler(ref bufferLength, ref currentBufferPosition, trimBackward: true);
                    return 0;
                }

                processor.SeekBackUntil(_scanBackTrie, true);
                FindEnd(processor, ref bufferLength, ref currentBufferPosition);
                processor.WhitespaceHandler(ref bufferLength, ref currentBufferPosition, _definition.WholeLine, _definition.TrimWhitespace);
                return 0;
            }

            private void FindEnd(IProcessorState processorState, ref int bufferLength, ref int currentBufferPosition)
            {
                int depth = 1;
                bool inElement = true;

                while (bufferLength >= _structureTrie.MinLength)
                {
                    //Try to get at least the max length of the tree into the buffer
                    if (bufferLength - currentBufferPosition < _structureTrie.MaxLength)
                    {
                        processorState.AdvanceBuffer(currentBufferPosition);
                        currentBufferPosition = processorState.CurrentBufferPosition;
                        bufferLength = processorState.CurrentBufferLength;
                    }

                    int sz = bufferLength == processorState.CurrentBuffer.Length ? _structureTrie.MaxLength : _structureTrie.MinLength;

                    for (; currentBufferPosition < bufferLength - sz + 1; ++currentBufferPosition)
                    {
                        if (bufferLength == 0)
                        {
                            currentBufferPosition = 0;
                            return;
                        }

                        int token;
                        if (_structureTrie.GetOperation(processorState.CurrentBuffer, bufferLength, ref currentBufferPosition, out token))
                        {
                            if (token == _mapping.OpenOpenElementToken)
                            {
                                ++depth;
                                inElement = true;
                            }
                            else if (token == _mapping.SelfClosingElementEndToken)
                            {
                                --depth;
                                inElement = false;
                            }
                            else if (token == _mapping.CloseElementTagToken)
                            {
                                if (inElement)
                                {
                                    inElement = false;
                                }
                                else
                                {
                                    --depth;
                                }
                            }
                            else if (token == _mapping.OpenCloseElementToken)
                            {
                                inElement = false;
                            }

                            if (depth == 0)
                            {
                                return;
                            }
                        }
                    }
                }

                //Ran out of places to check and haven't reached the actual match, consume all the way to the end
                currentBufferPosition = bufferLength;
            }

            private void ScanToCloseCondition(IProcessorState processorState, List<byte> conditionBytes, ref int bufferLength, ref int currentBufferPosition)
            {
                int previousPosition = currentBufferPosition;

                while (bufferLength >= _closeConditionTrie.MinLength)
                {
                    //Try to get at least the max length of the tree into the buffer
                    if (bufferLength - currentBufferPosition < _closeConditionTrie.MaxLength)
                    {
                        conditionBytes.AddRange(processorState.CurrentBuffer.Skip(previousPosition).Take(currentBufferPosition - previousPosition));
                        processorState.AdvanceBuffer(currentBufferPosition);
                        currentBufferPosition = processorState.CurrentBufferPosition;
                        bufferLength = processorState.CurrentBufferLength;
                        previousPosition = 0;
                    }

                    int sz = bufferLength == processorState.CurrentBuffer.Length ? _closeConditionTrie.MaxLength : _closeConditionTrie.MinLength;

                    for (; currentBufferPosition < bufferLength - sz + 1; ++currentBufferPosition)
                    {
                        if (bufferLength == 0)
                        {
                            currentBufferPosition = 0;
                            return;
                        }

                        int token;
                        if (_closeConditionTrie.GetOperation(processorState.CurrentBuffer, bufferLength, ref currentBufferPosition, out token))
                        {
                            conditionBytes.AddRange(processorState.CurrentBuffer.Skip(previousPosition).Take(currentBufferPosition - previousPosition - _closeConditionTrie.Tokens[token].Length));
                            return;
                        }
                    }
                }

                //Ran out of places to check and haven't reached the actual match, consume all the way to the end
                currentBufferPosition = bufferLength;
            }
        }
    }
}
