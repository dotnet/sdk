namespace Microsoft.TemplateEngine.Edge.Template
{

    public enum MatchKind
    {
        Unspecified,
        Exact,
        Partial,
        AmbiguousParameterValue,
        InvalidParameterName,
        InvalidParameterValue,
        Mismatch
    }
}
