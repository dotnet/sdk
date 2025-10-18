
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Bootstrapper
{
    internal class DnupProgram
    {
        public static int Main(string[] args)
        {
            var parseResult = Parser.Parse(args);
            return Parser.Invoke(parseResult);
        }
    }
}
