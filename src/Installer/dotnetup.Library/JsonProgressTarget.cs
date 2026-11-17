// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Buffers;
using System.Text.Json;
using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper;

/// <summary>
/// Writes progress as JSON Lines for programs that render their own progress UI. Each line is one event:
/// <c>{"version":1,"event":"started|progress|finished","task":1,"description":"...","percent":25}</c>,
/// where <c>percent</c> appears only on <c>progress</c> events.
/// </summary>
public sealed class JsonProgressTarget(TextWriter output, ProgressTaskIds? taskIds = null) : AppendOnlyProgressTarget(progressStepPercent: 1, taskIds)
{
    public const int EventVersion = 1;

    private readonly object _writeLock = new();

    protected override void WriteStarted(int taskId, string description) =>
        WriteEvent("started", taskId, description, percent: null);

    protected override void WriteStep(int taskId, string startDescription, string currentDescription, int percent) =>
        WriteEvent("progress", taskId, currentDescription, percent);

    protected override void WriteFinished(int taskId, string description) =>
        WriteEvent("finished", taskId, description, percent: null);

    private void WriteEvent(string eventName, int taskId, string description, int? percent)
    {
        var buffer = new ArrayBufferWriter<byte>();
        using (var json = new Utf8JsonWriter(buffer))
        {
            json.WriteStartObject();
            json.WriteNumber("version", EventVersion);
            json.WriteString("event", eventName);
            json.WriteNumber("task", taskId);
            json.WriteString("description", description);
            if (percent is int value)
            {
                json.WriteNumber("percent", value);
            }

            json.WriteEndObject();
        }

        lock (_writeLock)
        {
            output.WriteLine(Encoding.UTF8.GetString(buffer.WrittenSpan));
            output.Flush();
        }
    }
}

/// <summary>
/// Reserves stdout for <see cref="JsonProgressTarget"/> events while a command runs with
/// <c>--progress json</c>, moving all human-readable output to stderr.
/// </summary>
internal static class JsonProgressOutput
{
    private static Scope? s_scope;

    /// <summary>The writer for JSON progress events: the original stdout inside <see cref="Begin"/>, otherwise stdout.</summary>
    public static TextWriter Events => s_scope?.Events ?? Console.Out;

    /// <summary>Task IDs shared by every JSON progress target inside <see cref="Begin"/>, so IDs are unique per command.</summary>
    public static ProgressTaskIds TaskIds => s_scope?.TaskIds ?? new ProgressTaskIds();

    public static IDisposable Begin()
    {
        TextWriter stdout = Console.Out;
        IAnsiConsole previousConsole = AnsiConsole.Console;

        s_scope = new Scope(stdout, previousConsole);
        Console.SetOut(Console.Error);
        AnsiConsole.Console = AnsiConsole.Create(new AnsiConsoleSettings { Out = new AnsiConsoleOutput(Console.Error) });
        if (Console.IsErrorRedirected)
        {
            AnsiConsole.Profile.Width = int.MaxValue;
        }

        return s_scope;
    }

    private sealed class Scope(TextWriter stdout, IAnsiConsole previousConsole) : IDisposable
    {
        public TextWriter Events => stdout;

        public ProgressTaskIds TaskIds { get; } = new();

        public void Dispose()
        {
            Console.SetOut(stdout);
            AnsiConsole.Console = previousConsole;
            s_scope = null;
        }
    }
}
