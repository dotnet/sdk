using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Mutant.Chicken
{
    public class Replacment : IOperationProvider
    {
        private readonly string _match;
        private readonly string _replaceWith;

        public Replacment(string match, string replaceWith)
        {
            _match = match;
            _replaceWith = replaceWith;
        }

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            byte[] token = encoding.GetBytes(_match);
            return new Impl(token, encoding.GetBytes(_replaceWith));
        }

        private class Impl : IOperation
        {
            private readonly byte[] _replacement;
            private readonly byte[] _token;

            public Impl(byte[] token, byte[] replaceWith)
            {
                _replacement = replaceWith;
                _token = token;
                Tokens = new[] {token};
            }

            public IReadOnlyList<byte[]> Tokens { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                target.Write(_replacement, 0, _replacement.Length);
                return _token.Length;
            }
        }
    }
}
