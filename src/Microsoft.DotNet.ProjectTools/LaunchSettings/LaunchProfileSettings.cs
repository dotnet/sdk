// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;

namespace Microsoft.DotNet.ProjectTools;

public sealed class LaunchProfileSettings
{
    public string? FailureReason { get; }

    public LaunchSettings? Model { get; }

    private LaunchProfileSettings(string? failureReason, LaunchSettings? launchSettings)
    {
        FailureReason = failureReason;
        Model = launchSettings;
    }

    [MemberNotNullWhen(false, nameof(FailureReason))]
    public bool Successful
        => FailureReason == null;

    public static LaunchProfileSettings Failure(string reason)
        => new(reason, launchSettings: null);

    public static LaunchProfileSettings Success(LaunchSettings? model)
        => new(failureReason: null, launchSettings: model);
}
