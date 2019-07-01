using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions.TemplateUpdates
{
    public interface IUpdateCheckResult
    {
        IReadOnlyList<IUpdateUnitDescriptor> Updates { get; }

        IReadOnlyList<string> FailedUpdateCheckers { get; }

        IReadOnlyList<IInstallUnitDescriptor> DescriptorsWithoutFactories { get; }
    }
}
