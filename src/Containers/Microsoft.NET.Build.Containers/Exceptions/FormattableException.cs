// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Build.Containers.Resources;

namespace Microsoft.NET.Build.Containers;

internal abstract class FormattableException : Exception
{
    internal FormattableException(string resourceName, params object[] formatArgs) {
        ResourceName = resourceName;
        FormatArguments = formatArgs;
    }
    
    internal object[] FormatArguments { get; init; }

    internal string ResourceName { get; init; }
}
