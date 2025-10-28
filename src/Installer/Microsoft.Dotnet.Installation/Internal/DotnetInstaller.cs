﻿// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Text;
using Microsoft.Deployment.DotNet.Releases;

namespace Microsoft.Dotnet.Installation.Internal
{
    internal class DotnetInstaller : IDotnetInstaller
    {
        IProgressTarget _progressTarget;

        public DotnetInstaller(IProgressTarget progressTarget)
        {
            _progressTarget = progressTarget;
        }

        public void Install(DotnetInstallRoot dotnetRoot, InstallComponent component, ReleaseVersion version)
        {
            var installRequest = new DotnetInstallRequest(dotnetRoot, new UpdateChannel(version.ToString()), component, new InstallRequestOptions());

            using ArchiveDotnetExtractor installer = new(installRequest, version, _progressTarget);
            installer.Prepare();
            installer.Commit();
        }
        public void Uninstall(DotnetInstallRoot dotnetRoot, InstallComponent component, ReleaseVersion version) => throw new NotImplementedException();
    }
}
