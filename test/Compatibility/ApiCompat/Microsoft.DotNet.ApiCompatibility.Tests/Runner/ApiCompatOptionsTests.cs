// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompatibility.Runner.Tests
{
    [TestClass]
    public class ApiCompatOptionsTests
    {
        [TestMethod]
        public void Ctor_ValidArguments_PropertiesSet()
        {
            bool enableStrictMode = true;
            bool isBaselineComparison = true;

            ApiCompatRunnerOptions options = new(enableStrictMode, isBaselineComparison);

            Assert.AreEqual(enableStrictMode, options.EnableStrictMode);
            Assert.AreEqual(isBaselineComparison, options.IsBaselineComparison);
        }
    }
}
