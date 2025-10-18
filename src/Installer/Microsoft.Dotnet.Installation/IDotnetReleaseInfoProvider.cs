// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation;

public interface IDotnetReleaseInfoProvider
{
    IEnumerable<string> GetAvailableChannels();

    ReleaseVersion GetLatestVersion(InstallComponent component, string channel);

    // Get all versions in a channel - do we have a scenario for this?
    //IEnumerable<ReleaseVersion> GetAllVersions(InstallComponent component, string channel);

    SupportType GetSupportType(InstallComponent component, ReleaseVersion version);
}

public enum SupportType
{
    OutOfSupport,
    LongTermSupport,
    StandardTermSupport
}
