using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Core
{
    public class FileChange : IFileChange
    {
        public string TargetRelativePath { get; }

        public ChangeKind ChangeKind { get; }

        public FileChange(string path, ChangeKind changeKind)
        {
            TargetRelativePath = path;
            ChangeKind = changeKind;
        }
    }
}
