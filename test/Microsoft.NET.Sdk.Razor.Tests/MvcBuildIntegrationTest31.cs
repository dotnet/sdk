// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.Sdk.Razor.Tests
{
    [TestClass]
    public class MvcBuildIntegrationTest31 : MvcBuildIntegrationTestLegacy
    {
        public override string TestProjectName => "SimpleMvc31";
        public override string TargetFramework => "netcoreapp3.1";
    }
}
