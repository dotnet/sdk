using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockOperation : IOperation
    {
        public delegate int MatchHandler(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target);
        private readonly MatchHandler _onMatch;

        public MockOperation(string id, MatchHandler onMatch, params byte[][] tokens)
            : this(id, onMatch, tokens.Select(token => TokenConfig.LiteralToken(token)).ToArray())
        {
        }

        public MockOperation(string id, MatchHandler onMatch, params IToken[] tokens)
        {
            Tokens = tokens;
            Id = id;
            _onMatch = onMatch;
        }

        public IReadOnlyList<IToken> Tokens { get; }

        public string Id { get; }

        public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
        {
            return _onMatch?.Invoke(processor, bufferLength, ref currentBufferPosition, token, target) ?? 0;
        }

        public IOperationProvider Provider => new MockOperationProvider(this);
    }
}