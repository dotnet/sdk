namespace Microsoft.TemplateEngine.Edge.Template
{

    public enum MatchKind
    {
        Unspecified,    // TODO: rename to "ParseError". Will have to be done for a major version release.
        Exact,
        Partial,
        AmbiguousParameterValue,
        InvalidParameterName,
        InvalidParameterValue,
        Mismatch
    }
}
