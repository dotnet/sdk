// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Linq;

namespace Microsoft.DotNet.Tools.Bootstrapper;

internal class ArchiveDotnetInstaller : IDotnetInstaller, IDisposable
{
    public ArchiveDotnetInstaller(DotnetInstall version)
    {

    }

    public void Prepare()
    {
        // Create a user protected (wrx) random folder in temp
        // Download the correct archive to the temp folder
        // Verify the hash and or signature of the archive
    }

    public void Commit()
    {
    }

    public void Dispose()
    {
        // Clean up the temp directory
    }
}
