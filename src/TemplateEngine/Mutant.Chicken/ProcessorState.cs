using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Mutant.Chicken
{
    internal class ProcessorState : IProcessorState
    {
        private readonly int _flushThreshold;
        private readonly Stream _source;
        private readonly Stream _target;
        private readonly Trie _trie;
        private Encoding _encoding;

        public ProcessorState(Stream source, Stream target, int bufferSize, int flushThreshold, EngineConfig config, IReadOnlyList<IOperationProvider> operationProviders)
        {
            _source = source;
            _target = target;
            Config = config;
            _flushThreshold = flushThreshold;
            CurrentBuffer = new byte[bufferSize];
            CurrentBufferLength = source.Read(CurrentBuffer, 0, CurrentBuffer.Length);

            byte[] bom;
            Encoding encoding = DetectEncoding(CurrentBuffer, CurrentBufferLength, out bom);
            Encoding = encoding;
            CurrentBufferPosition = bom.Length;

            IOperation[] operations = new IOperation[operationProviders.Count];

            for (int i = 0; i < operations.Length; ++i)
            {
                operations[i] = operationProviders[i].GetOperation(encoding, this);
            }

            _trie = Trie.Create(operations);
        }

        public EngineConfig Config { get; }

        public byte[] CurrentBuffer { get; }

        public int CurrentBufferLength { get; private set; }

        public int CurrentBufferPosition { get; private set; }

        public Encoding Encoding
        {
            get
            {
                return _encoding;
            }
            set
            {
                _encoding = value;
                EncodingConfig = new EncodingConfig(Config, _encoding);
            }
        }

        public EncodingConfig EncodingConfig { get; private set; }

        public void AdvanceBuffer(int bufferPosition)
        {
            if (CurrentBufferLength == 0)
            {
                CurrentBufferPosition = 0;
                return;
            }

            int offset = 0;
            if (bufferPosition != CurrentBufferLength)
            {
                offset = CurrentBufferLength - bufferPosition;
                Array.Copy(CurrentBuffer, bufferPosition, CurrentBuffer, 0, offset);
            }

            CurrentBufferLength = _source.Read(CurrentBuffer, offset, CurrentBuffer.Length - offset) + offset;
            CurrentBufferPosition = 0;
        }

        public bool Run()
        {
            bool modified = false;
            int lastWritten = CurrentBufferPosition;
            int writtenSinceFlush = lastWritten;

            while (CurrentBufferLength > 0)
            {
                int token;
                int posedPosition = CurrentBufferPosition;

                if (CurrentBufferLength == CurrentBuffer.Length && CurrentBufferLength - CurrentBufferPosition < _trie.MinLength)
                {
                    int writeCount = CurrentBufferPosition - lastWritten;

                    if (writeCount > 0)
                    {
                        _target.Write(CurrentBuffer, lastWritten, writeCount);
                        writtenSinceFlush += writeCount;
                    }

                    AdvanceBuffer(CurrentBufferPosition);
                    lastWritten = 0;
                    posedPosition = 0;
                }

                IOperation op = _trie.GetOperation(CurrentBuffer, CurrentBufferLength, ref posedPosition, out token);

                if (op != null)
                {
                    int writeCount = CurrentBufferPosition - lastWritten;

                    if (writeCount > 0)
                    {
                        _target.Write(CurrentBuffer, lastWritten, writeCount);
                        writtenSinceFlush += writeCount;
                    }

                    CurrentBufferPosition = posedPosition;
                    writtenSinceFlush += op.HandleMatch(this, CurrentBufferLength, ref posedPosition, token, _target);
                    CurrentBufferPosition = posedPosition;
                    lastWritten = posedPosition;
                    modified = true;
                }
                else
                {
                    ++CurrentBufferPosition;
                }

                if (CurrentBufferPosition == CurrentBufferLength)
                {
                    int writeCount = CurrentBufferPosition - lastWritten;

                    if (writeCount > 0)
                    {
                        _target.Write(CurrentBuffer, lastWritten, writeCount);
                        writtenSinceFlush += writeCount;
                    }

                    AdvanceBuffer(CurrentBufferPosition);
                    lastWritten = 0;
                }

                if (writtenSinceFlush >= _flushThreshold)
                {
                    writtenSinceFlush = 0;
                    _target.Flush();
                }
            }

            if (lastWritten < CurrentBufferPosition)
            {
                int writeCount = CurrentBufferPosition - lastWritten;

                if (writeCount > 0)
                {
                    _target.Write(CurrentBuffer, lastWritten, writeCount);
                }
            }

            _target.Flush();
            return modified;
        }

        /// <remarks>http://www.unicode.org/faq/utf_bom.html</remarks>
        private static Encoding DetectEncoding(byte[] buffer, int currentBufferLength, out byte[] bom)
        {
            if (currentBufferLength == 0)
            {
                //File is zero length - pick something
                bom = new byte[0];
                return Encoding.UTF8;
            }

            if (currentBufferLength >= 4)
            {
                if (buffer[0] == 0x00 && buffer[1] == 0x00 && buffer[2] == 0xFE && buffer[3] == 0xFF)
                {
                    //Big endian UTF-32
                    bom = new byte[] { 0x00, 0x00, 0xFE, 0xFF };
                    return Encoding.GetEncoding(12001);
                }

                if (buffer[0] == 0xFF && buffer[1] == 0xFE && buffer[2] == 0x00 && buffer[3] == 0x00)
                {
                    //Little endian UTF-32
                    bom = new byte[] { 0xFF, 0xFE, 0x00, 0x00 };
                    return Encoding.UTF32;
                }
            }

            if (currentBufferLength >= 3)
            {
                if (buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                {
                    //UTF-8
                    bom = new byte[] { 0xEF, 0xBB, 0xBF };
                    return Encoding.UTF8;
                }
            }

            if (currentBufferLength >= 2)
            {
                if (buffer[0] == 0xFE && buffer[1] == 0xFF)
                {
                    //Big endian UTF-16
                    bom = new byte[] { 0xFE, 0xFF };
                    return Encoding.BigEndianUnicode;
                }

                if (buffer[0] == 0xFF && buffer[1] == 0xFE)
                {
                    //Little endian UTF-16
                    bom = new byte[] { 0xFF, 0xFE };
                    return Encoding.Unicode;
                }
            }

            //Fallback to UTF-8
            bom = new byte[0];
            return Encoding.UTF8;
        }

        public void SeekBackUntil(SimpleTrie match)
        {
            byte[] buffer = new byte[match.MaxLength];
            while (_target.Position > 0)
            {
                if (_target.Position < buffer.Length)
                {
                    _target.Position = 0;
                }
                else
                {
                    _target.Position -= buffer.Length;
                }

                int nRead = _target.Read(buffer, 0, buffer.Length);

                for (int i = nRead - match.MinLength; i >= 0; --i)
                {
                    int token;
                    if (match.GetOperation(buffer, nRead, ref i, out token))
                    {
                        _target.Position -= nRead - i;
                        _target.SetLength(_target.Position);
                        return;
                    }
                }

                //Back up the amount we already read to get a new window of data in
                if (_target.Position < buffer.Length)
                {
                    _target.Position = 0;
                }
                else
                {
                    _target.Position -= buffer.Length;
                }
            }

            if (_target.Position == 0)
            {
                _target.SetLength(0);
            }
        }

        public void SeekBackWhile(SimpleTrie match)
        {
            byte[] buffer = new byte[match.MaxLength];
            while (_target.Position > 0)
            {
                if (_target.Position < buffer.Length)
                {
                    _target.Position = 0;
                }
                else
                {
                    _target.Position -= buffer.Length;
                }

                int nRead = _target.Read(buffer, 0, buffer.Length);
                bool anyMatch = false;
                int token = -1;
                int i = nRead - match.MinLength;

                for (; i >= 0; --i)
                {
                    if (match.GetOperation(buffer, nRead, ref i, out token))
                    {
                        i -= match.TokenLength[token];
                        anyMatch = true;
                        break;
                    }
                }

                if (!anyMatch || (token != -1 && i + match.TokenLength[token] != nRead))
                {
                    return;
                }

                //Back up the amount we already read to get a new window of data in
                if (_target.Position < buffer.Length)
                {
                    _target.Position = 0;
                }
                else
                {
                    _target.Position -= buffer.Length;
                }
            }

            if (_target.Position == 0)
            {
                _target.SetLength(0);
            }
        }

        public void SeekForwardThrough(SimpleTrie match, ref int bufferLength, ref int currentBufferPosition)
        {
            while (bufferLength > match.MinLength)
            {
                for (; currentBufferPosition < bufferLength - match.MinLength + 1; ++currentBufferPosition)
                {
                    if (bufferLength == 0)
                    {
                        currentBufferPosition = 0;
                        return;
                    }

                    int token;
                    if (match.GetOperation(CurrentBuffer, bufferLength, ref currentBufferPosition, out token))
                    {
                        return;
                    }
                }

                AdvanceBuffer(bufferLength - match.MaxLength + 1);
                currentBufferPosition = CurrentBufferPosition;
                bufferLength = CurrentBufferLength;
            }

            //Ran out of places to check and haven't reached the actual match, consume all the way to the end
            currentBufferPosition = bufferLength;
        }

        public void SeekForwardWhile(SimpleTrie match, ref int bufferLength, ref int currentBufferPosition)
        {
            while (bufferLength > match.MinLength)
            {
                while(currentBufferPosition < bufferLength - match.MinLength + 1)
                {
                    if (bufferLength == 0)
                    {
                        currentBufferPosition = 0;
                        return;
                    }

                    int token;
                    if (!match.GetOperation(CurrentBuffer, bufferLength, ref currentBufferPosition, out token))
                    {
                        return;
                    }
                }

                AdvanceBuffer(currentBufferPosition);
                currentBufferPosition = CurrentBufferPosition;
                bufferLength = CurrentBufferLength;
            }
        }
    }
}