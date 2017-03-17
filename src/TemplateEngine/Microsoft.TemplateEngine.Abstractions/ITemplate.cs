using Microsoft.TemplateEngine.Abstractions.Mount;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ITemplate : ITemplateInfo
    {
        IGenerator Generator { get; }

        IFileSystemInfo Configuration { get; }

        IFileSystemInfo LocaleConfiguration { get; }

        IDirectory TemplateSourceRoot { get; }

        bool IsNameAgreementWithFolderPreferred { get; }
    }
}
