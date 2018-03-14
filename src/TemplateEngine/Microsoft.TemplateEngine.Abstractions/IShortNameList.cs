using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IShortNameList
    {
        IReadOnlyList<string> ShortNameList { get; }
    }
}
