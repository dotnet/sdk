// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Spectre.Console;
using Spectre.Console.Rendering;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init.Form;

/// <summary>
/// Renders the init form and runs its input loop using Spectre.Console's <see cref="LiveDisplay"/>
/// for flicker-free, in-place (inline) updates — it does not switch to an alternate screen buffer.
/// The form stays visible while the user navigates; the focused field shows its current value's
/// help text and derived info (install location, profile file, the system installs that would
/// migrate), and expands <i>inline</i> to a choice picker when edited. Non-focused fields collapse
/// to just their label and value so the form stays compact in short terminals (e.g. an embedded
/// VS Code terminal); when a field is edited, its choices render inside a scroll window sized from
/// the console height so a long list (e.g. the channel choices) never overflows the viewport.
///
/// This class owns only presentation and input plumbing; the focus/edit transitions live in the
/// console-free <see cref="FormSelectorState"/>. It mutates the model's field selections in place
/// and returns whether the user accepted.
/// </summary>
internal static class InteractiveFormSelector
{
    // Arrow/cursor flash interval in milliseconds (matches InteractiveOptionSelector).
    private const int FlashIntervalMs = 600;

    // Left-indent (columns) for content nested under a field row and under a choice row.
    private const int FieldIndent = 4;
    private const int ChoiceIndent = 2;
    private const int ChoiceDetailIndent = 6;

    // Marker appended to the recommended default choice.
    private const string DefaultSuffix = "  (default)";

    // Fallback console height used when the real height is unavailable (redirected output).
    private const int FallbackWindowHeight = 24;

    // Approximate fixed rows consumed by the header (banner panel + subtitle + the install-location
    // line + spacing).
    private const int HeaderRows = 8;

    // Fixed rows consumed by the footer (the question line + Accept row + spacing + legend).
    private const int FooterRows = 4;

    // Rows reserved within an open field for the up/down scroll indicators plus its label row and
    // trailing blank, so those never push the highlighted choice out of view.
    private const int OpenFieldReservedRows = 4;

    // Smallest choice window we will ever show, even in a very short terminal.
    private const int MinChoiceWindow = 3;

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
        var state = new FormSelectorState(model.Fields);

        if (Console.IsInputRedirected)
        {
            // Non-interactive/redirected: render the form once and accept the defaults.
            AnsiConsole.Write(BuildRenderable(model, state, showArrow: true));
            return true;
        }

