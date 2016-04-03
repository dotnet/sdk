using System.Collections.Generic;
using System.Text;

namespace Mutant.Chicken
{
    public interface IProcessorState
    {
        byte[] CurrentBuffer { get; }

        int CurrentBufferLength { get; }

        int CurrentBufferPosition { get; }

        Encoding Encoding { get; }

        SimpleTrie EOLMarkers { get; }

        SimpleTrie WhitespaceMarkers { get; }

        IReadOnlyList<byte[]> EOLTails { get; }

        IReadOnlyList<byte[]> WhitespaceTails { get; }

        int MaxEOLTailLength { get; }

        int MaxWhitespaceTailLength { get; }

        void AdvanceBuffer(int bufferPosition);

        void ConsumeToEndOfLine(ref int bufferLength, ref int currentBufferPosition);

        void TrimBackToPreviousEOL();

        void TrimBackWhitespace();

        void TrimForwardWhitespace();
    }
}