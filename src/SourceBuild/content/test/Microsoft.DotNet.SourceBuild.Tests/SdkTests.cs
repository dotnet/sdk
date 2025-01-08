// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.Tests;

/// <summary>
/// Shared base class for all SDK-based smoke tests.
/// </summary>
public abstract class SdkTests : TestBase
{
    internal DotNetHelper DotNetHelper { get; }

    protected SdkTests(ITestOutputHelper outputHelper) : base(outputHelper)
    {
        DotNetHelper = new DotNetHelper(outputHelper);
    }
}
