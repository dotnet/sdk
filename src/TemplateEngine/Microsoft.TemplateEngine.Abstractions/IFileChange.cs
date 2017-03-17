namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IFileChange
    {
        string TargetRelativePath { get; }

        ChangeKind ChangeKind { get; }

        byte[] Contents { get; }
    }
}
