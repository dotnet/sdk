using System.CommandLine;
using System.CommandLine.Parsing;
using System.CommandLine.StaticCompletions;

namespace Microsoft.DotNet.Cli.CommandLine;

public interface IForwardedOption
{
    Func<ParseResult, IEnumerable<string>> GetForwardingFunction();
}

public class ForwardedOption<T> : Option<T>, IForwardedOption
{
    internal Func<ParseResult, IEnumerable<string>> ForwardingFunction;

    public ForwardedOption(string name, params string[] aliases) : base(name, aliases)
    {
        ForwardingFunction = _ => [];
    }

    public ForwardedOption(string name, Func<ArgumentResult, T> parseArgument, string? description = null)
        : base(name)
    {
        CustomParser = parseArgument;
        Description = description;
        ForwardingFunction = _ => [];
    }

    public Func<ParseResult, IEnumerable<string>> GetForwardingFunction(Func<T, IEnumerable<string>> func)
    {
        return (ParseResult parseResult) =>
        {
            if (parseResult.GetResult(this) is OptionResult r)
            {
                if (r.GetValueOrDefault<T>() is T value)
                {
                    return func(value);
                }
                else
                {
                    return [];
                }
            }
            return [];
        };
    }

    public Func<ParseResult, IEnumerable<string>> GetForwardingFunction()
    {
        return ForwardingFunction;
    }
}
