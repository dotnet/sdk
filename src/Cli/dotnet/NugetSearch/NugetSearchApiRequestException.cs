// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.NugetSearch;

internal class NugetSearchApiRequestException : GracefulException
{
    public NugetSearchApiRequestException(string message)
        : base([message], null, false)
    {
    }
}
