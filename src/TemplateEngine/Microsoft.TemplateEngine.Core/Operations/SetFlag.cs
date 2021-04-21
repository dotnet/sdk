// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.Operations
{
    public class SetFlag : IOperationProvider
    {
        public static readonly string OperationName = "flags";

        private readonly string _id;
        private readonly bool _initialState;

        public SetFlag(string name, ITokenConfig on, ITokenConfig off, ITokenConfig onNoEmit, ITokenConfig offNoEmit, string id, bool initialState, bool? @default = null)
        {
            Name = name;
            On = on;
            Off = off;
            OnNoEmit = onNoEmit;
            OffNoEmit = offNoEmit;
            Default = @default;
            _id = id;
            _initialState = initialState;
        }

        public string Id => _id;

        public string Name { get; }

        public ITokenConfig On { get; }

        public ITokenConfig Off { get; }

        public bool? Default { get; }

        public ITokenConfig OnNoEmit { get; }

        public ITokenConfig OffNoEmit { get; }

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            IToken[] tokens = new[]
            {
                On.ToToken(encoding),
                Off.ToToken(encoding),
                OnNoEmit.ToToken(encoding),
                OffNoEmit.ToToken(encoding)
            };

            if (Default.HasValue)
            {
                processorState.Config.Flags[Name] = Default.Value;
            }

            return new Impl(this, tokens, _id, _initialState);
        }

        private class Impl : IOperation
        {
            private readonly SetFlag _owner;
            private readonly string _id;

            public Impl(SetFlag owner, IReadOnlyList<IToken> tokens, string id, bool initialState)
            {
                _owner = owner;
                Tokens = tokens;
                _id = id;
                IsInitialStateOn = string.IsNullOrEmpty(id) || initialState;
            }

            public IReadOnlyList<IToken> Tokens { get; }

            public string Id => _id;

            public bool IsInitialStateOn { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                if (!processor.Config.Flags.TryGetValue(OperationName, out bool flagsOn))
                {
                    flagsOn = true;
                }

                bool emit = token < 2 || !flagsOn;
                bool turnOn = (token % 2) == 0;
                int written = 0;

                if (emit)
                {
                    target.Write(Tokens[token].Value, Tokens[token].Start, Tokens[token].Length);
                    written = Tokens[token].Length;
                }
                else
                {
                    // consume the entire line when not emitting. Otherwise the newlines on the falg tokens get emitted
                    processor.SeekForwardThrough(processor.EncodingConfig.LineEndings, ref bufferLength, ref currentBufferPosition);
                }

                //Only turn the flag in question back on if it's the "flags" flag.
                //  Yes, we still need to emit it as the common case is for this
                //  to be done in the template definition file
                if (flagsOn)
                {
                    processor.Config.Flags[_owner.Name] = turnOn;
                }
                else if (_owner.Name == SetFlag.OperationName && turnOn)
                {
                    processor.Config.Flags[SetFlag.OperationName] = true;
                }

                return written;
            }
        }
    }
}
