// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Build.Containers;

internal sealed class UnableToDownloadFromRepositoryException : Exception
{
    public UnableToDownloadFromRepositoryException(string repository)
        : base($"The download of the image from repository { repository } has failed.")
    {
    }
}
