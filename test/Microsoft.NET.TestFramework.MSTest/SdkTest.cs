// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Microsoft.NET.TestFramework;

/// <summary>
/// MSTest base class for SDK tests. This is the MSTest counterpart of the xUnit
/// <c>SdkTest</c> (which lives in the legacy Microsoft.NET.TestFramework project and takes an
/// <c>ITestOutputHelper</c> via constructor injection).
///
/// MSTest constructs test classes with a parameterless constructor and then assigns the
/// <see cref="TestContext"/> property, so test output is exposed lazily via <see cref="Log"/>
/// (backed by a <see cref="TestContextOutputHelper"/>) rather than injected.
/// </summary>
public abstract class SdkTest
{
    private ITestOutputHelper? _log;
    private TestAssetsManager? _testAssetsManager;

    /// <summary>
    /// Set by the MSTest runtime before each test runs.
    /// </summary>
    public virtual TestContext TestContext { get; set; } = null!;

    protected bool? UsingFullFrameworkMSBuild => SdkTestContext.Current.ToolsetUnderTest?.ShouldUseFullFrameworkMSBuild;

    protected ITestOutputHelper Log => _log ??= new TestContextOutputHelper(TestContext);

    protected TestAssetsManager TestAssetsManager => _testAssetsManager ??= new TestAssetsManager(Log);

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
