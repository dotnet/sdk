// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.TemplateEngine.Abstractions.TemplateUpdates;

namespace Microsoft.TemplateEngine.Edge.TemplateUpdates
{
    public class UpdateUnitDescriptor : IUpdateUnitDescriptor
    {
        public UpdateUnitDescriptor(IInstallUnitDescriptor installUnitDescriptor, string installString, string displayString)
        {
            InstallUnitDescriptor = installUnitDescriptor;
            InstallString = installString;
            UpdateDisplayInfo = displayString;
        }

        public IInstallUnitDescriptor InstallUnitDescriptor { get; }

        public string InstallString { get; }

        public string UpdateDisplayInfo { get; }
    }
}
