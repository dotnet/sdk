// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.Operations
{
    public class Region : IOperationProvider
    {
        public static readonly string OperationName = "region";

        private readonly ITokenConfig _end;
        private readonly bool _include;
        private readonly ITokenConfig _start;
        private readonly bool _toggle;
        private readonly bool _wholeLine;
        private readonly bool _trimWhitespace;
        private readonly string _id;
        private readonly bool _initialState;

        public Region(ITokenConfig start, ITokenConfig end, bool include, bool wholeLine, bool trimWhitespace, string id, bool initialState)
        {
            _wholeLine = wholeLine;
            _trimWhitespace = trimWhitespace;
            _start = start;
            _end = end;
            _include = include;
            _toggle = _start.Equals(_end);
            _id = id;
            _initialState = initialState;
        }

        public string Id => _id;

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            IToken startToken = _start.ToToken(encoding);
            IToken endToken = _end.ToToken(encoding);
            return new Impl(this, startToken, endToken, _include, _toggle, _id, _initialState);
        }

        private class Impl : IOperation
        {
            private readonly IToken _endToken;
            private readonly bool _includeRegion;
            private readonly bool _startAndEndAreSame;
            private readonly Region _definition;
            private readonly string _id;
            private bool _waitingForEnd;

            public Impl(Region owner, IToken startToken, IToken endToken, bool include, bool toggle, string id, bool initialState)
            {
                _definition = owner;
                _endToken = endToken;
                _includeRegion = include;
                _startAndEndAreSame = toggle;

                Tokens = toggle ? new[] { startToken } : new[] { startToken, endToken };
                _id = id;
                IsInitialStateOn = string.IsNullOrEmpty(id) || initialState;
            }

            public IReadOnlyList<IToken> Tokens { get; }

            public string Id => _id;

            public bool IsInitialStateOn { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                bool flag;
                if (processor.Config.Flags.TryGetValue(Region.OperationName, out flag) && !flag)
                {
                    target.Write(Tokens[token].Value, Tokens[token].Start, Tokens[token].Length);
                    return Tokens[token].Length;
                }

                processor.WhitespaceHandler(ref bufferLength, ref currentBufferPosition, wholeLine: _definition._wholeLine, trim: _definition._trimWhitespace);

                if (_startAndEndAreSame)
                {
                    token = _waitingForEnd ? 1 : 0;
                }

                //If we're resuming from a region that has been included (we've found the end now)
                //  just process the end
                if (_waitingForEnd && token == 1)
                {
                    _waitingForEnd = false;
                    return 0;
                }

                if (token != 0)
                {
                    return 0;
                }

                //If we're including the region, set that we're waiting for the end and return
                //  control to the processor
                if (_includeRegion)
                {
                    _waitingForEnd = true;
                    return 0;
                }

                //If we've made it here, we're skipping stuff, skip all the way to the end of the
                //  end token

                int i = currentBufferPosition;
                int j = 0;

                for (; j < _endToken.Length; ++j)
                {
                    if (i + j == bufferLength)
                    {
                        processor.AdvanceBuffer(i + j);
                        bufferLength = processor.CurrentBufferLength;
                        i = -j;
                    }

                    //TODO: This should be using one of the tries rather than looking for the byte run directly
                    if (processor.CurrentBuffer[i + j] != _endToken.Value[j])
                    {
                        ++i;
                        j = -1;
                    }
                }

                i += j;

                processor.WhitespaceHandler(ref bufferLength, ref i, wholeLine: _definition._wholeLine, trim: _definition._trimWhitespace);

                currentBufferPosition = i;
                return 0;
            }
        }
    }
}
