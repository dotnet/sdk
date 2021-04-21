// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Matching;

namespace Microsoft.TemplateEngine.Core.Util
{
    public class ProcessorState : IProcessorState
    {
        private static readonly ConcurrentDictionary<IReadOnlyList<IOperationProvider>, Dictionary<Encoding, Trie<OperationTerminal>>> TrieLookup = new ConcurrentDictionary<IReadOnlyList<IOperationProvider>, Dictionary<Encoding, Trie<OperationTerminal>>>();
        private static readonly ConcurrentDictionary<IReadOnlyList<IOperationProvider>, List<string>> OperationsToExplicitlySetOnByDefault = new ConcurrentDictionary<IReadOnlyList<IOperationProvider>, List<string>>();
        private readonly Stream _target;
        private readonly TrieEvaluator<OperationTerminal> _trie;
        private readonly int _flushThreshold;
        private Stream _source;
        private byte[] _bom;
        private bool _isBomNeeded;
        private Encoding _encoding;

        public ProcessorState(Stream source, Stream target, int bufferSize, int flushThreshold, IEngineConfig config, IReadOnlyList<IOperationProvider> operationProviders)
        {
            if (source.CanSeek)
            {
                try
                {
                    if (source.Length < bufferSize)
                    {
                        bufferSize = (int)source.Length;
                    }
                }
                catch
                {
                    //The stream may not support getting the length property (in NetworkStream for instance, which throw a NotSupportedException), suppress any errors in
                    //  accessing the property and continue with the specified buffer size
                }
            }
            //Buffer has to be at least as large as the largest BOM we could expect
            else if (bufferSize < 4)
            {
                bufferSize = 4;
            }

            _source = source;
            _target = target;
            Config = config;
            _flushThreshold = flushThreshold;
            CurrentBuffer = new byte[bufferSize];
            CurrentBufferLength = source.Read(CurrentBuffer, 0, CurrentBuffer.Length);

            Encoding encoding = EncodingUtil.Detect(CurrentBuffer, CurrentBufferLength, out _bom);
            Encoding = encoding;
            CurrentBufferPosition = _bom.Length;
            CurrentSequenceNumber = _bom.Length;
            if (_bom.Length > 0)
            {
                _isBomNeeded = true;
            }

            bool explicitOnConfigurationRequired = false;
            Dictionary<Encoding, Trie<OperationTerminal>> byEncoding = TrieLookup.GetOrAdd(operationProviders, x => new Dictionary<Encoding, Trie<OperationTerminal>>());
            List<string> turnOnByDefault = OperationsToExplicitlySetOnByDefault.GetOrAdd(operationProviders, x =>
            {
                explicitOnConfigurationRequired = true;
                return new List<string>();
            });

            if (!byEncoding.TryGetValue(encoding, out Trie<OperationTerminal> trie))
            {
                trie = new Trie<OperationTerminal>();

                for (int i = 0; i < operationProviders.Count; ++i)
                {
                    IOperation op = operationProviders[i].GetOperation(encoding, this);

                    if (op != null)
                    {
                        for (int j = 0; j < op.Tokens.Count; ++j)
                        {
                            if (op.Tokens[j] != null)
                            {
                                trie.AddPath(op.Tokens[j].Value, new OperationTerminal(op, j, op.Tokens[j].Length, op.Tokens[j].Start, op.Tokens[j].End));
                            }
                        }

                        if (explicitOnConfigurationRequired && op.IsInitialStateOn && !string.IsNullOrEmpty(op.Id))
                        {
                            turnOnByDefault.Add(op.Id);
                        }
                    }
                }

                byEncoding[encoding] = trie;
            }

            foreach (string state in turnOnByDefault)
            {
                config.Flags[state] = true;
            }

            _trie = new TrieEvaluator<OperationTerminal>(trie);

            if (bufferSize < _trie.MaxLength + 1)
            {
                byte[] tmp = new byte[_trie.MaxLength + 1];
                Buffer.BlockCopy(CurrentBuffer, CurrentBufferPosition, tmp, 0, CurrentBufferLength - CurrentBufferPosition);
                int nRead = _source.Read(tmp, CurrentBufferLength - CurrentBufferPosition, tmp.Length - CurrentBufferLength);
                CurrentBuffer = tmp;
                CurrentBufferLength += nRead - _bom.Length;
                CurrentBufferPosition = 0;
                CurrentSequenceNumber = 0;
            }
        }

