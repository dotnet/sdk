// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.Operations
{
    public class ExpandVariables : IOperationProvider
    {
        public static readonly string OperationName = "expandvariables";

        private readonly bool _initialState;

        public ExpandVariables(string? id, bool initialState)
        {
            Id = id;
            _initialState = initialState;
        }

        public string? Id { get; }

        public IOperation GetOperation(Encoding encoding, IProcessorState processor)
        {
            return new Implementation(processor, Id, _initialState);
        }

        private class Implementation : IOperation
        {
            public Implementation(IProcessorState processor, string? id, bool initialState)
            {
                Tokens = processor.EncodingConfig.VariableKeys;
                Id = id;
                IsInitialStateOn = string.IsNullOrEmpty(id) || initialState;
            }

            public IReadOnlyList<IToken> Tokens { get; }

            public string? Id { get; }

            public bool IsInitialStateOn { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token)
            {
                if (processor.Config.Flags.TryGetValue("expandVariables", out bool flag) && !flag)
                {
                    processor.WriteToTarget(Tokens[token].Value, Tokens[token].Start, Tokens[token].Length);
                    return Tokens[token].Length;
                }

                object result = processor.EncodingConfig[token];
                string output = result?.ToString() ?? "null";

                byte[] outputBytes = processor.Encoding.GetBytes(output);
                processor.WriteToTarget(outputBytes, 0, outputBytes.Length);
                return outputBytes.Length;
            }
        }
    }
}
