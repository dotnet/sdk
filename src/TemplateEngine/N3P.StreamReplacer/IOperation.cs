using System.Collections.Generic;
using System.IO;

namespace N3P.StreamReplacer
{
    public interface IOperation
    {
        IOperationProvider Definition { get; }

        IReadOnlyList<byte[]> Tokens { get; }

        int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target);
    }
}