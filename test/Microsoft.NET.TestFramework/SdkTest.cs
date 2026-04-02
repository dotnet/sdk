// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

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

        /// <summary>
        /// Generates a MSBuild binlog argument with a unique name based on the caller and provided parts, and places it in a location that will be collected by Helix if running in that environment.
        /// </summary>
        protected string BinLogArgument(ReadOnlySpan<string> parts, [CallerMemberName] string callerName = "")
        {
            // combine the name and parts into a unique binlog
            var fileName = $"{callerName}{(parts.Length > 0 ? "-" + string.Join("-", parts.ToArray()) : "")}-{{}}.binlog";
            var binlogDestPath = Environment.GetEnvironmentVariable("HELIX_WORKITEM_UPLOAD_ROOT") is { } ciOutputRoot && Environment.GetEnvironmentVariable("HELIX_WORKITEM_ID") is { } helixGuid ?
                Path.Combine(ciOutputRoot, "binlog", helixGuid, fileName) :
                $"./{fileName}";
            return $"/bl:{binlogDestPath}";
        }
    }
}
