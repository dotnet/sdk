// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

namespace Microsoft.DotNet.Cli
{
    internal sealed record Module(string? DLLPath, string? ProjectPath, string? TargetFramework);

    internal sealed record Handshake(Dictionary<byte, string>? Properties);

    internal sealed record CommandLineOption(string? Name, string? Description, bool? IsHidden, bool? IsBuiltIn);

    internal sealed record DiscoveredTest(string? Uid, string? DisplayName);

    internal sealed record SuccessfulTestResult(string? Uid, string? DisplayName, byte? State, string? Reason, string? SessionUid);

    internal sealed record FailedTestResult(string? Uid, string? DisplayName, byte? State, string? Reason, string? ErrorMessage, string? ErrorStackTrace, string? SessionUid);

    internal sealed record FileArtifact(string? FullPath, string? DisplayName, string? Description, string? TestUid, string? TestDisplayName, string? SessionUid);

    internal sealed record TestSession(byte? SessionType, string? SessionUid, string? ExecutionId);
}
