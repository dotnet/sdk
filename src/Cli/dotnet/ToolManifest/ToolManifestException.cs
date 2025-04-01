// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.ToolManifest;

internal class ToolManifestException(string message) : GracefulException([message], null, false)
{
}
