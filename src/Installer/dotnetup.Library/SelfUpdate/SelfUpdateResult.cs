// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.DotNet.Tools.Bootstrapper.SelfUpdate;

/// <summary>Describes the installed and available versions considered by a self-update attempt.</summary>
internal sealed record SelfUpdateResult(
    ReleaseVersion InstalledVersion,
    ReleaseVersion AvailableVersion,
    bool WasUpdated);
