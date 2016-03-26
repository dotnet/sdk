namespace N3P.StreamReplacer
{
    public interface IProcessorState
    {
        byte[] CurrentBuffer { get; }

        int CurrentBufferPosition { get; }

        int CurrentBufferLength { get; }

        void AdvanceBuffer(int bufferPosition);
    }
}