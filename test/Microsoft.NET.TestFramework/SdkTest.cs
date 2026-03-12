// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    public abstract class SdkTest
    {
        protected bool? UsingFullFrameworkMSBuild => SdkTestContext.Current.ToolsetUnderTest?.ShouldUseFullFrameworkMSBuild;

        protected ITestOutputHelper Log { get; }

        protected TestAssetsManager TestAssetsManager { get; }

        protected SdkTest(ITestOutputHelper log)
        {
            Log = log;
            TestAssetsManager = new TestAssetsManager(log);
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
