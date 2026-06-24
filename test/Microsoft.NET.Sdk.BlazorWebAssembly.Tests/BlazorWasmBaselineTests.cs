// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.NET.Sdk.StaticWebAssets.Tests;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.NET.Sdk.BlazorWebAssembly.Tests
{
#pragma warning disable MSTEST0016
    [TestClass]
    public class BlazorWasmBaselineTests : AspNetSdkBaselineTest
    {
        protected override string EmbeddedResourcePrefix => string.Join('.', "Microsoft.NET.Sdk.BlazorWebAssembly.Tests", "StaticWebAssetsBaselines");

        protected override string ComputeBaselineFolder() =>
            Path.Combine(SdkTestContext.GetRepoRoot() ?? AppContext.BaseDirectory, "test", "Microsoft.NET.Sdk.BlazorWebAssembly.Tests", "StaticWebAssetsBaselines");
    }
#pragma warning restore MSTEST0016
}