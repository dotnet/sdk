using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Matching;

namespace Microsoft.TemplateEngine.Core.Util
{
    public class TokenTrieEvaluator : TrieEvaluator<Token>, ITokenTrieEvaluator
    {
        private int _currentSequenceNumber;

        public TokenTrieEvaluator(Trie<Token> trie)
            : base(trie)
        {
        }

        public int BytesToKeepInBuffer => _currentSequenceNumber - OldestRequiredSequenceNumber + 1;

        public bool Accept(byte data, ref int bufferPosition, out int token)
        {
            ++_currentSequenceNumber;
            if(Accept(data, ref _currentSequenceNumber, out TerminalLocation<Token> terminal))
            {
                token = terminal.Terminal.Index;
                bufferPosition += _currentSequenceNumber - terminal.Location - terminal.Terminal.End;
                return true;
            }

            token = -1;
            return false;
        }

        public bool TryFinalizeMatchesInProgress(ref int bufferPosition, out int token)
        {
            FinalizeMatchesInProgress(ref _currentSequenceNumber, out TerminalLocation<Token> terminal);

            if(terminal != null)
            {
                token = terminal.Terminal.Index;
                bufferPosition += _currentSequenceNumber - terminal.Location - terminal.Terminal.End;
                return true;
            }

            token = -1;
            return false;
        }
    }
}