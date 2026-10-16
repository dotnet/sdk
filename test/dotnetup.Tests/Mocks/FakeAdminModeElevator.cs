// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;

internal sealed class FakeAdminModeElevator : IAdminModeElevator
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

        if (!AppliesChange)
        {
            return;
        }

        if (operation == AdminModeOperation.Enable)
        {
            DotnetupPolicy.EnableAdminMode();
        }
        else
        {
            DotnetupPolicy.DisableAdminMode();
        }
    }
}
