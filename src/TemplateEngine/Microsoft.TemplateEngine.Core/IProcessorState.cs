using System.Text;

namespace Microsoft.TemplateEngine.Core
{
    public interface IProcessorState
    {
        EngineConfig Config { get; }

        byte[] CurrentBuffer { get; }

        int CurrentBufferLength { get; }

        int CurrentBufferPosition { get; }

        EncodingConfig EncodingConfig { get; }

        Encoding Encoding { get; set; }

        void AdvanceBuffer(int bufferPosition);

        void SeekForwardThrough(SimpleTrie trie, ref int bufferLength, ref int currentBufferPosition);

        void SeekForwardWhile(SimpleTrie trie, ref int bufferLength, ref int currentBufferPosition);

        void SeekBackUntil(SimpleTrie match);

        void SeekBackUntil(SimpleTrie match, bool consume);

        void SeekBackWhile(SimpleTrie match);
    }
}