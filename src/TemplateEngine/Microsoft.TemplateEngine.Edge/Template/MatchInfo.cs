namespace Microsoft.TemplateEngine.Edge.Template
{
    public struct MatchInfo
    {
        public MatchLocation Location;

        public MatchKind Kind;

        // TODO: Rename - this always represents the input parameter name.
        // This is an outward facing assembly, so we will have to wait for a major version release to change it.
        // If we can, should be "InputParameterName"
        public string ChoiceIfLocationIsOtherChoice;

        public string ParameterValue;

        // Stores the exception message if there is an args parse error.
        public string AdditionalInformation;
    }
}
