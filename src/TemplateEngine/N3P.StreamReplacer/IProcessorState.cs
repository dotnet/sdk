using System.Collections.Generic;
using System.Text;

namespace N3P.StreamReplacer
{
    public interface IProcessorState
    {
        byte[] CurrentBuffer { get; }

        int CurrentBufferLength { get; }

        int CurrentBufferPosition { get; }

        Encoding Encoding { get; }

        SimpleTrie EOLMarkers { get; }

        IReadOnlyList<byte[]> EOLTails { get; }

        int MaxEOLTailLength { get; }

        void AdvanceBuffer(int bufferPosition);

        void ConsumeToEndOfLine(ref int bufferLength, ref int currentBufferPosition);

        void TrimBackToPreviousEOL();
    }
}