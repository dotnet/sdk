// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.ToolManifest;

internal class ToolManifestCannotBeFoundException : GracefulException
{
    public ToolManifestCannotBeFoundException(string message) : base([message], null, false)
    {
    }
}