        return RunInteractive(model, state);
    }

    private static bool RunInteractive(InitFormModel model, FormSelectorState state)
    {
        bool showArrow = true;
        bool accepted = false;
        long lastToggle = Environment.TickCount64;
        bool done = false;

        AnsiConsole.Live(BuildRenderable(model, state, showArrow))
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
                            ctx.UpdateTarget(BuildRenderable(model, state, showArrow));
                        }

                        continue;
                    }

                    long now = Environment.TickCount64;
                    if (now - lastToggle >= FlashIntervalMs)
                    {
                        lastToggle = now;
                        showArrow = !showArrow;
                        ctx.UpdateTarget(BuildRenderable(model, state, showArrow));
                    }

                    Thread.Sleep(50);
                }
            });

        return accepted;
    }

    private static KeyResult ApplyKey(FormSelectorState state, ConsoleKeyInfo keyInfo)
    {
        return state.Mode == FormMode.EditingField
            ? ApplyEditKey(state, keyInfo)
            : ApplyFormKey(state, keyInfo.Key);
    }

    private static KeyResult ApplyFormKey(FormSelectorState state, ConsoleKey key)
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

    private static KeyResult ApplyEditKey(FormSelectorState state, ConsoleKeyInfo keyInfo)
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
                // Typing edits a highlighted custom-input choice in place (no-op otherwise).
                if (state.IsCustomChoiceHighlighted && !char.IsControl(keyInfo.KeyChar) && keyInfo.KeyChar != '\0')
                {
                    state.AppendChar(keyInfo.KeyChar);
                    return KeyResult.Redraw;
                }

                return KeyResult.Ignore;
        }
    }

    private static Rows BuildRenderable(InitFormModel model, FormSelectorState state, bool showArrow)
    {
        ThemeColors theme = DotnetupTheme.Current;
        IReadOnlyList<FormField> fields = state.VisibleFields;
        int labelWidth = MaxLabelWidth(fields);
        int windowHeight = SafeWindowHeight();

        // Prefer the richer view — every field showing its help and derived info — whenever the
        // console is tall enough for it. Fall back to detail for only the focused field (and scroll
        // windows while editing) when vertical space is tight (e.g. a small embedded terminal).
        bool showAllDetail = EstimateFullDetailHeight(fields) <= windowHeight;

        var rows = new List<IRenderable>
        {
            new Markup($"[bold {theme.Brand}]{model.Subtitle.EscapeMarkup()}[/]"),
            Text.Empty,
            new Markup(string.Format(
                CultureInfo.InvariantCulture,
                "[{0}]dotnetup will install .NET SDKs and runtimes in [/][{1}]{2}[/][{0}].[/]",
                theme.Dim,
                theme.Accent,
                model.InstallPath.EscapeMarkup())),
            Text.Empty,
        };

        for (int i = 0; i < fields.Count; i++)
        {
            AppendField(rows, model, state, fields[i], i, showAllDetail, windowHeight, labelWidth, showArrow, theme);
        }

        rows.Add(new Markup($"[white]{model.Question.EscapeMarkup()}[/]"));
        rows.Add(BuildAcceptRow(state.IsAcceptFocused, showArrow, theme));
        rows.Add(Text.Empty);
        rows.Add(BuildLegend(state, theme));

        return new Rows(rows);
    }

    // Estimates the row count of the browse view when every field shows its summary detail, used to
    // decide whether the console is tall enough for the rich layout.
    private static int EstimateFullDetailHeight(IReadOnlyList<FormField> fields)
    {
        int height = HeaderRows + FooterRows;
        foreach (FormField field in fields)
        {
            height += BrowseFieldRows(field);
        }

        return height;
    }

    // Rows a field occupies in the summary (browse) view: its label+value row, its one browse line
    // (the field description or the selected option's help), and a trailing blank.
    private static int BrowseFieldRows(FormField field)
    {
        string? browseLine = field.SummaryShowsDescription ? field.Description : field.Selected.HelperText;
        int rows = 1;
        if (!string.IsNullOrEmpty(browseLine))
        {
            rows += 1;
        }

        return rows + 1;
    }

    // Appends a field's row plus its detail. At the summary level a field shows its broad
    // description and a one-line selection summary; the full breakdown appears only when the field
    // is expanded. In the rich layout every field shows its summary detail; in the compact layout
    // only the focused field does, so the form fits a short terminal.
    private static void AppendField(
        List<IRenderable> rows,
        InitFormModel model,
        FormSelectorState state,
        FormField field,
        int index,
        bool showAllDetail,
        int windowHeight,
        int labelWidth,
        bool showArrow,
        ThemeColors theme)
    {
        bool focused = state.FocusedRow == index;
        bool editing = focused && state.Mode != FormMode.Form;
        bool showFieldInfo = focused || showAllDetail;

        rows.Add(BuildFieldRow(field, labelWidth, focused, editing, showArrow, theme));

        if (editing)
        {
            // Broad explanation of the field as context above its choices.
            if (field.Description is not null)
            {
                rows.Add(Indent(HelpMarkup(field.Description, theme), FieldIndent));
            }

            AppendChoiceWindow(rows, model, state, field, showAllDetail, windowHeight, showArrow, theme);
        }
        else if (showFieldInfo)
        {
            // Summary line: the field description for concept fields (e.g. SDK Channel), otherwise
            // the selected option's own help — the same text shown for that option when expanded.
            string? browseLine = field.SummaryShowsDescription ? field.Description : field.Selected.HelperText;
            if (!string.IsNullOrEmpty(browseLine))
            {
                rows.Add(Indent(HelpMarkup(browseLine, theme), FieldIndent));
            }
        }

        rows.Add(Text.Empty);
    }

    // Renders the edited field's choices inside a scroll window sized from the console height, with
    // "N more above/below" indicators, so a long choice list never overflows the viewport. The
    // window is centered on the highlighted choice.
    private static void AppendChoiceWindow(
        List<IRenderable> rows,
        InitFormModel model,
        FormSelectorState state,
        FormField field,
        bool showAllDetail,
        int windowHeight,
        bool showArrow,
        ThemeColors theme)
    {
        int count = field.Choices.Count;
        int windowSize = ComputeChoiceWindow(model, state, field, showAllDetail, windowHeight);
        int offset = ComputeWindowOffset(state.EditChoiceIndex, count, windowSize);
        int alignWidth = field.InlineHelp ? InlineHelpColumnWidth(field) : 0;

        if (offset > 0)
        {
            rows.Add(Indent(new Markup(string.Format(
                CultureInfo.InvariantCulture,
                "[{0}]{1} {2} more above[/]",
                theme.Dim,
                Constants.Symbols.UpTriangle,
                offset)), ChoiceIndent));
        }

        int end = Math.Min(count, offset + windowSize);
        for (int c = offset; c < end; c++)
        {
            AppendChoice(rows, model, field, state, c, alignWidth, showArrow, theme);
        }

        int below = count - end;
        if (below > 0)
        {
            rows.Add(Indent(new Markup(string.Format(
                CultureInfo.InvariantCulture,
                "[{0}]{1} {2} more below[/]",
                theme.Dim,
                Constants.Symbols.DownTriangle,
                below)), ChoiceIndent));
        }
    }

    // How many choices to show at once while editing, budgeted from the console height so the open
    // field (plus header, footer, the selected choice's derived info, and the other fields — which
    // may themselves be showing detail in the rich layout) fits without overflowing.
    private static int ComputeChoiceWindow(InitFormModel model, FormSelectorState state, FormField editedField, bool showAllDetail, int windowHeight)
    {
        int overhead = HeaderRows + FooterRows + OpenFieldReservedRows;

        // The edited field's description (shown while focused) also consumes a row.
        if (editedField.Description is not null)
        {
            overhead += 1;
        }

        // The selected choice's derived info renders inside the window region.
        overhead += model.BuildDetail(editedField, state.EditChoiceIndex).Lines.Count;

        // The other visible fields stay collapsed to a single line, unless the rich layout is
        // showing their summary detail too.
        foreach (FormField other in state.VisibleFields)
        {
            if (ReferenceEquals(other, editedField))
            {
                continue;
            }

            overhead += showAllDetail ? BrowseFieldRows(other) : 2;
        }

        int budget = windowHeight - overhead;

        // A non-inline choice occupies two rows (title + help); an inline-help choice occupies one.
        int rowsPerChoice = editedField.InlineHelp ? 1 : 2;
        int maxChoices = budget / rowsPerChoice;

        // Never fewer than MinChoiceWindow choices, but also never more than the field actually has
        // (which can itself be fewer than MinChoiceWindow, e.g. a two-choice yes/no field).
        int count = editedField.Choices.Count;
        int lower = Math.Min(MinChoiceWindow, count);
        return Math.Clamp(maxChoices, lower, count);
    }

    // Keeps the highlighted choice within the window, clamped to the list bounds.
    private static int ComputeWindowOffset(int highlighted, int count, int windowSize)
    {
        if (windowSize >= count)
        {
            return 0;
        }

        int offset = highlighted - (windowSize / 2);
        return Math.Clamp(offset, 0, count - windowSize);
    }

    private static int SafeWindowHeight()
    {
        try
        {
            int height = Console.WindowHeight;
            return height > 0 ? height : FallbackWindowHeight;
        }
        catch (IOException)
        {
            return FallbackWindowHeight;
        }
    }

    // While a field is being edited the value is omitted from its row, since the choice list below
    // shows (and may change) it — repeating it on the row would be redundant or conflicting.
    private static Markup BuildFieldRow(FormField field, int labelWidth, bool focused, bool editing, bool showArrow, ThemeColors theme)
    {
        string arrow = focused && showArrow ? "> " : "  ";
        string label = field.Label.PadRight(labelWidth);
        // Green marks the focused row (consistent with the Accept action and the highlighted choice).
        string labelStyle = focused ? $"{theme.Success} bold" : "white";

        if (editing)
        {
            return new Markup(string.Format(
                CultureInfo.InvariantCulture,
                "[{0}]{1}{2}[/]",
                labelStyle,
                arrow.EscapeMarkup(),
                label.EscapeMarkup()));
        }

        // The current value is shown in the accent color, or yellow when changed from the default so
        // it's easy to spot what you've adjusted.
        string valueColor = field.IsChangedFromDefault ? theme.Warning : theme.Accent;
        return new Markup(string.Format(
            CultureInfo.InvariantCulture,
            "[{0}]{1}{2}[/]  [{3}]{4}[/]",
            labelStyle,
            arrow.EscapeMarkup(),
            label.EscapeMarkup(),
            valueColor,
            field.DisplayValue.EscapeMarkup()));
    }

    // A choice row, its help text, and (when highlighted) its derived info inline beneath it.
    private static void AppendChoice(
        List<IRenderable> rows,
        InitFormModel model,
        FormField field,
        FormSelectorState state,
        int index,
        int alignWidth,
        bool showArrow,
        ThemeColors theme)
    {
        bool selected = state.EditChoiceIndex == index;
        FieldChoice choice = field.Choices[index];

        // Inline-help fields render content in the slot to the right of the title.
        string? trailing = field.InlineHelp ? BuildInlineTrailing(field, choice, selected, state, showArrow, theme) : null;

        rows.Add(Indent(BuildChoiceMarkup(field, index, selected, showArrow, trailing, alignWidth, theme), ChoiceIndent));

        if (!field.InlineHelp)
        {
            rows.Add(Indent(HelpMarkup(choice.HelperText, theme), ChoiceDetailIndent));
        }

        if (selected)
        {
            AppendDerived(rows, model.BuildDetail(field, index).Lines, ChoiceDetailIndent, theme);
        }
    }

    // The trailing slot for an inline-help choice: the live editor for a highlighted custom choice;
    // a custom choice's typed value once one exists (even when not selected); otherwise the help.
    private static string BuildInlineTrailing(FormField field, FieldChoice choice, bool selected, FormSelectorState state, bool showArrow, ThemeColors theme)
    {
        if (choice.IsCustomInput)
        {
            if (selected)
            {
                return BuildEditorTrailing(state.CustomTextBuffer, showArrow, theme);
            }

            string value = field.LastCustomText;
            if (value.Length > 0)
            {
                return string.Format(CultureInfo.InvariantCulture, "[{0}]{1}[/]", theme.Accent, value.EscapeMarkup());
            }
        }

        return string.Format(CultureInfo.InvariantCulture, "[{0}]{1}[/]", theme.Dim, choice.HelperText.EscapeMarkup());
    }

    private static Markup BuildChoiceMarkup(FormField field, int index, bool selected, bool showArrow, string? trailing, int alignWidth, ThemeColors theme)
    {
        FieldChoice choice = field.Choices[index];
        string suffix = index == field.DefaultIndex ? DefaultSuffix : string.Empty;
        // Green marks the highlighted choice, consistent with the focused row and the Accept action.
        string titleStyle = selected ? $"{theme.Success} bold" : "white";
        string arrow = selected && showArrow ? "> " : "  ";

        string tail = string.Empty;
        if (trailing is not null)
        {
            // Pad so the trailing slot starts at the same column for every choice (a simple table).
            int pad = Math.Max(0, alignWidth - (choice.Title.Length + suffix.Length));
            tail = new string(' ', pad) + "  " + trailing;
        }

        return new Markup(string.Format(
            CultureInfo.InvariantCulture,
            "[{0}]{1}{2}[/][{3}]{4}[/]{5}",
            titleStyle,
            arrow.EscapeMarkup(),
            choice.Title.EscapeMarkup(),
            theme.Dim,
            suffix.EscapeMarkup(),
            tail));
    }

    private static int InlineHelpColumnWidth(FormField field)
    {
        int width = 0;
        for (int i = 0; i < field.Choices.Count; i++)
        {
            int len = field.Choices[i].Title.Length + (i == field.DefaultIndex ? DefaultSuffix.Length : 0);
            width = Math.Max(width, len);
        }

        return width;
    }

    // The live custom editor rendered inline in the trailing slot: "> <buffer>▏" (or a placeholder).
    private static string BuildEditorTrailing(string buffer, bool showArrow, ThemeColors theme)
    {
        string cursor = showArrow ? "▏" : " ";
        if (buffer.Length == 0)
        {
            return string.Format(
                CultureInfo.InvariantCulture,
                "[{0}]> [/][{1}]{2}[/][{0} italic]  type a channel[/]",
                theme.Dim,
                theme.Accent,
                cursor);
        }

        return string.Format(
            CultureInfo.InvariantCulture,
            "[{0}]> [/][{1}]{2}{3}[/]",
            theme.Dim,
            theme.Accent,
            buffer.EscapeMarkup(),
            cursor);
    }

    private static void AppendDerived(List<IRenderable> rows, IReadOnlyList<DetailLine> lines, int indent, ThemeColors theme)
    {
        foreach (DetailLine line in lines)
        {
            rows.Add(Indent(BuildDetailLine(line, theme), indent));
        }
    }

    private static Markup HelpMarkup(string text, ThemeColors theme) =>
        new($"[{theme.Dim} italic]{text.EscapeMarkup()}[/]");

    private static Markup BuildDetailLine(DetailLine line, ThemeColors theme)
    {
        if (line.Value is null)
        {
            return new Markup($"[{theme.Dim}]{line.Label.EscapeMarkup()}[/]");
        }

        return new Markup(string.Format(
            CultureInfo.InvariantCulture,
            "[{0}]{1}[/] [{2}]{3}[/]",
            theme.Dim,
            line.Label.EscapeMarkup(),
            theme.Accent,
            line.Value.EscapeMarkup()));
    }

    private static Markup BuildAcceptRow(bool focused, bool showArrow, ThemeColors theme)
    {
        const string accept = "Accept and install";
        if (focused)
        {
            string arrow = showArrow ? "> " : "  ";
            return new Markup($"[{theme.Success} bold]{arrow.EscapeMarkup()}{accept.EscapeMarkup()}[/]");
        }

        return new Markup($"[{theme.Dim}]  {accept.EscapeMarkup()}[/]");
    }

    private static Markup BuildLegend(FormSelectorState state, ThemeColors theme)
    {
        string text;
        if (state.Mode != FormMode.EditingField)
        {
            text = "↑/↓ move · Enter edit/accept · Esc quit";
        }
        else if (state.IsCustomChoiceHighlighted)
        {
            text = "type · ↑/↓ choose · Enter set · Esc back";
        }
        else
        {
            text = "↑/↓ choose · Enter select · Esc back";
        }

        return new Markup($"[{theme.Dim}]{text.EscapeMarkup()}[/]");
    }

    private static Padder Indent(IRenderable content, int left) =>
        new(content, new Padding(left, 0, 0, 0));

    private static int MaxLabelWidth(IReadOnlyList<FormField> fields)
    {
        int width = 0;
        foreach (FormField field in fields)
        {
            width = Math.Max(width, field.Label.Length);
        }

        return width;
    }
}