        public IEngineConfig Config { get; }

        public byte[] CurrentBuffer { get; }

        public int CurrentBufferLength { get; private set; }

        public int CurrentBufferPosition { get; private set; }

        public int CurrentSequenceNumber { get; private set; }

        public IEncodingConfig EncodingConfig { get; private set; }

        public Encoding Encoding
        {
            get { return _encoding; }
            set
            {
                _encoding = value;
                EncodingConfig = new EncodingConfig(Config, _encoding);
            }
        }

        public bool AdvanceBuffer(int bufferPosition)
        {
            if (CurrentBufferLength == 0 || bufferPosition == 0)
            {
                return false;
            }

            //The number of bytes away from the current buffer position being
            //  retargeted to the buffer head
            int netMove = bufferPosition - CurrentBufferPosition;
            //Since the CurrentSequenceNumber and CurrentBufferPosition are
            //  different mappings over the same value, the same net move
            //  applies to the current sequence number
            CurrentSequenceNumber += netMove;
            //Calculate the number of bytes at the end of the buffer that
            //  should be preserved
            int bytesToPreserveInBuffer = CurrentBufferLength - bufferPosition;

            if (CurrentBufferLength < CurrentBuffer.Length && bytesToPreserveInBuffer == 0)
            {
                CurrentBufferLength = 0;
                CurrentBufferPosition = 0;
                return false;
            }

            //If we actually have to preserve any data, shift it to the start
            if (bytesToPreserveInBuffer > 0)
            {
                //Shift the relevant number of bytes back to the head of the buffer
                Buffer.BlockCopy(CurrentBuffer, bufferPosition, CurrentBuffer, 0, bytesToPreserveInBuffer);
            }

            //Fill the remaining spaces in the buffer with new data, save how
            //  many we've read for recalculating the new effective buffer size
            int nRead = _source.Read(CurrentBuffer, bytesToPreserveInBuffer, CurrentBufferLength - bytesToPreserveInBuffer);
            CurrentBufferLength = bytesToPreserveInBuffer + nRead;

            //The new buffer position is set to point at the byte that buffer
            //  position pointed at (which is now at the head of the buffer)
            CurrentBufferPosition = 0;

            return true;
        }

