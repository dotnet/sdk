// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Telemetry;

namespace Microsoft.DotNet.Cli.MSBuild.Tests;

internal static class TestCommandServices
{
    public static CommandServices CreateNonLLM() => new(new TestLLMEnvironmentDetector(isLLMEnvironment: false));
}

internal sealed class TestLLMEnvironmentDetector : ILLMEnvironmentDetector
{
    private readonly bool _isLLMEnvironment;
    private readonly Exception? _exception;

    public TestLLMEnvironmentDetector(bool isLLMEnvironment)
    {
        _isLLMEnvironment = isLLMEnvironment;
    }

    public TestLLMEnvironmentDetector(Exception exception)
    {
        _exception = exception;
    }

    public int IsLLMEnvironmentCallCount { get; private set; }

    public string? GetLLMEnvironment() => _isLLMEnvironment ? "test" : null;

    public bool IsLLMEnvironment()
    {
        IsLLMEnvironmentCallCount++;

        if (_exception is not null)
        {
            throw _exception;
        }

        return _isLLMEnvironment;
    }
}
