using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Bootstrapper
{
    internal class DotnetupProgram
    {
        public static int Main(string[] args)
        {
            // Handle --debug flag using the standard .NET SDK pattern
            // This is DEBUG-only and removes the --debug flag from args
            DotnetupDebugHelper.HandleDebugSwitch(ref args);

            return Parser.Invoke(args);
        }
    }
}
