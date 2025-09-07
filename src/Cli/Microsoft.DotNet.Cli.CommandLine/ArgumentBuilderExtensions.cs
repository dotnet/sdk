using System.CommandLine;
using System.CommandLine.Completions;

namespace Microsoft.DotNet.Cli.CommandLine;

/// <summary>
/// Extension methods that make it easier to chain argument configuration methods when building arguments.
/// </summary>
public static class ArgumentBuilderExtensions
{
    extension<T>(Argument<T> argument)
    {
        public Argument<T> AddCompletions(Func<CompletionContext, IEnumerable<CompletionItem>> completionSource)
        {
            argument.CompletionSources.Add(completionSource);
            return argument;
        }
    }
}
