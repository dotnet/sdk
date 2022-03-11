// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit.Abstractions;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

/// <summary>
/// Shared base class for all smoke tests.
/// </summary>
public abstract class SmokeTests
{
    internal DotNetHelper DotNetHelper { get; }
    internal ITestOutputHelper OutputHelper { get; }

    protected SmokeTests(ITestOutputHelper outputHelper)
    {
        DotNetHelper = new DotNetHelper(outputHelper);
        OutputHelper = outputHelper;
    }
}
