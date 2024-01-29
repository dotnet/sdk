// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.MsiInstallerTests
{
    internal class VirtualMachine : IDisposable
    {
        ITestOutputHelper Log { get; }
        VMControl VMControl { get; }
        public VirtualMachine(ITestOutputHelper log)
        {
            Log = log;
            VMControl = new VMControl(log);
        }
        public void Dispose()
        {
            VMControl.Dispose();
        }

        public CommandResult RunCommand(params string[] args)
        {
            throw new NotImplementedException();
        }

        public void CopyFile(string localSource, string vmDestination)
        {
            throw new NotImplementedException();
        }

        public void WriteFile(string vmDestination, string contents)
        {
            throw new NotImplementedException();
        }

        public RemoteDirectory GetRemoteDirectory(string path)
        {
            throw new NotImplementedException();
        }

    }
}
