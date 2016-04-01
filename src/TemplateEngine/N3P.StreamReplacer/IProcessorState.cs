using System.Text;

namespace N3P.StreamReplacer
{
    public interface IProcessorState
    {
        byte[] CurrentBuffer { get; }

        int CurrentBufferPosition { get; }

        int CurrentBufferLength { get; }

        Encoding Encoding { get; }

        void AdvanceBuffer(int bufferPosition);

        SimpleTrie EOLMarkers { get; }
    }
}