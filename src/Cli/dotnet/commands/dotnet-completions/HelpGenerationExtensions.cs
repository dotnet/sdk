namespace Microsoft.DotNet.Cli.Completions;

using System.CommandLine;

public static class HelpExtensions
{
    /// <summary>
    /// Create a unique shell function name for a command - these names should be
    /// * distinct from the 'root' command's name (i.e. we should not generate the function name 'dotnet' for the binary 'dotnet')
    /// * distinct based on 'path' to get to this function (hence the parentCommandNames)
    /// </summary>
    /// <param name="command"></param>
    /// <param name="prefix"></param>
    /// <returns></returns>
    public static string FunctionName(this CliCommand command, string[]? parentCommandNames = null) => parentCommandNames switch
    {
        null => "_" + command.Name,
        [] => "_" + command.Name,
        var names => "_" + string.Join('_', names) + "_" + command.Name
    };

    /// <summary>
    /// sanitizes a function name to be safe for bash
    /// </summary>
    /// <param name="functionName"></param>
    /// <returns></returns>
    public static string MakeSafeFunctionName(this string functionName) => functionName.Replace('-', '_');

    /// <summary>
    /// Get all names for an option, including the primary name and all aliases
    /// </summary>
    /// <param name="option"></param>
    /// <returns></returns>
    public static string[] Names(this CliOption option)
    {
        if (option.Aliases.Count == 0)
        {
            return [option.Name];
        }
        else if (option is System.CommandLine.Help.HelpOption) // some of the help aliases are truly horrible
        {
            return ["--help", "-h"];
        }
        else
        {
            return [option.Name, .. option.Aliases];
        }
    }

    /// <summary>
    /// Get all names for a command, including the primary name and all aliases
    /// </summary>
    /// <param name="command"></param>
    /// <returns></returns>
    public static string[] Names(this CliCommand command)
    {
        if (command.Aliases.Count == 0)
        {
            return [command.Name];
        }
        else
        {
            return [command.Name, .. command.Aliases];
        }
    }

    public static IEnumerable<CliOption> HeirarchicalOptions(this CliCommand c)
    {
        var myOptions = c.Options.Where(o => !o.Hidden);
        if (c.Parents.Count() == 0)
        {
            return myOptions;
        }
        else
        {
            return c.Parents.OfType<CliCommand>().SelectMany(OptionsForParent).Concat(myOptions);
        }

    }

    private static IEnumerable<CliOption> OptionsForParent(CliCommand c)
    {
        foreach (var o in c.Options)
        {
            if (o.Recursive)
            {
                if (!o.Hidden)
                {
                    yield return o;
                }
            }
        }
        foreach (var p in c.Parents.OfType<CliCommand>())
        {
            foreach (var o in OptionsForParent(p))
            {
                yield return o;
            }
        }
    }

    public static bool IsUpperCaseSingleCharacterFlag(this string name) => name.Length == 2 && char.IsUpper(name[1]);
}
