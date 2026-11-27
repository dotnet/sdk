// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using BenchmarkDotNet.Attributes;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.TemplateEngine.Core;
using Microsoft.TemplateEngine.Core.Contracts;
using Microsoft.TemplateEngine.Core.Expressions.Cpp;
using Microsoft.TemplateEngine.Core.Operations;
using Microsoft.TemplateEngine.Core.Util;

namespace Benchmark;

[MemoryDiagnoser]
[InProcess]
[WarmupCount(3)]
[IterationCount(5)]
public class ConditionalTests
{
    private IProcessor _processor = null!;
    private MemoryStream _input = null!;
    private MemoryStream _output = null!;

    [Params(false, true)]
    public bool Condition { get; set; }

    [Params(0, 100)]
    public int ContentLines { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        ConditionalTokens tokens = new ConditionalTokens
        {
            IfTokens = new[] { "#if" }.TokenConfigs(),
            ElseTokens = new[] { "#else" }.TokenConfigs(),
            ElseIfTokens = new[] { "#elseif", "#elif" }.TokenConfigs(),
            EndIfTokens = new[] { "#endif" }.TokenConfigs()
        };
        VariableCollection variables = new VariableCollection { ["VALUE"] = Condition };
        _processor = Processor.Create(
            new EngineConfig(NullLogger.Instance, variables),
            new Conditional(tokens, true, true, CppStyleEvaluatorDefinition.Evaluate, null, true));

        StringBuilder content = new StringBuilder("Hello\n#if (VALUE)\n");
        for (int i = 0; i < ContentLines; ++i)
        {
            content.AppendLine("Conditional content");
        }
        content.Append("#endif\n");
        byte[] bytes = Encoding.UTF8.GetBytes(content.ToString());
        _input = new MemoryStream(bytes);
        _output = new MemoryStream(bytes.Length);
    }

    [Benchmark]
    public bool ProcessConditional()
    {
        _input.Position = 0;
        _output.SetLength(0);
        return _processor.Run(_input, _output, 4096);
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _input.Dispose();
        _output.Dispose();
    }
}
