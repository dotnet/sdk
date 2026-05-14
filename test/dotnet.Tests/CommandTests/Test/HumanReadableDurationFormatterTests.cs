// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text;
using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Test.Terminal;

namespace dotnet.Tests.CommandTests.Test;

public class HumanReadableDurationFormatterTests
{
    [Fact]
    public void Render_FormatsDurationWithResourceBackedUnitAbbreviations()
    {
        var duration = new TimeSpan(days: 1, hours: 2, minutes: 3, seconds: 4, milliseconds: 5);

        string result = HumanReadableDurationFormatter.Render(duration, showMilliseconds: true);

        result.Should().Be(GetExpectedFormattedDuration(duration, wrapInParentheses: true));
    }

    [Fact]
    public void Append_FormatsDurationWithResourceBackedUnitAbbreviations()
    {
        var duration = new TimeSpan(days: 1, hours: 2, minutes: 3, seconds: 4, milliseconds: 5);
        var terminal = new RecordingTerminal();

        HumanReadableDurationFormatter.Append(terminal, duration);

        terminal.Content.Should().Be(GetExpectedFormattedDuration(duration, wrapInParentheses: true));
    }

    private static string GetExpectedFormattedDuration(TimeSpan duration, bool wrapInParentheses)
    {
        string expected = $"{duration.Days}{CliCommandStrings.DurationDaysAbbreviation}" +
            $" {duration.Hours:00}{CliCommandStrings.DurationHoursAbbreviation}" +
            $" {duration.Minutes:00}{CliCommandStrings.DurationMinutesAbbreviation}" +
            $" {duration.Seconds:00}{CliCommandStrings.DurationSecondsAbbreviation}" +
            $" {duration.Milliseconds:000}{CliCommandStrings.DurationMillisecondsAbbreviation}";

        return wrapInParentheses ? $"({expected})" : expected;
    }

    private sealed class RecordingTerminal : ITerminal
    {
        private readonly StringBuilder _stringBuilder = new();

        public int Width => 0;
        public int Height => 0;
        public string Content => _stringBuilder.ToString();

        public void Append(char value) => _stringBuilder.Append(value);
        public void Append(string value) => _stringBuilder.Append(value);
        public void AppendLine() => throw new NotSupportedException();
        public void AppendLine(string value) => throw new NotSupportedException();
        public void AppendLink(string path, int? lineNumber) => throw new NotSupportedException();
        public void SetColor(TerminalColor color) => throw new NotSupportedException();
        public void ResetColor() => throw new NotSupportedException();
        public void ShowCursor() => throw new NotSupportedException();
        public void HideCursor() => throw new NotSupportedException();
        public void StartUpdate() => throw new NotSupportedException();
        public void StopUpdate() => throw new NotSupportedException();
        public void EraseProgress() => throw new NotSupportedException();
        public void RenderProgress(TestProgressState?[] progress) => throw new NotSupportedException();
        public void StartBusyIndicator() => throw new NotSupportedException();
        public void StopBusyIndicator() => throw new NotSupportedException();
    }
}
