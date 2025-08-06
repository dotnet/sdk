// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.Build.Framework;
using Microsoft.Build.Logging;

namespace Microsoft.DotNet.Watch.UnitTests;

[Collection(nameof(InProcBuildTestCollection))]
public class BinaryLoggerTests
{
    [Theory]
    [InlineData(null, null, false)]
    [InlineData("", "msbuild.binlog")]
    [InlineData("path.binlog", "path.binlog")]
    [InlineData("LogFile=path.binlog", "path.binlog")]
    [InlineData("LogFile=path.binlog;ProjectImports=None", "path.binlog")]
    [InlineData("ProjectImports=None", "msbuild.binlog")]
    [InlineData("ProjectImports=Embed", "msbuild.binlog")]
    [InlineData("ProjectImports=ZipFile", "msbuild.binlog")]
    [InlineData("ProjectImports=ZipFile.binlog", "ProjectImports=ZipFile.binlog")]
    [InlineData("ProjectImports=ZipFilx", "ProjectImports=ZipFilx.binlog", false)] // we append .binlog to the path if it's not already there
    [InlineData("OmitInitialInfo", "msbuild.binlog")]
    [InlineData("ProjectImports=None;path1.binlog;LogFile=path2.binlog", "path2.binlog")]
    [InlineData("path", "path.binlog", false)] // we append .binlog to the path if it's not already there
    [InlineData("\"\"\"path{}\"", "path{}.binlog", false)] // wildcard {} not supported
    public void ParseBinaryLogFilePath(string? value, string? expected, bool matchesMSBuildImpl = true)
    {
        Assert.Equal(expected, CommandLineOptions.ParseBinaryLogFilePath(value));

        if (!matchesMSBuildImpl)
        {
            return;
        }

        Assert.NotNull(value);
        Assert.NotNull(expected);

        var dir = TestContext.Current.TestExecutionDirectory;
        Directory.SetCurrentDirectory(dir);

        var bl = new BinaryLogger() { Parameters = value };
        bl.Initialize(new EventSource());

        var actualPath = bl.GetType().GetProperty("FilePath", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance)?.GetValue(bl);
        if (actualPath != null)
        {
            Assert.Equal(Path.Combine(dir, expected), actualPath);
        }

        bl.Shutdown();
    }

    private class EventSource : IEventSource
    {
        public event BuildMessageEventHandler MessageRaised { add { } remove { } }
        public event BuildErrorEventHandler ErrorRaised { add { } remove { } }
        public event BuildWarningEventHandler WarningRaised { add { } remove { } }
        public event BuildStartedEventHandler BuildStarted { add { } remove { } }
        public event BuildFinishedEventHandler BuildFinished { add { } remove { } }
        public event ProjectStartedEventHandler ProjectStarted { add { } remove { } }
        public event ProjectFinishedEventHandler ProjectFinished { add { } remove { } }
        public event TargetStartedEventHandler TargetStarted { add { } remove { } }
        public event TargetFinishedEventHandler TargetFinished { add { } remove { } }
        public event TaskStartedEventHandler TaskStarted { add { } remove { } }
        public event TaskFinishedEventHandler TaskFinished { add { } remove { } }
        public event CustomBuildEventHandler CustomEventRaised { add { } remove { } }
        public event BuildStatusEventHandler StatusEventRaised { add { } remove { } }
        public event AnyEventHandler AnyEventRaised { add { } remove { } }
    }
}
