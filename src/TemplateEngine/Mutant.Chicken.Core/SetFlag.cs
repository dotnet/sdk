using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Mutant.Chicken.Core
{
    public class SetFlag : IOperationProvider
    {
        public string Name { get; }

        public string On { get; }

        public string Off { get; }

        public bool? Default { get; }

        public SetFlag(string name, string on, string off, bool? @default = null)
        {
            Name = name;
            On = on;
            Off = off;
            Default = @default;
        }

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            byte[][] tokens = new byte[][]
            {
                encoding.GetBytes(On),
                encoding.GetBytes(Off)
            };

            if (Default.HasValue)
            {
                processorState.Config.Flags[Name] = Default.Value;
            }

            return new Impl(this, tokens);
        }

        private class Impl : IOperation
        {
            private readonly SetFlag _owner;

            public Impl(SetFlag owner, IReadOnlyList<byte[]> tokens)
            {
                _owner = owner;
                Tokens = tokens;
            }

            public IReadOnlyList<byte[]> Tokens { get; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                bool flag;
                if (processor.Config.Flags.TryGetValue("flags", out flag) && !flag)
                {
                    byte[] tokenValue = Tokens[token];
                    target.Write(tokenValue, 0, tokenValue.Length);
                    processor.Config.Flags[_owner.Name] = token == 0;
                    return tokenValue.Length;
                }

                processor.Config.Flags[_owner.Name] = token == 0;
                return 0;
            }
        }
    }
}
