namespace System.CommandLine.StaticCompletions;

public interface IExtendedSymbol
{
    /// <summary>
    /// Indicates whether this option requires a dynamic call into the dotnet process to compute completions.
    /// </summary>
    bool IsDynamic { get; set; }
}

public sealed class ExtendedOption<T>(string name, params string[] aliases)
    : Option<T>(name, aliases), IExtendedSymbol
{
    public bool IsDynamic { get; set; }
}

public sealed class ExtendedArgument<T>(string name)
    : Argument<T>(name), IExtendedSymbol
{
    public bool IsDynamic { get; set; }
}

public static class SymbolExtensions
{
    extension(Symbol symbol)
    {
        public bool IsDynamic => symbol is IExtendedSymbol { IsDynamic: true };
    }
}
