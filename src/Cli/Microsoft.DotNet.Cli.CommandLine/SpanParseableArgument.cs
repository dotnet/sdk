using System.CommandLine;

namespace Microsoft.DotNet.Cli.CommandLine;

/// <summary>
/// An argument whose generic type parameter implements <see cref="ISpanParsable{T}"/>, allowing parsing from a single token with no additional customization
/// </summary>
public class SpanParsableArgument<T> : Argument<T> where T : ISpanParsable<T>
{
    public SpanParsableArgument(string name) : base(name)
    {
        // because we know how to parse the T, we can create the custom parser easily
        CustomParser = SpanParserHelpers.StaticSingleItemParser<T>;
    }
}

/// <summary>
/// An option that contains a collection of <see cref="ISpanParsable{T}"/> items.
/// </summary>
public class SpanParsableCollectionArgument<TElem> : Argument<IReadOnlyCollection<TElem>>
    where TElem : ISpanParsable<TElem>
{
    public SpanParsableCollectionArgument(string name) : base(name)
    {
        // because we know how to parse the T, we can create the custom parser easily
        CustomParser = SpanParserHelpers.StaticMultiItemItemParser<TElem>;
    }

}
