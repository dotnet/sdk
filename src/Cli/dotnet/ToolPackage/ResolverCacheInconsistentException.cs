// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.ToolPackage;

internal class ResolverCacheInconsistentException(string message) : Exception(message)
{
}
