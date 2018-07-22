using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface ICreationEffects
    {
        IReadOnlyList<IFileChange> FileChanges { get; }

        ICreationResult CreationResult { get; }
    }

    public interface ICreationEffects2
    {
        IReadOnlyList<IFileChange2> FileChanges { get; }

        ICreationResult CreationResult { get; }
    }
}
