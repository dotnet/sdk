// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.Operations
{
    public class Replacement : IOperationProvider
    {
        public static readonly string OperationName = "replacement";

        private readonly ITokenConfig _match;
        private readonly string _replaceWith;
        private readonly string _id;
        private readonly bool _initialState;

        public Replacement(ITokenConfig match, string replaceWith, string id, bool initialState)
        {
            _match = match;
            _replaceWith = replaceWith;
            _id = id;
            _initialState = initialState;
        }

        public string Id => _id;

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            IToken token = _match.ToToken(encoding);
            byte[] replaceWith = encoding.GetBytes(_replaceWith);

            if (token.Value.Skip(token.Start).Take(token.Length).SequenceEqual(replaceWith))
            {
                return null;
            }

            return new Impl(token, replaceWith, _id, _initialState);
        }

        private class Impl : IOperation
        {
            private readonly byte[] _replacement;
            private readonly IToken _token;
            private readonly string _id;

            public Impl(IToken token, byte[] replaceWith, string id, bool initialState)
            {
                _replacement = replaceWith;
                _token = token;
                _id = id;
                Tokens = new[] { token };
                IsInitialStateOn = string.IsNullOrEmpty(id) || initialState;
            }

            public IReadOnlyList<IToken> Tokens { get; }

            public string Id => _id;

            public bool IsInitialStateOn { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                bool flag;
                if (processor.Config.Flags.TryGetValue(OperationName, out flag) && !flag)
                {
                    target.Write(Tokens[token].Value, Tokens[token].Start, Tokens[token].Length);
                    return Tokens[token].Length;
                }

                target.Write(_replacement, 0, _replacement.Length);
                return _replacement.Length;
            }
        }
    }
}
