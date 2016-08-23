using System.Collections.Generic;
using System.IO;
using System.Text;
using Microsoft.TemplateEngine.Abstractions.Engine;

namespace Microsoft.TemplateEngine.Core
{
    public class SetFlag : IOperationProvider
    {
        private readonly string _id;

        public string Name { get; }

        public string On { get; }

        public string Off { get; }

        public bool? Default { get; }

        public string OnNoEmit { get; }

        public string OffNoEmit { get; }

        public SetFlag(string name, string on, string off, string onNoEmit, string offNoEmit, string id, bool? @default = null)
        {
            Name = name;
            On = on;
            Off = off;
            OnNoEmit = onNoEmit;
            OffNoEmit = offNoEmit;
            Default = @default;
            _id = id;
        }

        public IOperation GetOperation(Encoding encoding, IProcessorState processorState)
        {
            byte[][] tokens = new byte[][]
            {
                encoding.GetBytes(On),
                encoding.GetBytes(Off),
                encoding.GetBytes(OnNoEmit),
                encoding.GetBytes(OffNoEmit)
            };

            if (Default.HasValue)
            {
                processorState.Config.Flags[Name] = Default.Value;
            }

            return new Impl(this, tokens, _id);
        }

        private class Impl : IOperation
        {
            private readonly SetFlag _owner;

            public Impl(SetFlag owner, IReadOnlyList<byte[]> tokens, string id)
            {
                _owner = owner;
                Tokens = tokens;
                Id = id;
            }

            public IReadOnlyList<byte[]> Tokens { get; }

            public string Id { get; private set; }

            public int HandleMatch(IProcessorState processor, int bufferLength, ref int currentBufferPosition, int token, Stream target)
            {
                bool flagsOn;
                if(!processor.Config.Flags.TryGetValue("flags", out flagsOn))
                {
                    flagsOn = true;
                }

                bool emit = token < 2 || !flagsOn;
                bool turnOn = (token % 2) == 0;
                int written = 0;

                if (emit)
                {
                    byte[] tokenValue = Tokens[token];
                    target.Write(tokenValue, 0, tokenValue.Length);
                    written = tokenValue.Length;
                }

                //Only turn the flag in question back on if it's the "flags" flag.
                //  Yes, we still need to emit it as the common case is for this
                //  to be done in the template definition file
                if (flagsOn)
                {
                    processor.Config.Flags[_owner.Name] = token == 0;
                }
                else if (_owner.Name == "flags" && turnOn)
                {
                    processor.Config.Flags["flags"] = true;
                }

                return written;
            }
        }
    }
}
