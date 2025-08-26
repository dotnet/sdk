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

        // https://github.com/dn-vm/dnvm/blob/e656f6e0011d4d710c94cb520d00604d9058460f/src/dnvm/InstallCommand.cs#L359C47-L359C62
        // Use the MIT license version of basically this logic.
    }

    public void Commit()
    {
        // https://github.com/dn-vm/dnvm/blob/main/src/dnvm/InstallCommand.cs#L393
        // Use the MIT license version of basically this logic.
    }

    public void Dispose()
    {
        // Clean up the temp directory
    }
}
