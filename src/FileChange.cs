using Microsoft.CodeAnalysis.Text;

namespace Microsoft.CodeAnalysis.Tools
{
    public class FileChange
    {
        public int LineNumber { get; }

        public int CharNumber { get; }

        public string FormatDescription { get; }

        public FileChange(LinePosition changePosition, string formatDescription)
        {
            // LinePosition is zero based so we need to increment to report numbers people expect.
            LineNumber = changePosition.Line + 1;
            CharNumber = changePosition.Character + 1;
            FormatDescription = formatDescription;
        }
    }
}
