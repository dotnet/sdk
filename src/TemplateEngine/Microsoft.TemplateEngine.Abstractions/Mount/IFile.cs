using System.IO;

namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    public interface IFile : IFileSystemInfo
    {
        Stream OpenRead();
    }
}