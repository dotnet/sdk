using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions.Engine;

namespace Microsoft.TemplateEngine.Core
{
    public class Include : IOperationProvider
    {
        public Include(string startToken, string endToken, Func<string, Stream> sourceStreamOpener)
        {
            SourceStreamOpener = sourceStreamOpener;
            StartToken = startToken;
            EndToken = endToken;
        }

        public string EndToken { get; }

        public string StartToken { get; }

        public Func<string, Stream> SourceStreamOpener { get; }

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            byte[] tokenBytes = encoding.GetBytes(StartToken);
            byte[] endTokenBytes = encoding.GetBytes(EndToken);
            TokenTrie endTokenMatcher = new TokenTrie();
            endTokenMatcher.AddToken(endTokenBytes);
            return new Impl(tokenBytes, endTokenMatcher, this);
        }

        private class Impl : IOperation
        {
            private readonly Include _source;
            private readonly TokenTrie _endTokenMatcher;

            public Impl(byte[] token, TokenTrie endTokenMatcher, Include source)
            {
                Tokens = new[] {token};
                _source = source;
                _endTokenMatcher = endTokenMatcher;
            }

            public IReadOnlyList<byte[]> Tokens { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                bool flag;
                if (processor.Config.Flags.TryGetValue("include", out flag) && !flag)
                {
                    byte[] tokenValue = Tokens[token];
                    target.Write(tokenValue, 0, tokenValue.Length);
                    return tokenValue.Length;
                }

                List<byte> pathBytes = new List<byte>();
                while (!_endTokenMatcher.GetOperation(processor.CurrentBuffer, bufferLength, ref currentBufferPosition, out token))
                {
                    pathBytes.Add(processor.CurrentBuffer[currentBufferPosition++]);
                    if (bufferLength - currentBufferPosition < _endTokenMatcher.MinLength)
                    {
                        processor.AdvanceBuffer(currentBufferPosition);
                        bufferLength = processor.CurrentBufferLength;
                        currentBufferPosition = 0;

                        if (bufferLength == 0)
                        {
                            break;
                        }
                    }
                }

                byte[] pathBytesArray = pathBytes.ToArray();
                string sourceLocation = processor.Encoding.GetString(pathBytesArray).Trim();

                const int pageSize = 65536;
                //Start off with a 64K buffer, we'll keep adding chunks to this
                byte[] composite = new byte[pageSize];
                int totalLength;
                
                using (Stream data = _source.SourceStreamOpener(sourceLocation))
                {
                    int index = composite.Length - pageSize;
                    int nRead = data.Read(composite, index, pageSize);

                    //As long as we're reading whole pages, keep allocating more space ahead
                    while (nRead == pageSize)
                    {
                        byte[] newBuffer = new byte[composite.Length + pageSize];
                        Buffer.BlockCopy(composite, 0, newBuffer, 0, composite.Length);
                        composite = newBuffer;
                        nRead = data.Read(composite, index, pageSize);
                    }

                    totalLength = composite.Length - (pageSize - nRead);
                }

                byte[] bom;
                Encoding realEncoding = EncodingUtil.Detect(composite, totalLength, out bom);

                if (!Equals(realEncoding, processor.Encoding))
                {
                    composite = Encoding.Convert(realEncoding, processor.Encoding, composite, bom.Length, totalLength - bom.Length);
                    totalLength = composite.Length;
                }

                target.Write(composite, 0, totalLength - bom.Length);
                return composite.Length;
            }
        }
    }
}
