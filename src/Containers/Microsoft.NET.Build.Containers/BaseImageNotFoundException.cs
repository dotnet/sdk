// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace Microsoft.NET.Build.Containers;

public sealed class BaseImageNotFoundException : Exception
{
    internal BaseImageNotFoundException(string specifiedRuntimeIdentifier, string repositoryName, string reference, IEnumerable<string> supportedRuntimeIdentifiers)
            : base($"The RuntimeIdentifier '{specifiedRuntimeIdentifier}' is not supported by {repositoryName}:{reference}. The supported RuntimeIdentifiers are {String.Join(",", supportedRuntimeIdentifiers)}") {}
}
