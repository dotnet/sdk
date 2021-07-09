// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Util;

namespace Microsoft.TemplateEngine.Core.Operations
{
    public class Include : IOperationProvider
    {
        public static readonly string OperationName = "include";

        private readonly string _id;

        private readonly bool _initialState;

        public Include(ITokenConfig startToken, ITokenConfig endToken, Func<string, Stream> sourceStreamOpener, string id, bool initialState)
        {
            SourceStreamOpener = sourceStreamOpener;
            StartToken = startToken;
            EndToken = endToken;
            _id = id;
            _initialState = initialState;
        }

        public ITokenConfig EndToken { get; }

        public ITokenConfig StartToken { get; }

        public Func<string, Stream> SourceStreamOpener { get; }

        public string Id => _id;

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            IToken tokenBytes = StartToken.ToToken(encoding);
            IToken endTokenBytes = EndToken.ToToken(encoding);
            TokenTrie endTokenMatcher = new TokenTrie();
            endTokenMatcher.AddToken(endTokenBytes);
            return new Impl(tokenBytes, endTokenMatcher, this, _id, _initialState);
        }

        private class Impl : IOperation
        {
            private readonly Include _source;
            private readonly ITokenTrie _endTokenMatcher;
            private readonly string _id;

            public Impl(IToken token, ITokenTrie endTokenMatcher, Include source, string id, bool initialState)
            {
                Tokens = new[] { token };
                _source = source;
                _endTokenMatcher = endTokenMatcher;
                _id = id;
                IsInitialStateOn = string.IsNullOrEmpty(id) || initialState;
            }

            public IReadOnlyList<IToken> Tokens { get; }

            public string Id => _id;

            public bool IsInitialStateOn { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                bool flag;
                if (processor.Config.Flags.TryGetValue(OperationName, out flag) && !flag)
                {
                    target.Write(Tokens[token].Value, Tokens[token].Start, Tokens[token].Length);
                    return Tokens[token].Length;
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
                int totalBytesRead = 0;
                using (Stream data = _source.SourceStreamOpener(sourceLocation))
                {
                    while (totalBytesRead < composite.Length)
                    {
                        int bytesRead = data.Read(composite, totalBytesRead, composite.Length - totalBytesRead);
                        if (bytesRead == 0)
                        {
                            break;
                        }
                        totalBytesRead += bytesRead;
                    }
                    int bytesFromPage = totalBytesRead;

                    //As long as we're reading whole pages, keep allocating more space ahead
                    while (bytesFromPage == pageSize)
                    {
                        byte[] newBuffer = new byte[composite.Length + pageSize];
                        Buffer.BlockCopy(composite, 0, newBuffer, 0, composite.Length);
                        composite = newBuffer;

                        bytesFromPage = 0;
                        while (totalBytesRead < composite.Length)
                        {
                            int bytesRead = data.Read(composite, totalBytesRead, composite.Length - totalBytesRead);
                            if (bytesRead == 0)
                            {
                                break;
                            }
                            totalBytesRead += bytesRead;
                            bytesFromPage += bytesRead;
                        }
                    }
                }

                int offset = 0;
                int nBytesToWrite;
                Encoding realEncoding = EncodingUtil.Detect(composite, totalBytesRead, out byte[] bom);

                if (!Equals(realEncoding, processor.Encoding))
                {
                    composite = Encoding.Convert(realEncoding, processor.Encoding, composite, bom.Length, totalBytesRead - bom.Length);
                    nBytesToWrite = composite.Length;
                }
                else
                {
                    offset = bom.Length;
                    nBytesToWrite = totalBytesRead - bom.Length;
                }

                target.Write(composite, offset, nBytesToWrite);
                return nBytesToWrite;
            }
        }
    }
}
