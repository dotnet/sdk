using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateEngine.Core.Operations
{
    // TODO: Determine how to reset this if the stream is lead token heavy.
    // Or better yet force balancing, in which case nothing needs to be done.
    // but that requires a lookahead.
    //
    // If (when) more general use cases are found for this type of thing, this class will
    // probably need to be reworked to deal with more generalized balancing
    // and more generalized actions.

    public class BalancedNesting : IOperationProvider
    {
        public static readonly string OperationName = "balancednesting";

        private readonly string _startToken;
        private readonly string _realEndToken;
        private readonly string _pseudoEndToken;
        private readonly string _id;
        private readonly string _resetFlag;

        public BalancedNesting(string startToken, string realEndToken, string pseudoEndToken, string id, string resetFlag)
        {
            _startToken = startToken;
            _realEndToken = realEndToken;
            _pseudoEndToken = pseudoEndToken;
            _id = id;
            _resetFlag = resetFlag;
        }

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            byte[] startToken = encoding.GetBytes(_startToken);
            byte[] realEndToken = encoding.GetBytes(_realEndToken);
            byte[] pseudoEndToken = encoding.GetBytes(_pseudoEndToken);

            return new Impl(startToken, realEndToken, pseudoEndToken, _id, _resetFlag);
        }

        private class Impl : IOperation
        {
            private readonly byte[] _startToken;
            private readonly byte[] _realEndToken;
            private readonly byte[] _psuedoEndToken;
            private readonly string _id;
            private readonly string _resetFlag;
            private int _depth;

            // the order they're added to this.Tokens in the constructor must be the same as this!
            private const int StartTokenIndex = 0;
            private const int RealEndTokenIndex = 1;
            private const int PseudoEndTokenIndex = 2;

            public Impl(byte[] start, byte[] realEnd, byte[] pseudoEnd, string id, string resetFlag)
            {
                _startToken = start;
                _realEndToken = realEnd;
                _psuedoEndToken = pseudoEnd;
                _id = id;
                _resetFlag = resetFlag;
                Tokens = new[] { _startToken, _realEndToken, _psuedoEndToken };
                _depth = 0;
            }

            public string Id => _id;

            public IReadOnlyList<byte[]> Tokens { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                // check if this operation has been reset. If so, set _depth = 0
                // this fixes the reset problem, but not the trailing unbalanced pseduo comment problem, i.e. it won't turn this:
                //      <!-- <!-- comment -- >
                // into this:
                //      <!-- <!-- comment -->
                bool resetFlagValue;
                if (processor.Config.Flags.TryGetValue(_resetFlag, out resetFlagValue) && resetFlagValue)
                {
                    processor.Config.Flags.Remove(_resetFlag);
                    _depth = 0;
                }

                if (token == StartTokenIndex)
                {
                    ++_depth;
                }
                else if (token == RealEndTokenIndex || token == PseudoEndTokenIndex)
                {
                    --_depth;
                }

                if (_depth < 0)
                {
                    // TODO: determine a better way to deal with this.
                    // The reset operation should limit the scope of the problem.
                    // The depth-zero pseudo-comment is the only one that will be "fixed".
                    // But this could be changed to also fix any where depth < 0.
                    EngineEnvironmentSettings.Host.LogMessage($"Balanced nesting depth < 0. CurrentBufferPosition = {currentBufferPosition}");
                }

                if (_depth == 0 && token == PseudoEndTokenIndex)
                {
                    target.Write(_realEndToken, 0, _realEndToken.Length);
                    return _psuedoEndToken.Length;  // the source buffer needs to skip over this token.
                }
                else
                {
                    target.Write(Tokens[token], 0, Tokens[token].Length);
                }

                return 0;
            }
        }
    }
}
