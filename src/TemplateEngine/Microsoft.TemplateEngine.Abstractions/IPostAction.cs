using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IPostAction
    {
        int Order { get; }

        string Name { get; }

        IReadOnlyList<IPostActionOperation> Operations { get; }

        string ManualInstructions { get; }
    }
}
