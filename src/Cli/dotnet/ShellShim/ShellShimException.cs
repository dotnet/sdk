// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Cli.ShellShim;

internal class ShellShimException : Exception
{
    public ShellShimException()
    {
    }

    public ShellShimException(string message) : base(message)
    {
    }

    public ShellShimException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
