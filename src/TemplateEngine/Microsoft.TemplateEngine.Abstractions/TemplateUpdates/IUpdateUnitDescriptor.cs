// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.TemplateEngine.Abstractions.TemplateUpdates
{
    public interface IUpdateUnitDescriptor
    {
        // The existing install unit that this update descriptor is for.
        IInstallUnitDescriptor InstallUnitDescriptor { get; }

        // to be passed to the installer
        string InstallString { get; }

        // for user display
        string UpdateDisplayInfo { get; }
    }
}