        public bool Run()
        {
            int nextSequenceNumberThatCouldBeWritten = CurrentSequenceNumber;
            int bytesWrittenSinceLastFlush = 0;
            bool anyOperationsExecuted = false;

            while (true)
            {
                //Loop until we run out of data in the buffer
                while (CurrentBufferPosition < CurrentBufferLength)
                {
                    int posedPosition = CurrentSequenceNumber;
                    bool skipAdvanceBuffer = false;
                    if (_trie.Accept(CurrentBuffer[CurrentBufferPosition], ref posedPosition, out TerminalLocation<OperationTerminal> terminal))
                    {
                        IOperation operation = terminal.Terminal.Operation;
                        int matchLength = terminal.Terminal.End - terminal.Terminal.Start + 1;
                        int handoffBufferPosition = CurrentBufferPosition + matchLength - (CurrentSequenceNumber - terminal.Location);

                        if (terminal.Location > nextSequenceNumberThatCouldBeWritten)
                        {
                            int toWrite = terminal.Location - nextSequenceNumberThatCouldBeWritten;
                            //Console.WriteLine("UnmatchedBlock");
                            //string text = System.Text.Encoding.UTF8.GetString(CurrentBuffer, handoffBufferPosition - toWrite - matchLength, toWrite).Replace("\0", "\\0");
                            //Console.WriteLine(text);
                            if (_isBomNeeded)
                            {
                                _target.Write(_bom, 0, _bom.Length);
                                _isBomNeeded = false;
                            }
                            _target.Write(CurrentBuffer, handoffBufferPosition - toWrite - matchLength, toWrite);
                            bytesWrittenSinceLastFlush += toWrite;
                            nextSequenceNumberThatCouldBeWritten = posedPosition - matchLength + 1;
                        }

                        if (operation.Id == null || (Config.Flags.TryGetValue(operation.Id, out bool opEnabledFlag) && opEnabledFlag))
                        {
                            CurrentSequenceNumber += handoffBufferPosition - CurrentBufferPosition;
                            CurrentBufferPosition = handoffBufferPosition;
                            posedPosition = handoffBufferPosition;
                            int bytesWritten = operation.HandleMatch(this, CurrentBufferLength, ref posedPosition, terminal.Terminal.Token, _target);
                            bytesWrittenSinceLastFlush += bytesWritten;

                            CurrentSequenceNumber += posedPosition - CurrentBufferPosition;
                            CurrentBufferPosition = posedPosition;
                            nextSequenceNumberThatCouldBeWritten = CurrentSequenceNumber;
                            skipAdvanceBuffer = true;
                            anyOperationsExecuted = true;
                        }
                        else
                        {
                            int oldSequenceNumber = CurrentSequenceNumber;
                            CurrentSequenceNumber = terminal.Location + terminal.Terminal.End + 1;
                            CurrentBufferPosition += CurrentSequenceNumber - oldSequenceNumber;
                        }

                        if (bytesWrittenSinceLastFlush >= _flushThreshold)
                        {
                            _target.Flush();
                            bytesWrittenSinceLastFlush = 0;
                        }
                    }

                    if (!skipAdvanceBuffer)
                    {
                        ++CurrentSequenceNumber;
                        ++CurrentBufferPosition;
                    }
                }

                //Calculate the sequence number at the head of the buffer
                int headSequenceNumber = CurrentSequenceNumber - CurrentBufferPosition;

                // Calculate the buffer position to advance to. It can not be negative.
                // Taking a maximum is a workaround for out-of-sync _trie.OldestRequiredSequenceNumber which may appear near EOF.
                int bufferPositionToAdvanceTo = Math.Max(_trie.OldestRequiredSequenceNumber - headSequenceNumber, 0);
                int numberOfUncommittedBytesBeforeThePositionToAdvanceTo = _trie.OldestRequiredSequenceNumber - nextSequenceNumberThatCouldBeWritten;

                //If we'd advance data out of the buffer that hasn't been
                //  handled already, write it out
                if (numberOfUncommittedBytesBeforeThePositionToAdvanceTo > 0)
                {
                    int toWrite = numberOfUncommittedBytesBeforeThePositionToAdvanceTo;
                    // Console.WriteLine("AdvancePreserve");
                    // Console.WriteLine($"nextSequenceNumberThatCouldBeWritten {nextSequenceNumberThatCouldBeWritten}");
                    // Console.WriteLine($"headSequenceNumber {headSequenceNumber}");
                    // Console.WriteLine($"bufferPositionToAdvanceTo {bufferPositionToAdvanceTo}");
                    // Console.WriteLine($"numberOfUncommittedBytesBeforeThePositionToAdvanceTo {numberOfUncommittedBytesBeforeThePositionToAdvanceTo}");
                    // Console.WriteLine($"CurrentBufferPosition {CurrentBufferPosition}");
                    // Console.WriteLine($"CurrentBufferLength {CurrentBufferLength}");
                    // Console.WriteLine($"CurrentBuffer.Length {CurrentBuffer.Length}");
                    // string text = System.Text.Encoding.UTF8.GetString(CurrentBuffer, bufferPositionToAdvanceTo - toWrite, toWrite).Replace("\0", "\\0");
                    // Console.WriteLine(text);
                    _target.Write(CurrentBuffer, bufferPositionToAdvanceTo - toWrite, toWrite);
                    bytesWrittenSinceLastFlush += toWrite;
                    nextSequenceNumberThatCouldBeWritten = _trie.OldestRequiredSequenceNumber;
                }

                //We ran out of data in the buffer, so attempt to advance
                //  if we fail,
                if (!AdvanceBuffer(bufferPositionToAdvanceTo))
                {
                    int posedPosition = CurrentSequenceNumber;
                    _trie.FinalizeMatchesInProgress(ref posedPosition, out TerminalLocation<OperationTerminal> terminal);

                    while (terminal != null)
                    {
                        IOperation operation = terminal.Terminal.Operation;
                        int matchLength = terminal.Terminal.End - terminal.Terminal.Start + 1;
                        int handoffBufferPosition = CurrentBufferPosition + matchLength - (CurrentSequenceNumber - terminal.Location);

                        if (terminal.Location > nextSequenceNumberThatCouldBeWritten)
                        {
                            int toWrite = terminal.Location - nextSequenceNumberThatCouldBeWritten;
                            // Console.WriteLine("TailUnmatchedBlock");
                            // string text = System.Text.Encoding.UTF8.GetString(CurrentBuffer, handoffBufferPosition - toWrite - matchLength, toWrite).Replace("\0", "\\0");
                            // Console.WriteLine(text);
                            _target.Write(CurrentBuffer, handoffBufferPosition - toWrite - matchLength, toWrite);
                            bytesWrittenSinceLastFlush += toWrite;
                            nextSequenceNumberThatCouldBeWritten = terminal.Location;
                        }

                        if (operation.Id == null || (Config.Flags.TryGetValue(operation.Id, out bool opEnabledFlag) && opEnabledFlag))
                        {
                            CurrentSequenceNumber += handoffBufferPosition - CurrentBufferPosition;
                            CurrentBufferPosition = handoffBufferPosition;
                            posedPosition = handoffBufferPosition;
                            int bytesWritten = operation.HandleMatch(this, CurrentBufferLength, ref posedPosition, terminal.Terminal.Token, _target);
                            bytesWrittenSinceLastFlush += bytesWritten;

                            CurrentSequenceNumber += posedPosition - CurrentBufferPosition;
                            CurrentBufferPosition = posedPosition;
                            nextSequenceNumberThatCouldBeWritten = CurrentSequenceNumber;
                            anyOperationsExecuted = true;
                        }
                        else
                        {
                            int oldSequenceNumber = CurrentSequenceNumber;
                            CurrentSequenceNumber = terminal.Location + terminal.Terminal.End + 1;
                            CurrentBufferPosition += CurrentSequenceNumber - oldSequenceNumber;
                        }

                        _trie.FinalizeMatchesInProgress(ref posedPosition, out terminal);
                    }

                    break;
                }
            }

            int endSequenceNumber = CurrentSequenceNumber - CurrentBufferPosition + CurrentBufferLength;
            if (endSequenceNumber > nextSequenceNumberThatCouldBeWritten)
            {
                int toWrite = endSequenceNumber - nextSequenceNumberThatCouldBeWritten;
                // Console.WriteLine("LastBlock");
                // string text = System.Text.Encoding.UTF8.GetString(CurrentBuffer, CurrentBufferLength - toWrite, toWrite).Replace("\0", "\\0");
                // Console.WriteLine(text);
                _target.Write(CurrentBuffer, CurrentBufferLength - toWrite, toWrite);
            }

            _target.Flush();
            return anyOperationsExecuted;
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
                for (int i = nRead - match.MinLength; i >= 0; --i)
                {
                    int token;
                    int ic = i;
                    if (match.GetOperation(buffer, nRead, ref ic, out token) && ic >= bestPos)
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

        public void SeekForwardThrough(ITokenTrie trie, ref int bufferLength, ref int currentBufferPosition)
        {
            BaseSeekForward(trie, ref bufferLength, ref currentBufferPosition, true);
        }

        public void SeekForwardUntil(ITokenTrie trie, ref int bufferLength, ref int currentBufferPosition)
        {
            BaseSeekForward(trie, ref bufferLength, ref currentBufferPosition, false);
        }

        public void SeekForwardWhile(ITokenTrie trie, ref int bufferLength, ref int currentBufferPosition)
        {
            while (bufferLength > trie.MinLength)
            {
                while (currentBufferPosition < bufferLength - trie.MinLength + 1)
                {
                    if (bufferLength == 0)
                    {
                        currentBufferPosition = 0;
                        return;
                    }

                    int token;
                    if (!trie.GetOperation(CurrentBuffer, bufferLength, ref currentBufferPosition, out token))
                    {
                        return;
                    }
                }

                AdvanceBuffer(currentBufferPosition);
                currentBufferPosition = CurrentBufferPosition;
                bufferLength = CurrentBufferLength;
            }
        }

        public void Inject(Stream staged)
        {
            _source = new CombinedStream(staged, _source, inner => _source = inner);
            CurrentBufferLength = _source.Read(CurrentBuffer, 0, CurrentBufferLength);
            CurrentBufferPosition = 0;
        }

        private void BaseSeekForward(ITokenTrie match, ref int bufferLength, ref int currentBufferPosition, bool consumeToken)
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

                    if (match.GetOperation(CurrentBuffer, bufferLength, ref currentBufferPosition, false, out int token))
                    {
                        if (!consumeToken)
                        {
                            currentBufferPosition -= match.Tokens[token].Length;
                        }

                        return;
                    }
                }
            }

            //Ran out of places to check and haven't reached the actual match, consume all the way to the end
            currentBufferPosition = bufferLength;
        }
    }
}
