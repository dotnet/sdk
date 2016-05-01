using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Mutant.Chicken.Core
{
    public class ExpandVariables : IOperationProvider
    {
        public IOperation GetOperation(Encoding encoding, IProcessorState processor)
        {
            return new Impl(processor);
        }

        private class Impl : IOperation
        {
            public Impl(IProcessorState processor)
            {
                Tokens = processor.EncodingConfig.VariableKeys;
            }

            public IReadOnlyList<byte[]> Tokens { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                object result = processor.EncodingConfig[token];
                string output = result?.ToString() ?? "null";

                byte[] outputBytes = processor.Encoding.GetBytes(output);
                target.Write(outputBytes, 0, outputBytes.Length);
                return outputBytes.Length;
            }
        }
    }
}
