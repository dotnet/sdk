using System.Text;

namespace Microsoft.TemplateEngine.Abstractions.Engine
{
    public interface IProcessorState
    {
        IEngineConfig Config { get; }

        byte[] CurrentBuffer { get; }

        int CurrentBufferLength { get; }

        int CurrentBufferPosition { get; }

        IEncodingConfig EncodingConfig { get; }

        Encoding Encoding { get; set; }

        bool AdvanceBuffer(int bufferPosition);

        void SeekForwardThrough(ITokenTrie trie, ref int bufferLength, ref int currentBufferPosition);

        void SeekForwardWhile(ITokenTrie trie, ref int bufferLength, ref int currentBufferPosition);

        void SeekBackUntil(ITokenTrie match);

        void SeekBackUntil(ITokenTrie match, bool consume);

        void SeekBackWhile(ITokenTrie match);
    }
}