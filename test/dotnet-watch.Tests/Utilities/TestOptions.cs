// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watcher;

internal static class TestOptions
{
    public static readonly CommandLineOptions CommandLine = new() { RemainingArguments = [] };
    public static readonly EnvironmentOptions Environmental = new(WorkingDirectory: "", MuxerPath: "");
}
