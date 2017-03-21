using System.Collections.Generic;
using System.IO;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IOperation
    {
        IReadOnlyList<IToken> Tokens { get; }

        int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target);

        string Id { get; }

        bool IsInitialStateOn { get; }
    }
}
