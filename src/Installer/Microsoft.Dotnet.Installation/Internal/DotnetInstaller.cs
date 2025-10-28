// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Deployment.DotNet.Releases;
using Spectre.Console;

namespace Microsoft.Dotnet.Installation.Internal
{
    internal class DotnetInstaller : IDotnetInstaller
    {
        public void Install(DotnetInstallRoot dotnetRoot, InstallComponent component, ReleaseVersion version)
        {
            var installRequest = new DotnetInstallRequest(dotnetRoot, new UpdateChannel(version.ToString()), component, new InstallRequestOptions());

            using DotnetArchiveExtractor installer = new(installRequest, version, new ReleaseManifest(), noProgress: true);
            installer.Prepare();
            installer.Commit();
        }
        public void Uninstall(DotnetInstallRoot dotnetRoot, InstallComponent component, ReleaseVersion version) => throw new NotImplementedException();
    }
}
