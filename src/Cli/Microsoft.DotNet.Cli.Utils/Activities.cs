// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Utils;

public static class Activities
{
    public static ActivitySource Source { get; } = new("dotnet-cli", Product.Version);

    public const string DOTNET_CLI_TRACEPARENT = nameof(DOTNET_CLI_TRACEPARENT);
    public const string DOTNET_CLI_TRACESTATE = nameof(DOTNET_CLI_TRACESTATE);
}
