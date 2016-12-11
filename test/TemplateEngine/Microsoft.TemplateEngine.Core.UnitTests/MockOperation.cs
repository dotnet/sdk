using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Core.UnitTests
{
    internal class MockOperationProvider : IOperationProvider
    {
        private readonly MockOperation _operation;

        public MockOperationProvider(MockOperation operation)
        {
            _operation = operation;
        }

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            return _operation;
        }
    }

    internal class MockOperation : IOperation
    {
        public delegate int MatchHandler(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target);
        private readonly MatchHandler _onMatch;

        public MockOperation(string id, MatchHandler onMatch, params byte[][] tokens)
        {
            Tokens = tokens;
            Id = id;
            _onMatch = onMatch;
        }

        public IReadOnlyList<byte[]> Tokens { get; }

        public string Id { get; }

        public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
        {
            return _onMatch?.Invoke(processor, bufferLength, ref currentBufferPosition, token, target) ?? 0;
        }

        public IOperationProvider Provider => new MockOperationProvider(this);
    }
}