namespace Microsoft.TemplateEngine.Edge.Template
{

    public enum MatchLocation
    {
        Unspecified,
        Name,
        ShortName,
        Alias,      // never used, alias expansion occurs prior to matching
        Classification,
        Language,   // this is meant for the input language
        Context,
        OtherParameter,
        Baseline,
        DefaultLanguage,
        Author
    }    
}
