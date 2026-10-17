// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Dotnet.Installation.Internal;
using Microsoft.DotNet.Tools.Bootstrapper;
using Microsoft.DotNet.Tools.Dotnetup.Tests.Mocks;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests.Utilities;

internal sealed class AdminModeTestEnvironment : IDisposable
{
    private readonly TestEnvironment _files = new();
    private readonly IDotnetupPolicyStore _originalStore = DotnetupPolicy.Store;
    private readonly Func<EffectiveAccessMode>? _originalAccessModeOverride = AccessModeResolver.OverrideForTesting;
    private readonly Func<bool>? _originalUnsignedOverride = UnsignedSourcePolicy.OverrideForTesting;

    public AdminModeTestEnvironment()
    {
        DotnetupPaths.SetTestDataDirectoryOverride(TempRoot);
        DotnetupPolicy.Store = Store;
        AccessModeResolver.OverrideForTesting = null;
        UnsignedSourcePolicy.OverrideForTesting = null;
    }

    public string TempRoot => _files.TempRoot;
    public FakeDotnetupPolicyStore Store { get; } = new();

    public void Dispose()
    {
        DotnetupPolicy.Store = _originalStore;
        AccessModeResolver.OverrideForTesting = _originalAccessModeOverride;
        UnsignedSourcePolicy.OverrideForTesting = _originalUnsignedOverride;
        DotnetupPaths.ClearTestDataDirectoryOverride();
        _files.Dispose();
    }
}
