namespace Microsoft.TemplateEngine.Edge.Template
{
    public struct MatchInfo
    {
        public MatchLocation Location;

        public MatchKind Kind;

        public string ChoiceIfLocationIsOtherChoice;
    }
}
