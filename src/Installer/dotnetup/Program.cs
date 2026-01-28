using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Info;

namespace Microsoft.DotNet.Tools.Bootstrapper
{
    internal class DotnetupProgram
    {
        public static int Main(string[] args)
        {
            // Handle --debug flag using the standard .NET SDK pattern
            // This is DEBUG-only and removes the --debug flag from args
            DotnetupDebugHelper.HandleDebugSwitch(ref args);

            var parseResult = Parser.Parse(args);

            // Handle --info at the top level before other command processing.
            // This is necessary because System.CommandLine requires an action on the root command
            // for subcommand-style invocation (e.g., "dotnetup info"). By handling --info here,
            // we can support "dotnetup --info" (option style) consistent with "dotnet --info".
            if (parseResult.GetValue(Parser.InfoOption))
            {
                var jsonOutput = parseResult.GetValue(InfoCommandParser.JsonOption);
                var noList = parseResult.GetValue(InfoCommandParser.NoListOption);
                return InfoCommand.Execute(jsonOutput, noList);
            }

            return Parser.Invoke(parseResult);
        }
    }
}
