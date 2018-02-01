using System;
using System.IO;

namespace BaselineComparer
{
    public class BufferedReadStream
    {
        private static readonly int _readBlockSize = 1000;

        private readonly Stream _source;
        private int _readBlockNumber;
        private int _positionInBuffer;
        private byte[] _buffer;

        private bool _finalReadBlock;
        private int _endIfFinalReadBlock;

        public BufferedReadStream(Stream source)
        {
            _source = source;
            _readBlockNumber = -1;
            _positionInBuffer = 0;
            _buffer = new byte[_readBlockSize];

            _finalReadBlock = false;
            _endIfFinalReadBlock = -1;

            _inReplayMode = false;

            ReadNextBlock();
        }

        private bool _inReplayMode;
        private int _replayIndex;
        private byte[] _replayBuffer;
        private int _replayStartPosition;

        public void SetupReplayBytes(byte[] toReplay)
        {
            if (_inReplayMode)
            {
                throw new Exception("Cant setup nested replays");
            }

            _replayStartPosition = LastReadPosition - toReplay.Length;
            _replayIndex = 0;
            _replayBuffer = (byte[])toReplay.Clone();
            _inReplayMode = true;
        }

        public bool TryReadNext(out byte next)
        {
            if (_inReplayMode)
            {
                if (_replayIndex == _replayBuffer.Length)
                {
                    _inReplayMode = false;
                }
                else
                {
                    next = _replayBuffer[_replayIndex];
                    ++_replayIndex;
                    return true;
                }
            }

            if (_positionInBuffer >= _readBlockSize)
            {
                ReadNextBlock();
            }

            if (_finalReadBlock && _positionInBuffer >= _endIfFinalReadBlock)
            {
                next = 0;
                return false;
            }

            next = _buffer[_positionInBuffer++];
            return true;
        }

        public byte[] ReadBytes(int toReadCount, out int actuallyReadCount)
        {
            byte[] tryReadBuffer = new byte[toReadCount];
            actuallyReadCount = 0;

            for (int i = 0; i < toReadCount; i++)
            {
                if (TryReadNext(out byte next))
                {
                    tryReadBuffer[i] = next;
                }
                else
                {
                    break;
                }

                ++actuallyReadCount;
            }

            Span<byte> actuallyReadBuffer = tryReadBuffer;
            return actuallyReadBuffer.Slice(0, actuallyReadCount).ToArray();
        }

        public int LastReadPosition
        {
            get
            {
                if (_inReplayMode)
                {
                    return _replayStartPosition + _replayIndex;
                }

                return _readBlockNumber * _readBlockSize + _positionInBuffer;
            }
        }

        public bool DoneReading
        {
            get
            {
                return _finalReadBlock && _positionInBuffer >= _endIfFinalReadBlock;
            }
        }

        private void ReadNextBlock()
        {
            Array.Clear(_buffer, 0, _readBlockSize);
            int byteCount = _source.Read(_buffer, 0, _readBlockSize);

            if (byteCount < _readBlockSize)
            {
                _finalReadBlock = true;
                _endIfFinalReadBlock = byteCount;
            }

            _positionInBuffer = 0;
            ++_readBlockNumber;
        }
    }
}
