// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.NET.Build.Containers;

internal sealed class UnableToDownloadFromRepositoryException : Exception
{
    public UnableToDownloadFromRepositoryException(string repository, string stackTrace)
        : base($"The load of the image from registry {repository} has failed. Stack trace: {stackTrace}")
    {
    }
}
