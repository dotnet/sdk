// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.Build.Framework;

namespace Microsoft.NET.Build.Containers.IntegrationTests;

public class LoggingBuildEngineFixture
{
    public List<BuildMessageEventArgs> Messages { get; } = new();
    public List<BuildWarningEventArgs> Warnings { get; } = new();
    public List<BuildErrorEventArgs> Errors { get; } = new();
    public IBuildEngine BuildEngine { get; private set; } = null!;

    public LoggingBuildEngineFixture()
    {
    }

    public void SetupBuildEngine(ITestOutputHelper testOutput)
    {
        if (BuildEngine is null)
        {
            IBuildEngine buildEngine = A.Fake<IBuildEngine>();
            A.CallTo(() => buildEngine.LogWarningEvent(A<BuildWarningEventArgs>.Ignored)).Invokes((BuildWarningEventArgs e) =>
            {
                Warnings.Add(e);
                testOutput.WriteLine($"Warning: {e.Message}");
            });
            A.CallTo(() => buildEngine.LogErrorEvent(A<BuildErrorEventArgs>.Ignored)).Invokes((BuildErrorEventArgs e) =>
            {
                Errors.Add(e);
                testOutput.WriteLine($"Error: {e.Message}");
            });
            A.CallTo(() => buildEngine.LogMessageEvent(A<BuildMessageEventArgs>.Ignored)).Invokes((BuildMessageEventArgs e) =>
            {
                Messages.Add(e);
                testOutput.WriteLine($"Message: {e.Message}");
            });
            BuildEngine = buildEngine;
        }
    }
}
