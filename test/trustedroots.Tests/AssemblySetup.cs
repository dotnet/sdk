// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tests;

[TestClass]
public class AssemblySetup
{
    [AssemblyInitialize]
    public static void Initialize(TestContext _)
    {
        _ = SdkTestContext.Current;
    }
}
