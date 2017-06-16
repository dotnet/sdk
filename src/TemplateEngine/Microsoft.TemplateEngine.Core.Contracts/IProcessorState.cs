using System.IO;
using System.Text;

namespace Microsoft.TemplateEngine.Core.Contracts
{
    public interface IProcessorState
    {
        IEngineConfig Config { get; }

        byte[] CurrentBuffer { get; }

        int CurrentBufferLength { get; }

        int CurrentBufferPosition { get; }

        int CurrentSequenceNumber { get; }

        IEncodingConfig EncodingConfig { get; }

        Encoding Encoding { get; set; }

        bool AdvanceBuffer(int bufferPosition);

        void SeekForwardUntil(ITokenTrie trie, ref int bufferLength, ref int currentBufferPosition);

        void SeekForwardThrough(ITokenTrie trie, ref int bufferLength, ref int currentBufferPosition);

        void SeekForwardWhile(ITokenTrie trie, ref int bufferLength, ref int currentBufferPosition);

        void SeekBackUntil(ITokenTrie match);

        void SeekBackUntil(ITokenTrie match, bool consume);

        void SeekBackWhile(ITokenTrie match);

        void Inject(Stream staged);
    }
}
