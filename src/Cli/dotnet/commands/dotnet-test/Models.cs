// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli
{
    internal sealed record Module(string? DllOrExePath, string? ProjectPath, string? TargetFramework, string? RunSettingsFilePath);

    internal sealed record Handshake(Dictionary<byte, string>? Properties);

    internal sealed record CommandLineOption(string? Name, string? Description, bool? IsHidden, bool? IsBuiltIn);

    internal sealed record DiscoveredTest(string? Uid, string? DisplayName);

    internal sealed record SuccessfulTestResult(string? Uid, string? DisplayName, byte? State, long? Duration, string? Reason, string? StandardOutput, string? ErrorOutput, string? SessionUid);

    internal sealed record FailedTestResult(string? Uid, string? DisplayName, byte? State, long? Duration, string? Reason, FlatException[]? Exceptions, string? StandardOutput, string? ErrorOutput, string? SessionUid);

    internal sealed record FlatException(string? ErrorMessage, string? ErrorType, string? StackTrace);

    internal sealed record FileArtifact(string? FullPath, string? DisplayName, string? Description, string? TestUid, string? TestDisplayName, string? SessionUid);

    internal sealed record TestSession(byte? SessionType, string? SessionUid, string? ExecutionId);
}
