using Microsoft.TemplateEngine.Core.Matching;

namespace Microsoft.TemplateEngine.Core.Util
{
    public class Token : TerminalBase
    {
        public Token(byte[] token, int index, int start = 0, int end = -1)
            : base(token.Length, start, end)
        {
            Value = token;
            Index = index;
        }

        public byte[] Value { get; }

        public int Index { get; }
    }
}
