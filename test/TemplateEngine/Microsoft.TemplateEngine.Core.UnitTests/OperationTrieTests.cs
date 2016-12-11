using System.Collections.Generic;
using System.IO;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Util;
using Xunit;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    public class OperationTrieTests
    {
        [Fact(DisplayName = nameof(VerifyOperationTrieFindsTokenAtStart))]
        public void VerifyOperationTrieFindsTokenAtStart()
        {
            OperationTrie trie = OperationTrie.Create(new IOperation[]
            {
                new MockOperation("Test1", null, new byte[] {1, 2, 3, 4}),
                new MockOperation("Test2", null, new byte[] {2, 3})
            });

            byte[] buffer = {1, 2, 3, 4, 5};
            int currentBufferPosition = 0;
            IOperation match = trie.GetOperation(buffer, buffer.Length, ref currentBufferPosition, out int
            token)
            ;

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
                new MockOperation("Test1", null, new byte[] {5, 2, 3, 4}),
                new MockOperation("Test2", null, new byte[] {4, 6}, new byte[] {2, 3})
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
                new MockOperation("Test1", null, new byte[] {5, 2, 3, 4}),
                new MockOperation("Test2", null, new byte[] {4, 5}, new byte[] {2, 3})
            });

            byte[] buffer = { 1, 2, 3, 4, 5 };
            int currentBufferPosition = 3;
            IOperation match = trie.GetOperation(buffer, buffer.Length, ref currentBufferPosition, out int token);

            Assert.NotNull(match);
            Assert.Equal("Test2", match.Id);
            Assert.Equal(0, token);
            Assert.Equal(buffer.Length, currentBufferPosition);
        }
    }
}
