using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Mutant.Chicken
{
    public class ExpandVariables : IOperationProvider
    {
        public IOperation GetOperation(Encoding encoding, IProcessorState processor)
        {
            return new Impl(this, processor);
        }

        private class Impl : IOperation
        {
            public Impl(IOperationProvider definition, IProcessorState processor)
            {
                Definition = definition;
                Tokens = processor.EncodingConfig.VariableKeys;
            }

            public IOperationProvider Definition { get; }

            public IReadOnlyList<byte[]> Tokens { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                object result = processor.EncodingConfig[token];
                string output = result?.ToString();

                if (output != null)
                {
                    byte[] outputBytes = processor.Encoding.GetBytes(output);
                    target.Write(outputBytes, 0, outputBytes.Length);
                    return outputBytes.Length;
                }

                return 0;
            }
        }
    }
}
