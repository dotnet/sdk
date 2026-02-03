// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Commands.Test.Terminal;

/// <summary>
/// An ANSI or non-ANSI terminal that is capable of rendering the messages from <see cref="TestProgressStateAwareTerminal"/>.
/// </summary>
internal interface ITerminal
{
    int Width { get; }

    int Height { get; }

    void Append(char value);

    void Append(string value);

    void AppendLine();

    void AppendLine(string value);

    void AppendLink(string path, int? lineNumber);

    void SetColor(TerminalColor color);

    void ResetColor();

    void ShowCursor();

    void HideCursor();

    void StartUpdate();

    void StopUpdate();

    void EraseProgress();

    void RenderProgress(TestProgressState?[] progress);

    void StartBusyIndicator();

    void StopBusyIndicator();
}
