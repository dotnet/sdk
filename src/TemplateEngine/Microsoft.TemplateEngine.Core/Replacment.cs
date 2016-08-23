using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using Microsoft.TemplateEngine.Abstractions.Engine;

namespace Microsoft.TemplateEngine.Core
{
    public class Replacment : IOperationProvider
    {
        private readonly string _match;
        private readonly string _replaceWith;
        private string _id;

        public Replacment(string match, string replaceWith, string id)
        {
            _match = match;
            _replaceWith = replaceWith;
            _id = id;
        }

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            byte[] token = encoding.GetBytes(_match);
            byte[] replaceWith = encoding.GetBytes(_replaceWith);

            if(token.SequenceEqual(replaceWith))
            {
                return null;
            }

            return new Impl(token, replaceWith, _id);
        }

        private class Impl : IOperation
        {
            private readonly byte[] _replacement;
            private readonly byte[] _token;

            public Impl(byte[] token, byte[] replaceWith, string id)
            {
                _replacement = replaceWith;
                _token = token;
                Id = id;
                Tokens = new[] {token};
            }

            public IReadOnlyList<byte[]> Tokens { get; }

            public string Id { get; private set; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                bool flag;
                if (processor.Config.Flags.TryGetValue("replacements", out flag) && !flag)
                {
                    byte[] tokenValue = Tokens[token];
                    target.Write(tokenValue, 0, tokenValue.Length);
                    return tokenValue.Length;
                }

                target.Write(_replacement, 0, _replacement.Length);
                return _token.Length;
            }
        }
    }
}
