
using System.CommandLine;
using System.CommandLine.StaticCompletions;

namespace Microsoft.DotNet.Cli.CommandLine;

public class DynamicOption<T>(string name, params string[] aliases) : Option<T>(name, aliases), IDynamicOption
{
}

