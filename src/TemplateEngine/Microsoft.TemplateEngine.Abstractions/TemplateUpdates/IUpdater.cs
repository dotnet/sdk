// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Microsoft.TemplateEngine.Abstractions.TemplateUpdates
{
    public interface IUpdater : IIdentifiedComponent
    {
        Guid DescriptorFactoryId { get; }

        string DisplayIdentifier { get; }

        void Configure(IEngineEnvironmentSettings environmentSettings, IReadOnlyList<IInstallUnitDescriptor> existingInstallDescriptors);

        Task<IReadOnlyList<IUpdateUnitDescriptor>> CheckForUpdatesAsync(IReadOnlyList<IInstallUnitDescriptor> installDescriptorsToCheck);

        void ApplyUpdates(IInstallerBase installer, IReadOnlyList<IUpdateUnitDescriptor> updatesToApply);
    }
}
