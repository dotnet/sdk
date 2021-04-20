// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;

namespace Microsoft.TemplateEngine.Abstractions.Mount
{
    public interface IMountPointManager
    {
        IEngineEnvironmentSettings EnvironmentSettings { get; }

        bool TryDemandMountPoint(string mountPointUri, out IMountPoint mountPoint);
    }
}
