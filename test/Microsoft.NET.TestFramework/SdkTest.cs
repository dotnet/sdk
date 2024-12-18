// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    public abstract class SdkTest
    {
        protected TestAssetsManager _testAssetsManager;

        protected bool UsingFullFrameworkMSBuild => TestContext.Current.ToolsetUnderTest.ShouldUseFullFrameworkMSBuild;

        protected ITestOutputHelper Log { get; }

        protected SdkTest(ITestOutputHelper log)
        {
            Log = log;
#pragma warning disable CS0618 // The only allowed caller of TestAssetsManager constructor.
            _testAssetsManager = new TestAssetsManager(log);
#pragma warning restore CS0618
        }

        protected static void WaitForUtcNowToAdvance()
        {
            var start = DateTime.UtcNow;

            while (DateTime.UtcNow <= start)
            {
                Thread.Sleep(millisecondsTimeout: 1);
            }
        }
    }
}
