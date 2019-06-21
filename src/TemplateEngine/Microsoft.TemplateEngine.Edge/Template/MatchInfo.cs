using System.Runtime.InteropServices;

namespace Microsoft.TemplateEngine.Edge.Template
{
    [StructLayout(LayoutKind.Sequential)]
    public struct MatchInfo
    {
        public MatchLocation Location;

        public MatchKind Kind;

        public string InputParameterName;

        public string ParameterValue;

        // Stores the exception message if there is an args parse error.
        public string AdditionalInformation;
    }
}
