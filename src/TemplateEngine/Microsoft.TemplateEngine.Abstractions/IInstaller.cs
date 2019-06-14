// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions
{
    public interface IInstaller : IInstallerBase
    {
        void InstallPackages(IEnumerable<string> installationRequests, IList<string> nuGetSources);

        void InstallPackages(IEnumerable<string> installationRequests, IList<string> nuGetSources, bool debugAllowDevInstall);
    }
}
