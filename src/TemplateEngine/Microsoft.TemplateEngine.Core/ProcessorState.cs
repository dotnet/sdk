using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions.Engine;

namespace Microsoft.TemplateEngine.Core
{
    internal class ProcessorState : IProcessorState
    {
        private readonly int _flushThreshold;
        private readonly Stream _source;
        private readonly Stream _target;
        private readonly OperationTrie _trie;
        private Encoding _encoding;
        private static readonly Dictionary<IReadOnlyList<IOperationProvider>, Dictionary<Encoding, OperationTrie>> TrieLookup = new Dictionary<IReadOnlyList<IOperationProvider>, Dictionary<Encoding, OperationTrie>>();

        public ProcessorState(Stream source, Stream target, int bufferSize, int flushThreshold, IEngineConfig config, IReadOnlyList<IOperationProvider> operationProviders)
        {
            bool sizedToStream = false;

            //Buffer has to be at least as large as the largest BOM we could expect
            if (bufferSize < 4)
            {
                bufferSize = 4;
            }
            else
            {
                try
                {
                    if (source.Length < bufferSize)
                    {
                        sizedToStream = true;
                        bufferSize = (int) source.Length;
                    }
                }
                catch
                {
                    //The stream may not support getting the length property (in NetworkStream for instance, which throw a NotSupportedException), suppress any errors in
                    //  accessing the property and continue with the specified buffer size
                }
            }

            _source = source;
            _target = target;
            Config = config;
            _flushThreshold = flushThreshold;
            CurrentBuffer = new byte[bufferSize];
            CurrentBufferLength = source.Read(CurrentBuffer, 0, CurrentBuffer.Length);

            byte[] bom;
            Encoding encoding = EncodingUtil.Detect(CurrentBuffer, CurrentBufferLength, out bom);
            Encoding = encoding;
            CurrentBufferPosition = bom.Length;
            target.Write(bom, 0, bom.Length);

            Dictionary<Encoding, OperationTrie> byEncoding;
            if(!TrieLookup.TryGetValue(operationProviders, out byEncoding))
            {
                TrieLookup[operationProviders] = byEncoding = new Dictionary<Encoding, OperationTrie>();
            }

            if (!byEncoding.TryGetValue(encoding, out _trie))
            {
                List<IOperation> operations = new List<IOperation>(operationProviders.Count);

                for (int i = 0; i < operationProviders.Count; ++i)
                {
                    IOperation op = operationProviders[i].GetOperation(encoding, this);
                    if (op != null)
                    {
                        operations.Add(op);
                    }
                }

                byEncoding[encoding] = _trie = OperationTrie.Create(operations);
            }

            if (bufferSize < _trie.MaxLength && !sizedToStream)
            {
                byte[] tmp = new byte[_trie.MaxLength];
                Buffer.BlockCopy(CurrentBuffer, CurrentBufferPosition, tmp, 0, CurrentBufferLength - CurrentBufferPosition);
                int nRead = _source.Read(tmp, CurrentBufferLength - CurrentBufferPosition, tmp.Length - CurrentBufferLength);
                CurrentBuffer = tmp;
                CurrentBufferLength += nRead;
                CurrentBufferPosition = 0;
            }
        }

        public IEngineConfig Config { get; }

        public byte[] CurrentBuffer { get; }

        public int CurrentBufferLength { get; private set; }

        public int CurrentBufferPosition { get; private set; }

        public Encoding Encoding
        {
            get { return _encoding; }
            set
            {
                _encoding = value;
                EncodingConfig = new EncodingConfig(Config, _encoding);
            }
        }

        public IEncodingConfig EncodingConfig { get; private set; }

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

            if(CurrentBufferPosition == CurrentBufferLength)
            {
                AdvanceBuffer(CurrentBufferPosition);
                lastWritten = 0;
            }

            while (CurrentBufferLength > 0)
            {
                int token;
                int posedPosition = CurrentBufferPosition;

                if (CurrentBufferLength == CurrentBuffer.Length && CurrentBufferLength - CurrentBufferPosition < _trie.MaxLength)
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

                    try
                    {
                        writtenSinceFlush += op.HandleMatch(this, CurrentBufferLength, ref posedPosition, token, _target);
                    }
                    catch(Exception ex)
                    {
                        throw new Exception($"Error running handler {op} at position {CurrentBufferPosition} in {Encoding.EncodingName} bytes of {Encoding.GetString(CurrentBuffer, 0, CurrentBufferLength)}.\n\nStart: {Encoding.GetString(CurrentBuffer, CurrentBufferPosition, CurrentBufferLength - CurrentBufferPosition)} \n\nCheck InnerException for details.", ex);
                    }

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

        public void SeekBackUntil(ITokenTrie match)
        {
            SeekBackUntil(match, false);
        }

        public void SeekBackUntil(ITokenTrie match, bool consume)
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
                int best = -1;
                int bestPos = -1;
                for (int i = nRead - match.MaxLength; i >= 0; --i)
                {
                    int token;
                    int ic = i;
                    if (match.GetOperation(buffer, nRead, ref ic, out token))
                    {
                        bestPos = ic;
                        best = token;
                    }
                }

                if (best != -1)
                {
                    _target.Position -= nRead - bestPos + (consume ? match.TokenLength[best] : 0);
                    _target.SetLength(_target.Position);
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

        public void SeekBackWhile(ITokenTrie match)
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
                    _target.SetLength(_target.Position);
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

        public void SeekForwardThrough(ITokenTrie match, ref int bufferLength, ref int currentBufferPosition)
        {
            while (bufferLength >= match.MinLength)
            {
                //Try to get at least the max length of the tree into the buffer
                if (bufferLength - currentBufferPosition < match.MaxLength)
                {
                    AdvanceBuffer(currentBufferPosition);
                    currentBufferPosition = CurrentBufferPosition;
                    bufferLength = CurrentBufferLength;
                }

                int sz = bufferLength == CurrentBuffer.Length ? match.MaxLength : match.MinLength;

                for (; currentBufferPosition < bufferLength - sz + 1; ++currentBufferPosition)
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
            }

            //Ran out of places to check and haven't reached the actual match, consume all the way to the end
            currentBufferPosition = bufferLength;
        }

        public void SeekForwardWhile(ITokenTrie match, ref int bufferLength, ref int currentBufferPosition)
        {
            while (bufferLength > match.MinLength)
            {
                while (currentBufferPosition < bufferLength - match.MinLength + 1)
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