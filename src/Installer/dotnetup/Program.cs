
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Bootstrapper
{
    internal class DnupProgram
    {
        public static int Main(string[] args)
        {
            // Handle --debug flag using the standard .NET SDK pattern
            // This is DEBUG-only and removes the --debug flag from args
            DnupDebugHelper.HandleDebugSwitch(ref args);

            var parseResult = Parser.Parse(args);
            return Parser.Invoke(parseResult);
        }
    }
}
