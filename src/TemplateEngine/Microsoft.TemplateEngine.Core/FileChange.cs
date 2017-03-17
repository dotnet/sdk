using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Core
{
    public class FileChange : IFileChange
    {
        public string TargetRelativePath { get; }

        public ChangeKind ChangeKind { get; }

        public byte[] Contents { get; }

        public FileChange(string path, ChangeKind changeKind, byte[] contents = null)
        {
            TargetRelativePath = path;
            ChangeKind = changeKind;
            Contents = contents;
        }
    }
}
