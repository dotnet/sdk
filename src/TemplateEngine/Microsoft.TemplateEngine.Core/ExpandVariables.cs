using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions.Engine;

namespace Microsoft.TemplateEngine.Core
{
    public class ExpandVariables : IOperationProvider
    {
        private string _id;

        public ExpandVariables(string id)
        {
            _id = id;
        }

        public IOperation GetOperation(Encoding encoding, IProcessorState processor)
        {
            return new Impl(processor, _id);
        }

        private class Impl : IOperation
        {
            public Impl(IProcessorState processor, string id)
            {
                Tokens = processor.EncodingConfig.VariableKeys;
                Id = id;
            }

            public IReadOnlyList<byte[]> Tokens { get; }

            public string Id { get; private set; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                bool flag;
                if (processor.Config.Flags.TryGetValue("expandVariables", out flag) && !flag)
                {
                    byte[] tokenValue = Tokens[token];
                    target.Write(tokenValue, 0, tokenValue.Length);
                    return tokenValue.Length;
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
