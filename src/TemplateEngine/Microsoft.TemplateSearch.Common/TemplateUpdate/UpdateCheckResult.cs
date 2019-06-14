using System.Collections.Generic;
using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;

namespace Microsoft.TemplateSearch.Common.TemplateUpdate
{
    public class UpdateCheckResult : IUpdateCheckResult
    {
        public UpdateCheckResult(IReadOnlyList<IUpdateUnitDescriptor> updates, IReadOnlyList<string> failedUpdateCheckers, IReadOnlyList<IInstallUnitDescriptor> descriptorsWithoutFactories)
        {
            Updates = updates;
            FailedUpdateCheckers = failedUpdateCheckers;
            DescriptorsWithoutFactories = descriptorsWithoutFactories;
        }

        public IReadOnlyList<IUpdateUnitDescriptor> Updates { get; }

        public IReadOnlyList<string> FailedUpdateCheckers { get; }

        public IReadOnlyList<IInstallUnitDescriptor> DescriptorsWithoutFactories { get; }
    }
}
