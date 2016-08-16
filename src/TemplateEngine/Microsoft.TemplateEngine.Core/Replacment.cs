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
        private readonly int? _id;

        public Replacment(string match, string replaceWith)
            : this(match, replaceWith, null)
        {
        }

        public Replacment(string match, string replaceWith, int? id)
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
            private readonly int? _id;

            public Impl(byte[] token, byte[] replaceWith, int? id)
            {
                _replacement = replaceWith;
                _token = token;
                _id = id;
                Tokens = new[] {token};
            }

            public IReadOnlyList<byte[]> Tokens { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                bool flag;
                if (processor.Config.Flags.TryGetValue("replacements", out flag) && !flag)
                {
                    byte[] tokenValue = Tokens[token];
                    target.Write(tokenValue, 0, tokenValue.Length);
                    return tokenValue.Length;
                }

                bool opFlag;
                if (_id.HasValue 
                    && processor.Config.Flags.TryGetValue(processor.Config.OperationIdFlag(_id.GetValueOrDefault()), out opFlag) 
                    && !opFlag)
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
