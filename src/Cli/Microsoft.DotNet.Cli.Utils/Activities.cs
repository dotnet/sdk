// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Cli.Utils;

/// <summary>
/// Contains helpers for working with <see cref="Activity">Activities</see> in the .NET CLI.
/// </summary>
public static class Activities
{
    /// <summary>
    /// The main entrypoint for creating <see cref="Activity">Activities</see> in the .NET CLI.
    /// All activities created in the CLI should use this <see cref="ActivitySource"/>, to allow
    /// consumers to easily filter and trace CLI activities.
    /// </summary>
    public static ActivitySource Source { get; } = new("dotnet-cli", Product.Version);

    /// <summary>
    /// The environment variable used to transfer the chain of parent activity IDs.
    /// This should be used when constructing new sub-processes in order to
    /// track spans across calls.
    /// </summary>
    public const string TRACEPARENT = nameof(TRACEPARENT);
    /// <summary>
    /// The environment variable used to transfer the trace state of the parent activities.
    /// This should be used when constructing new sub-processes in order to
    /// track spans across calls.
    /// </summary>
    public const string TRACESTATE = nameof(TRACESTATE);
}
