// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockOperation : IOperation
    {
        public delegate int MatchHandler(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target);
        private readonly MatchHandler _onMatch;

        public MockOperation(string id, MatchHandler onMatch, bool initialState, params byte[][] tokens)
            : this(id, onMatch, initialState, tokens.Select(token => TokenConfig.LiteralToken(token)).ToArray())
        {
        }

        public MockOperation(string id, MatchHandler onMatch, bool initialState, params IToken[] tokens)
        {
            Tokens = tokens;
            Id = id;
            _onMatch = onMatch;
            IsInitialStateOn = initialState;
        }

        public IReadOnlyList<IToken> Tokens { get; }

        public string Id { get; }

        public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
        {
            return _onMatch?.Invoke(processor, bufferLength, ref currentBufferPosition, token, target) ?? 0;
        }

        public IOperationProvider Provider => new MockOperationProvider(this);

        public bool IsInitialStateOn { get; }
    }
}
