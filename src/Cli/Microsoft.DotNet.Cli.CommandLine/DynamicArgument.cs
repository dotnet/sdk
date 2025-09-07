using System.CommandLine;
using System.CommandLine.StaticCompletions;

namespace Microsoft.DotNet.Cli.CommandLine;

public class DynamicArgument<T>(string name) : Argument<T>(name), IDynamicArgument
{
}
