using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions.Engine;

namespace Microsoft.TemplateEngine.Core
{
    public class Region : IOperationProvider
    {
        private readonly string _end;
        private readonly bool _include;
        private readonly string _start;
        private readonly bool _toggle;
        private readonly bool _wholeLine;
        private readonly bool _trimWhitespace;
        private readonly string _id;

        public Region(string start, string end, bool include, bool wholeLine, bool trimWhitespace, string id)
        {
            _wholeLine = wholeLine;
            _trimWhitespace = trimWhitespace;
            _start = start;
            _end = end;
            _include = include;
            _toggle = _start == _end;
            _id = id;
        }

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            byte[] startToken = encoding.GetBytes(_start);
            byte[] endToken = encoding.GetBytes(_end);
            return new Impl(this, startToken, endToken, _include, _toggle, _id);
        }

        private class Impl : IOperation
        {
            private readonly byte[] _endToken;
            private readonly bool _includeRegion;
            private readonly bool _startAndEndAreSame;
            private bool _waitingForEnd;
            private readonly Region _definition;

            public Impl(Region owner, byte[] startToken, byte[] endToken, bool include, bool toggle, string id)
            {
                _definition = owner;
                _endToken = endToken;
                _includeRegion = include;
                _startAndEndAreSame = toggle;

                Tokens = toggle ? new[] {startToken} : new[] {startToken, endToken};
                Id = id;
            }

            public IReadOnlyList<byte[]> Tokens { get; }

            public string Id { get; private set; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                bool flag;
                if (processor.Config.Flags.TryGetValue("regions", out flag) && !flag)
                {
                    byte[] tokenValue = Tokens[token];
                    target.Write(tokenValue, 0, tokenValue.Length);
                    return tokenValue.Length;
                }

                processor.WhitespaceHandler(ref bufferLength, ref currentBufferPosition, wholeLine: _definition._wholeLine, trim: _definition._trimWhitespace);

                if (_startAndEndAreSame)
                {
                    token = _waitingForEnd ? 1 : 0;
                }

                //If we're resuming from a region that has been included (we've found the end now)
                //  just process the end
                if (_waitingForEnd && token == 1)
                {
                    _waitingForEnd = false;
                    return 0;
                }

                if (token != 0)
                {
                    return 0;
                }

                //If we're including the region, set that we're waiting for the end and return
                //  control to the processor
                if (_includeRegion)
                {
                    _waitingForEnd = true;
                    return 0;
                }

                //If we've made it here, we're skipping stuff, skip all the way to the end of the
                //  end token

                int i = currentBufferPosition;
                int j = 0;

                for (; j < _endToken.Length; ++j)
                {
                    if (i + j == bufferLength)
                    {
                        processor.AdvanceBuffer(i + j);
                        bufferLength = processor.CurrentBufferLength;
                        i = -j;
                    }

                    if (processor.CurrentBuffer[i + j] != _endToken[j])
                    {
                        ++i;
                        j = -1;
                    }
                }

                i += j;

                processor.WhitespaceHandler(ref bufferLength, ref i, wholeLine: _definition._wholeLine, trim: _definition._trimWhitespace);

                currentBufferPosition = i;
                return 0;
            }
        }
    }
}