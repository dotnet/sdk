// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Matching;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class OperationTrieTests
    {
        private delegate int MatchHandler(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target);

        [Fact(DisplayName = nameof(VerifyOperationTrieFindsTokenAtStart))]
        public void VerifyOperationTrieFindsTokenAtStart()
        {
            OperationTrie trie = OperationTrie.Create(new IOperation[]
            {
                new MockOperation("Test1", null, true, TokenConfig.LiteralToken(new byte[] { 1, 2, 3, 4 })),
                new MockOperation("Test2", null, true, TokenConfig.LiteralToken(new byte[] { 2, 3 }))
            });

            byte[] buffer = { 1, 2, 3, 4, 5 };
            int currentBufferPosition = 0;
            IOperation match = trie.GetOperation(buffer, buffer.Length, ref currentBufferPosition, out int token);

            Assert.NotNull(match);
            Assert.Equal("Test1", match.Id);
            Assert.Equal(0, token);
            Assert.Equal(4, currentBufferPosition);
        }

        [Fact(DisplayName = nameof(VerifyOperationTrieFindsTokenAfterStart))]
        public void VerifyOperationTrieFindsTokenAfterStart()
        {
            OperationTrie trie = OperationTrie.Create(new IOperation[]
            {
                new MockOperation("Test1", null, true, TokenConfig.LiteralToken(new byte[] { 5, 2, 3, 4 })),
                new MockOperation("Test2", null, true, TokenConfig.LiteralToken(new byte[] { 4, 6 }), TokenConfig.LiteralToken(new byte[] { 2, 3 }))
            });

            byte[] buffer = { 1, 2, 3, 4, 5 };
            int currentBufferPosition = 0;
            IOperation match = trie.GetOperation(buffer, buffer.Length, ref currentBufferPosition, out int token);

            Assert.Null(match);
            Assert.Equal(0, currentBufferPosition);
            currentBufferPosition = 1;
            match = trie.GetOperation(buffer, buffer.Length, ref currentBufferPosition, out token);

            Assert.NotNull(match);
            Assert.Equal("Test2", match.Id);
            Assert.Equal(1, token);
            Assert.Equal(3, currentBufferPosition);
        }

        [Fact(DisplayName = nameof(VerifyOperationTrieFindsTokenAtEnd))]
        public void VerifyOperationTrieFindsTokenAtEnd()
        {
            OperationTrie trie = OperationTrie.Create(new IOperation[]
            {
                new MockOperation("Test1", null, true, TokenConfig.LiteralToken(new byte[] { 5, 2, 3, 4 })),
                new MockOperation("Test2", null, true, TokenConfig.LiteralToken(new byte[] { 4, 5 }), TokenConfig.LiteralToken(new byte[] { 2, 3 }))
            });

            byte[] buffer = { 1, 2, 3, 4, 5 };
            int currentBufferPosition = 3;
            IOperation match = trie.GetOperation(buffer, buffer.Length, ref currentBufferPosition, out int token);

            Assert.NotNull(match);
            Assert.Equal("Test2", match.Id);
            Assert.Equal(0, token);
            Assert.Equal(buffer.Length, currentBufferPosition);
        }

        [Fact(DisplayName = nameof(VerifyLastInWinsForIdenticalMatching))]
        public void VerifyLastInWinsForIdenticalMatching()
        {
            OperationTrie trie = OperationTrie.Create(new IOperation[]
            {
                new MockOperation("TestOp1", null, true, TokenConfig.LiteralToken(new byte[] { 5, 5, 5 })),
                new MockOperation("TestOp2", null, true, TokenConfig.LiteralToken(new byte[] { 2, 3, 4, 5 })),
                new MockOperation("TestOp3", null, true, TokenConfig.LiteralToken(new byte[] { 7, 7, 7 })),
                new MockOperation("TestOp4", null, true, TokenConfig.LiteralToken(new byte[] { 9, 9, 9, 9 }), TokenConfig.LiteralToken(new byte[] { 2, 3, 4, 5 })),
            });

            byte[] buffer = { 9, 8, 9, 8, 7, 2, 3, 4, 5 };
            int currentBufferPosition = 0;
            IOperation match = trie.GetOperation(buffer, buffer.Length, ref currentBufferPosition, out int token);

            Assert.NotNull(match);
            Assert.Equal("TestOp4", match.Id);
            Assert.Equal(1, token);
            Assert.Equal(buffer.Length, currentBufferPosition);
        }

        private class MockOperation : IOperation
        {
            private readonly MatchHandler _onMatch;

            public MockOperation(string id, MatchHandler onMatch, bool initialState, params IToken[] tokens)
            {
                Tokens = tokens;
                Id = id;
                _onMatch = onMatch;
                IsInitialStateOn = initialState;
            }

            public IReadOnlyList<IToken> Tokens { get; }

            public string Id { get; }

            public bool IsInitialStateOn { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                return _onMatch?.Invoke(processor, bufferLength, ref currentBufferPosition, token, target) ?? 0;
            }
        }

        private class OperationTrie : Trie<OperationTerminal>
        {
            public static OperationTrie Create(IEnumerable<IOperation> operations)
            {
                OperationTrie trie = new OperationTrie();

                foreach (IOperation operation in operations)
                {
                    int tokenNumber = 0;
                    foreach (IToken token in operation.Tokens)
                    {
                        trie.AddPath(token.Value, new OperationTerminal(operation, tokenNumber++, token.Length, token.Start, token.End));
                    }
                }

                return trie;
            }

            public IOperation GetOperation(byte[] buffer, int bufferLength, ref int bufferPosition, out int token)
            {
                int originalPosition = bufferPosition;
                TrieEvaluator<OperationTerminal> evaluator = new TrieEvaluator<OperationTerminal>(this);
                int sn = originalPosition;

                for (; bufferPosition < bufferLength; ++bufferPosition)
                {
                    if (evaluator.Accept(buffer[bufferPosition], ref sn, out TerminalLocation<OperationTerminal> terminal))
                    {
                        if (terminal.Location == originalPosition)
                        {
                            bufferPosition -= sn - terminal.Location - terminal.Terminal.End;
                            token = terminal.Terminal.Token;
                            return terminal.Terminal.Operation;
                        }
                        else
                        {
                            token = -1;
                            bufferPosition = originalPosition;
                            return null;
                        }
                    }

                    ++sn;
                }

                if (bufferPosition == bufferLength)
                {
                    evaluator.FinalizeMatchesInProgress(ref sn, out TerminalLocation<OperationTerminal> terminal);

                    if (terminal != null)
                    {
                        bufferPosition -= sn - terminal.Location - terminal.Terminal.End;
                        token = terminal.Terminal.Token;
                        return terminal.Terminal.Operation;
                    }
                }

                bufferPosition = originalPosition;
                token = -1;
                return null;
            }
        }
    }
}
