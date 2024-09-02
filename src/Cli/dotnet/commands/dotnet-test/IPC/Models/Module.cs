// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.Tools.Test;

internal sealed record Module(string? DLLPath, string? ProjectPath, string? TargetFramework) : IRequest
{
    public Module(string? DLLPath) : this(DLLPath, string.Empty, string.Empty) { }
}
