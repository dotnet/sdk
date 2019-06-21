namespace Microsoft.TemplateEngine.Edge.Template
{

    public enum MatchKind
    {
        Unspecified,    // TODO: rename to "ParseError". Will have to be done for a major version release.
        Exact,
        Partial,        // only used for name & name-type field matches
        AmbiguousParameterValue,    // when the value is a starts with match on more than one choice value.
        InvalidParameterName,
        InvalidParameterValue,
        Mismatch,       // only used for template language
        SingleStartsWith
    }
}
