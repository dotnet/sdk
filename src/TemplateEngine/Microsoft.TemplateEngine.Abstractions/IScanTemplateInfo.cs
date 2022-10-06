// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    /// <summary>
    /// The information about the template obtained as the result of scanning <see cref="IGenerator.GetTemplatesFromMountPointAsync(Mount.IMountPoint, System.Threading.CancellationToken)"/>.
    /// </summary>
    public interface IScanTemplateInfo : ITemplateMetadata, ITemplateLocator, IValidationInfo
    {
        /// <summary>
        /// Gets all localizations available for the template. The key is locale name.
        /// </summary>
        IReadOnlyDictionary<string, ILocalizationLocator> Localizations { get; }

        /// <summary>
        /// Gets all host files available for the template. The key is host identifier, the value is a relative path to the host file inside the mount point <see cref="ITemplateLocator.MountPointUri"/>.
        /// </summary>
        IReadOnlyDictionary<string, string> HostConfigFiles { get; }
    }
}
