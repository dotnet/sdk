// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Core.Matching
{
    public class TrieEvaluationDriver<T>
        where T : TerminalBase
    {
        private readonly TrieEvaluator<T> _evaluator;
        private int _sequenceNumber;

        public TrieEvaluationDriver(TrieEvaluator<T> trie)
        {
            _evaluator = trie;
        }

        public TerminalLocation<T> Evaluate(byte[] buffer, int bufferLength, bool isFinalBuffer, int lastNetBufferEffect, ref int bufferPosition)
        {
            TerminalLocation<T> terminal;
            _sequenceNumber += lastNetBufferEffect;
            int sequenceNumberToBufferPositionRelationship = _sequenceNumber - bufferPosition;
            int originalSequenceNumber = _sequenceNumber;

            if (lastNetBufferEffect != 0 || !_evaluator.TryGetNext(isFinalBuffer && bufferPosition >= bufferLength, ref _sequenceNumber, out terminal))
            {
                while (!_evaluator.Accept(buffer[bufferPosition], ref _sequenceNumber, out terminal))
                {
                    ++_sequenceNumber;
                    ++bufferPosition;

                    if (bufferPosition >= bufferLength)
                    {
                        if (!isFinalBuffer)
                        {
                            break;
                        }
                        else
                        {
                            _evaluator.FinalizeMatchesInProgress(ref _sequenceNumber, out terminal);
                            break;
                        }
                    }
                    originalSequenceNumber = _sequenceNumber;
                }
            }

            if (terminal != null)
            {
                terminal.Location -= sequenceNumberToBufferPositionRelationship;

                if (originalSequenceNumber > _sequenceNumber + 1)
                {
                    int expectedShift = terminal.Terminal.Length - terminal.Terminal.End - 1;

                    if (expectedShift == 0)
                    {
                        int actualShift = originalSequenceNumber - _sequenceNumber - 1;
                        int compensation = actualShift - expectedShift;
                        bufferPosition -= compensation;
                    }
                }
            }

            return terminal;
        }
    }
}
