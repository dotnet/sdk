// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using Microsoft.DotNet.Cli.Telemetry;
using Microsoft.DotNet.Cli.Extensions;

namespace Microsoft.DotNet.Cli;

public abstract class CommandBase
{
    private static readonly Lazy<ILLMEnvironmentDetector> s_defaultLLMEnvironmentDetector =
        new(() => new LLMEnvironmentDetectorForTelemetry());
    // Test overrides must be scoped per execution context because command tests run in parallel.
    private static readonly AsyncLocal<ILLMEnvironmentDetector> s_llmEnvironmentDetector = new();

    protected ParseResult _parseResult;

    protected CommandBase(ParseResult parseResult)
    {
        _parseResult = parseResult;
        parseResult.ShowHelpOrErrorIfAppropriate();
    }

    protected CommandBase() { }

    private protected static ILLMEnvironmentDetector LLMEnvironmentDetector =>
        s_llmEnvironmentDetector.Value ?? s_defaultLLMEnvironmentDetector.Value;

    /// <summary>
    /// Overrides LLM environment detection for commands created within the current execution context.
    /// </summary>
    internal static IDisposable UseLLMEnvironmentDetectorForTests(ILLMEnvironmentDetector llmEnvironmentDetector)
    {
        ArgumentNullException.ThrowIfNull(llmEnvironmentDetector);

        ILLMEnvironmentDetector previousDetector = s_llmEnvironmentDetector.Value;
        s_llmEnvironmentDetector.Value = llmEnvironmentDetector;
        return new LLMEnvironmentDetectorRestorer(previousDetector);
    }

    public abstract int Execute();

    private sealed class LLMEnvironmentDetectorRestorer(ILLMEnvironmentDetector previousDetector) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (!_disposed)
            {
                s_llmEnvironmentDetector.Value = previousDetector;
                _disposed = true;
            }
        }
    }
}

public abstract class CommandBase<TDefinition>(ParseResult parseResult) : CommandBase(parseResult)
    where TDefinition : Command
{
    protected TDefinition Definition { get; } = (TDefinition)parseResult.CommandResult.Command;
}
