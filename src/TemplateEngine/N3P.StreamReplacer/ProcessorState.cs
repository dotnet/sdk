using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace N3P.StreamReplacer
{
    internal class ProcessorState : IProcessorState
    {
        private readonly int _flushThreshold;
        private readonly Stream _source;
        private readonly Stream _target;
        private readonly Trie _trie;
        private Encoding _encoding;

        public ProcessorState(Stream source, Stream target, int bufferSize, int flushThreshold, IReadOnlyList<IOperationProvider> operationProviders)
        {
            _source = source;
            _target = target;
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
                operations[i] = operationProviders[i].GetOperation(encoding);
            }

            _trie = Trie.Create(operations);
        }

        public byte[] CurrentBuffer { get; }

        public int CurrentBufferLength { get; private set; }

        public int CurrentBufferPosition { get; private set; }

        public Encoding Encoding
        {
            get { return _encoding; }
            set
            {
                _encoding = value;
                CalculateEOLMarkers();
            }
        }

        public SimpleTrie EOLMarkers { get; private set; }

        public IReadOnlyList<byte[]> EOLTails { get; private set; }

        public int MaxEOLTailLength { get; private set; }

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

        public void ConsumeToEndOfLine(ref int bufferLength, ref int currentBufferPosition)
        {
            while (bufferLength > EOLMarkers.Length)
            {
                for (; currentBufferPosition < bufferLength - EOLMarkers.Length + 1; ++currentBufferPosition)
                {
                    if (bufferLength == 0)
                    {
                        currentBufferPosition = 0;
                        return;
                    }

                    int token;
                    if (EOLMarkers.GetOperation(CurrentBuffer, bufferLength, ref currentBufferPosition, out token))
                    {
                        return;
                    }
                }

                AdvanceBuffer(bufferLength - EOLMarkers.Length + 1);
                currentBufferPosition = CurrentBufferPosition;
                bufferLength = CurrentBufferLength;
            }
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

                if (CurrentBufferLength == CurrentBuffer.Length && CurrentBufferLength - CurrentBufferPosition < _trie.Length)
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

        public void TrimBackToPreviousEOL()
        {
            while (_target.Position > 0)
            {
                byte[] buffer = new byte[MaxEOLTailLength];
                _target.Position -= MaxEOLTailLength;
                _target.Read(buffer, 0, buffer.Length);

                for (int i = 0; i < EOLTails.Count; ++i)
                {
                    for (int j = 0; j <= buffer.Length - EOLTails[i].Length; ++j)
                    {
                        bool allMatch = true;
                        for (int k = 0; allMatch && k < EOLTails[i].Length; ++k)
                        {
                            if (EOLTails[i][k] != buffer[j + k])
                            {
                                allMatch = false;
                            }
                        }

                        if (allMatch)
                        {
                            _target.Position -= j;
                            _target.SetLength(_target.Position);
                            return;
                        }
                    }
                }

                //Back up the amount we already read to get a new window of data in
                _target.Position -= MaxEOLTailLength;
            }
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

        private void CalculateEOLMarkers()
        {
            SimpleTrie t = new SimpleTrie();
            t.AddToken(Encoding.GetBytes("\r\n"), 0);
            t.AddToken(Encoding.GetBytes("\n"), 1);
            t.AddToken(Encoding.GetBytes("\r"), 2);
            EOLMarkers = t;

            byte[] carriageReturn = Encoding.GetBytes("\r");
            byte[] newLine = Encoding.GetBytes("\n");

            EOLTails = new[]
            {
                carriageReturn,
                newLine
            };

            MaxEOLTailLength = Math.Max(carriageReturn.Length, newLine.Length);
        }
    }
}