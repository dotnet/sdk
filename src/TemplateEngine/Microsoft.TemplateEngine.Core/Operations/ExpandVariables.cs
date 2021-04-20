// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.Operations
{
    public class ExpandVariables : IOperationProvider
    {
        public static readonly string OperationName = "expandvariables";

        private readonly string _id;
        private readonly bool _initialState;

        public ExpandVariables(string id, bool initialState)
        {
            _id = id;
            _initialState = initialState;
        }

        public string Id => _id;

        public IOperation GetOperation(Encoding encoding, IProcessorState processor)
        {
            return new Impl(processor, _id, _initialState);
        }

        private class Impl : IOperation
        {
            private readonly string _id;

            public Impl(IProcessorState processor, string id, bool initialState)
            {
                Tokens = processor.EncodingConfig.VariableKeys;
                _id = id;
                IsInitialStateOn = string.IsNullOrEmpty(id) || initialState;
            }

            public IReadOnlyList<IToken> Tokens { get; }

            public string Id => _id;

            public bool IsInitialStateOn { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                bool flag;
                if (processor.Config.Flags.TryGetValue("expandVariables", out flag) && !flag)
                {
                    target.Write(Tokens[token].Value, Tokens[token].Start, Tokens[token].Length);
                    return Tokens[token].Length;
                }

                object result = processor.EncodingConfig[token];
                string output = result?.ToString() ?? "null";

                byte[] outputBytes = processor.Encoding.GetBytes(output);
                target.Write(outputBytes, 0, outputBytes.Length);
                return outputBytes.Length;
            }
        }
    }
}
