namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IFileChange
    {
        string TargetRelativePath { get; }

        ChangeKind ChangeKind { get; }

        byte[] Contents { get; }
    }

    public interface IFileChange2 : IFileChange
    {
        string SourceRelativePath { get; }
    }
}
