using System;
using System.Runtime.InteropServices;

namespace Microsoft.TemplateEngine.Edge.Template
{
    [Obsolete("The struct is deprecated, use " + nameof(Abstractions.TemplateFiltering.MatchInfo) + " instead")]
    [StructLayout(LayoutKind.Sequential)]
    public struct MatchInfo
    {
        public MatchLocation Location;

        public MatchKind Kind;

        /// <summary>
        /// stores canonical parameter name
        /// </summary>
        public string InputParameterName;

        /// <summary>
        /// stores parameter value
        /// </summary>
        public string ParameterValue;

        /// <summary>
        /// stores the exception message if there is an args parse error.
        /// </summary>
        public string AdditionalInformation;

        /// <summary>
        /// stores the option for parameter as used in the host
        /// for example dotnet CLI offers two options for Framework parameter: -f and --framework
        /// if the user uses -f when executing command, <see cref="InputParameterFormat"/> contains -f.
        /// </summary>
        public string InputParameterFormat;
    }
}
