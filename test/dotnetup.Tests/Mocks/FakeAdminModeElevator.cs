// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;

/// <summary>
/// Stands in for the elevated <c>dotnetup elevatedsystempath</c> child process: instead of prompting
/// for UAC, it applies the operation to <paramref name="policy"/> with the same code the child runs.
/// </summary>
internal sealed class FakeAdminModeElevator(DotnetupPolicy policy) : IAdminModeElevator
{
    public List<AdminModeOperation> Operations { get; } = [];

    public Exception? Failure { get; init; }

    public bool AppliesChange { get; init; } = true;

    public void Run(AdminModeOperation operation)
    {
        Operations.Add(operation);
        if (Failure is not null)
        {
            throw Failure;
        }

        if (AppliesChange)
        {
            AdminModeSwitcher.ApplyPolicyChange(policy, operation);
        }
    }
}
