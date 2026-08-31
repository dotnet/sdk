// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Spectre.Console;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init.Form;

/// <summary>
/// Runs the init form's interactive input loop and delegates all presentation decisions to
/// <see cref="InitFormRenderer"/>. State transitions remain in the console-free
/// <see cref="InitFormState"/>.
/// </summary>
internal static class InteractiveFormSelector
{
    // Arrow/cursor flash interval in milliseconds.
    private const int FlashIntervalMs = 600;

    private enum KeyResult
    {
        Ignore,
        Redraw,
        Quit,
        Accept,
    }

    /// <summary>
    /// Displays the form. Returns <c>true</c> if the user accepted (the model's field selections
    /// hold the chosen values), or <c>false</c> if they quit without accepting.
    /// </summary>
    public static bool Show(InitFormModel model)
    {
        var state = new InitFormState(model.Fields);

        if (Console.IsInputRedirected)
        {
            AnsiConsole.Write(InitFormRenderer.BuildRenderable(model, state, showArrow: true));
            return true;
        }

        return RunInteractive(model, state);
    }

    private static bool RunInteractive(InitFormModel model, InitFormState state)
    {
        bool showArrow = true;
        bool accepted = false;
        long lastToggle = Environment.TickCount64;
        bool done = false;

        AnsiConsole.Live(InitFormRenderer.BuildRenderable(model, state, showArrow))
            .AutoClear(true)
            .Start(ctx =>
            {
                while (!done)
                {
                    if (Console.KeyAvailable)
                    {
                        KeyResult result = ApplyKey(state, Console.ReadKey(intercept: true));
                        if (result == KeyResult.Accept)
                        {
                            accepted = true;
                            done = true;
                        }
                        else if (result == KeyResult.Quit)
                        {
                            done = true;
                        }
                        else if (result == KeyResult.Redraw)
                        {
                            showArrow = true;
                            lastToggle = Environment.TickCount64;
                            ctx.UpdateTarget(InitFormRenderer.BuildRenderable(model, state, showArrow));
                        }

                        continue;
                    }

                    long now = Environment.TickCount64;
                    if (now - lastToggle >= FlashIntervalMs)
                    {
                        lastToggle = now;
                        showArrow = !showArrow;
                        ctx.UpdateTarget(InitFormRenderer.BuildRenderable(model, state, showArrow));
                    }

                    Thread.Sleep(50);
                }
            });

        return accepted;
    }

    private static KeyResult ApplyKey(InitFormState state, ConsoleKeyInfo keyInfo)
    {
        return state.Mode == FormMode.EditingField
            ? ApplyEditKey(state, keyInfo)
            : ApplyFormKey(state, keyInfo.Key);
    }

    private static KeyResult ApplyFormKey(InitFormState state, ConsoleKey key)
    {
        switch (key)
        {
            case ConsoleKey.UpArrow:
                state.MoveUp();
                return KeyResult.Redraw;

            case ConsoleKey.DownArrow:
                state.MoveDown();
                return KeyResult.Redraw;

            case ConsoleKey.Enter:
                state.Enter();
                return state.IsDone ? KeyResult.Accept : KeyResult.Redraw;

            case ConsoleKey.Escape:
                return KeyResult.Quit;

            default:
                return KeyResult.Ignore;
        }
    }

    private static KeyResult ApplyEditKey(InitFormState state, ConsoleKeyInfo keyInfo)
    {
        switch (keyInfo.Key)
        {
            case ConsoleKey.UpArrow:
                state.MoveUp();
                return KeyResult.Redraw;

            case ConsoleKey.DownArrow:
                state.MoveDown();
                return KeyResult.Redraw;

            case ConsoleKey.Enter:
                state.Enter();
                return KeyResult.Redraw;

            case ConsoleKey.Escape:
                state.Cancel();
                return KeyResult.Redraw;

            case ConsoleKey.Backspace:
                state.Backspace();
                return KeyResult.Redraw;

            default:
                if (state.IsCustomChoiceHighlighted && !char.IsControl(keyInfo.KeyChar) && keyInfo.KeyChar != '\0')
                {
                    state.AppendChar(keyInfo.KeyChar);
                    return KeyResult.Redraw;
                }

                return KeyResult.Ignore;
        }
    }
}
