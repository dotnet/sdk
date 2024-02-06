// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.DotNet.MsiInstallerTests
{
    //  TODO: Add VMAction for reading remote file, to allow read of file to be cached, avoiding the need to apply a snapshot to read the file on subsequent runs

    abstract class RemoteFile
    {
        public RemoteFile(string path)
        {
            Path = path;
        }

        public string Path { get; }

        public abstract string ReadAllText();
    }
}
