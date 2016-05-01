using System.Collections.Generic;
using System.IO;

namespace Mutant.Chicken.Core
{
    public interface IOperation
    {
        IReadOnlyList<byte[]> Tokens { get; }

        int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target);
    }
}