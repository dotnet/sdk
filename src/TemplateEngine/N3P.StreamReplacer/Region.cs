using System.Collections.Generic;
using System.IO;
using System.Text;

namespace N3P.StreamReplacer
{
    public class Region : IOperationProvider
    {
        private readonly string _end;
        private readonly bool _include;
        private readonly string _start;
        private readonly bool _toggle;
        public Region(string start, string end, bool include)
        {
            _start = start;
            _end = end;
            _include = include;
            _toggle = _start == _end;
        }

        public IOperation GetOperation(Encoding encoding)
        {
            byte[] startToken = encoding.GetBytes(_start);
            byte[] endToken = encoding.GetBytes(_end);
            return new Impl(this, startToken, endToken, _include, _toggle);
        }

        public override string ToString()
        {
            return $"[{_start} ... {_end}]";
        }

        private class Impl : IOperation
        {
            private readonly byte[] _endToken;
            private readonly bool _includeRegion;
            private readonly bool _startAndEndAreSame;
            private bool _waitingForEnd;

            public Impl(IOperationProvider owner, byte[] startToken, byte[] endToken, bool include, bool toggle)
            {
                Definition = owner;
                _endToken = endToken;
                _includeRegion = include;
                _startAndEndAreSame = toggle;

                Tokens = toggle ? new[] {startToken} : new[] {startToken, endToken};
            }

            public IOperationProvider Definition { get; }
            public IReadOnlyList<byte[]> Tokens { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
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

                        if (processor.CurrentBufferLength == 0)
                        {
                            return 0;
                        }

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

                currentBufferPosition = i;
                return 0;
            }
        }
    }
}