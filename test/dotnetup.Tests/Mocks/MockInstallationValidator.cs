// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;

internal sealed class MockInstallationValidator(bool validateResult) : IInstallationValidator
{
    public int ValidateCalls { get; private set; }
    public DotnetInstall? LastInstall { get; private set; }

    public bool Validate(DotnetInstall install)
    {
        ValidateCalls++;
        LastInstall = install;
        return validateResult;
    }
}