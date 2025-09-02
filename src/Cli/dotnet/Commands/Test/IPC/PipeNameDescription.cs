// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Cli.Commands.Test.IPC;

internal sealed class PipeNameDescription(string name, bool isDirectory) : IDisposable
{
    private readonly bool _isDirectory = isDirectory;
    private bool _disposed;

    public string Name { get; } = name;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        if (_isDirectory)
        {
            try
            {
                Directory.Delete(Path.GetDirectoryName(Name)!, true);
            }
            catch (IOException)
            {
                // This folder is created inside the temp directory and will be cleaned up eventually by the OS
            }
        }

        _disposed = true;
    }
}
