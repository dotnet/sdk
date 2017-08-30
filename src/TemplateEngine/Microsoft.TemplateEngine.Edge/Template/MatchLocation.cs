namespace Microsoft.TemplateEngine.Edge.Template
{

    public enum MatchLocation
    {
        Unspecified,
        Name,
        ShortName,
        Alias,
        Classification,
        Language,   // this is meant for the input language
        Context,
        OtherParameter,
        Baseline,
        DefaultLanguage
    }    
}
