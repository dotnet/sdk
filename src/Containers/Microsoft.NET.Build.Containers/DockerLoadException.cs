// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.


namespace Microsoft.NET.Build.Containers;

internal sealed class DockerLoadException : Exception
{
    public DockerLoadException()
    {
    }

    public DockerLoadException(string? message) : base(message)
    {
    }

    public DockerLoadException(string? message, Exception? innerException) : base(message, innerException)
    {
    }
}
