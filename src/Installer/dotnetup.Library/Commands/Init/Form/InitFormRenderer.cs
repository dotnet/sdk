// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Spectre.Console;
using Spectre.Console.Rendering;

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init.Form;

/// <summary>
/// Renders the init form at each <see cref="FormCompressionLevel"/> and returns the richest result
/// that fits the terminal.
/// </summary>
internal static class InitFormRenderer
{
    // Note that for field and choice rows there's a two-character arrow prefix ("> " or "  ")
    // to indicate the current selection.  So the effective start of the label is 0 + 2 = 2 for field rows
    // and 2 + 2 = 4 for choice rows.
    private const int ChoiceRowIndent = 2;
    private const int FieldDetailIndent = 4;
    private const int ChoiceDetailIndent = 6;

    // Marker appended to the recommended default choice.
    private const string DefaultSuffix = "  (default)";

    private const string WelcomeMessage = "Welcome to dotnetup!";
    private const string ConfirmationPrompt = "Install .NET with these settings?";

    // Fallback console dimensions used when the real dimensions are unavailable.
    private const int FallbackWindowWidth = 80;
    private const int FallbackWindowHeight = 24;

    internal static Rows BuildRenderable(
        InitFormModel model, InitFormState state, bool showArrow,
        IAnsiConsole? console = null)
    {
        console ??= AnsiConsole.Console;
        ThemeColors theme = DotnetupTheme.Current;
        IReadOnlyList<FormField> fields = state.VisibleFields;
        int labelWidth = MaxLabelWidth(fields);
        int windowWidth = PositiveOrFallback(console.Profile.Width, FallbackWindowWidth);
        int windowHeight = PositiveOrFallback(console.Profile.Height, FallbackWindowHeight);
        RenderOptions renderOptions = RenderOptions.Create(console, console.Profile.Capabilities);
        Rows? fallback = null;
        foreach (FormCompressionLevel compression in Enum.GetValues<FormCompressionLevel>())
        {
            var candidate = new Rows(RenderForm(model, state, fields, labelWidth, showArrow, theme, compression));
            fallback = candidate;
            if (RenderedHeight(candidate, renderOptions, windowWidth) <= windowHeight)
            {
                return candidate;
            }
        }

        // LiveDisplay crops from the top when even the smallest layout cannot fit. Renderers place
        // essential choices and actions after optional context so cropping removes context first.
        return fallback!;
    }

    // Keeping the complete form in one iterator makes its top-to-bottom layout easier to follow.
#pragma warning disable MA0051 // Method is too long
    private static IEnumerable<IRenderable> RenderForm(
        InitFormModel model, InitFormState state, IReadOnlyList<FormField> fields,
        int labelWidth, bool showArrow, ThemeColors theme, FormCompressionLevel compression)
    {
        if (compression < FormCompressionLevel.WithoutWelcome)
        {
            yield return new Markup($"[bold {theme.Brand}]{WelcomeMessage.EscapeMarkup()}[/]");
            if (compression < FormCompressionLevel.Compact)
            {
                // Add a blank line for spacing if there's room for it
                yield return Text.Empty;
            }
        }

        if (compression < FormCompressionLevel.WithoutInstallLocation)
        {
            yield return new Markup(
                $"[{theme.Dim}]dotnetup will install .NET SDKs and runtimes in [/]" +
                $"[{theme.Accent}]{model.InstallPath.EscapeMarkup()}[/]" +
                $"[{theme.Dim}].[/]");
            yield return Text.Empty;
        }

        foreach (FormField field in fields)
        {
            foreach (IRenderable row in RenderField(model, state, field, compression, labelWidth, showArrow, theme))
            {
                yield return row;
            }
        }

        if (compression < FormCompressionLevel.WithoutConfirmationPrompt)
        {
            yield return new Markup($"[white]{ConfirmationPrompt.EscapeMarkup()}[/]");
        }

        // Show the accept row unless we're editing a field and need to save space.
        bool focusedEdit = state.Mode == FormMode.EditingField
            && compression >= FormCompressionLevel.FocusedEdit;
        if (!focusedEdit)
        {
            const string accept = "Accept and install";
            string acceptStyle = state.IsAcceptFocused ? $"{theme.Success} bold" : theme.Dim;
            string acceptArrow = state.IsAcceptFocused && showArrow ? "> " : "  ";
            yield return new Markup(
                $"[{acceptStyle}]{acceptArrow.EscapeMarkup()}{accept.EscapeMarkup()}[/]");

            if (compression < FormCompressionLevel.Compact)
            {
                yield return Text.Empty;
            }
        }

        if (compression < FormCompressionLevel.WithoutNavigationLegend)
        {
            string legend;
            if (state.Mode != FormMode.EditingField)
            {
                legend = "↑/↓ move · Enter edit/accept · Esc quit";
            }
            else if (state.IsCustomChoiceHighlighted)
            {
                legend = "type · ↑/↓ choose · Enter set · Esc back";
            }
            else
            {
                legend = "↑/↓ choose · Enter select · Esc back";
            }

            yield return new Markup($"[{theme.Dim}]{legend.EscapeMarkup()}[/]");
        }
    }
#pragma warning restore MA0051 // Method is too long

    // In the rich layout, every field shows its row, detail, and trailing spacing. The compact
    // layout removes spacing and hides detail for unfocused fields. Subsequent levels remove the
    // remaining field details, then hide every field except the one being
    // edited. RenderFieldEditor applies the later reductions to an expanded field's choices.
#pragma warning disable MA0051 // Method is too long
    private static IEnumerable<IRenderable> RenderField(
        InitFormModel model, InitFormState state, FormField field,
        FormCompressionLevel compression, int labelWidth, bool showArrow, ThemeColors theme)
    {
        bool focused = ReferenceEquals(state.FocusedField, field);
        bool editing = focused && state.Mode != FormMode.Form;

        // In "focused edit mode", only the focused field is shown when editing a field, in order to fit smaller terminals.
        // So in that case if we're editing a different field, we don't show this one at all.
        if (state.Mode == FormMode.EditingField
            && compression >= FormCompressionLevel.FocusedEdit
            && !focused)
        {
            yield break;
        }

        bool showDetail = compression < FormCompressionLevel.WithoutFieldDetails;

        //  Render the field label and possibly the value, with a selection marker in front if the current field is selected
        string arrow = focused && showArrow ? "> " : "  ";
        string label = field.Label.PadRight(labelWidth);
        string labelStyle = focused ? $"{theme.Success} bold" : "white";
        if (editing)
        {
            // Omit the value while editing because the choices below show the pending value.
            yield return new Markup(
                $"[{labelStyle}]{arrow.EscapeMarkup()}{label.EscapeMarkup()}[/]");
        }
        else
        {
            // Highlight values that differ from their defaults.
            string valueColor = field.IsChangedFromDefault ? theme.Warning : theme.Accent;
            yield return new Markup(
                $"[{labelStyle}]{arrow.EscapeMarkup()}{label.EscapeMarkup()}[/]  " +
                $"[{valueColor}]{field.DisplayValue.EscapeMarkup()}[/]");
        }

        if (editing)
        {
            //  Render the field editor (the different possible values that can be selected)
            foreach (IRenderable row in RenderFieldEditor(model, state, field,
                compression, showDetail, showArrow, theme))
            {
                yield return row;
            }
        }
        else if (showDetail && (focused || compression < FormCompressionLevel.Compact))
        {
            //  Show the field details if there's room
            string? detailText = field.BrowseDetailShowsDescription
                ? field.Description
                : field.Selected.HelperText;
            if (!string.IsNullOrEmpty(detailText))
            {
                yield return Indent(FieldDetailIndent, HelpMarkup(detailText, theme));
            }
        }

        if (compression < FormCompressionLevel.Compact)
        {
            yield return Text.Empty;
        }
    }
#pragma warning restore MA0051 // Method is too long

    private static IEnumerable<IRenderable> RenderFieldEditor(
        InitFormModel model, InitFormState state, FormField field,
        FormCompressionLevel compression, bool showDetail, bool showArrow, ThemeColors theme)
    {
        if (showDetail && field.Description is not null)
        {
            yield return Indent(FieldDetailIndent, HelpMarkup(field.Description, theme));
        }

        bool showDerived = compression < FormCompressionLevel.WithoutDerivedDetails;
        if (field.InlineHelp && compression >= FormCompressionLevel.HorizontalChoices)
        {
            //  Collapse choices into a horizontal line if we don't have enough vertical space
            //  to put each on a separate line
            foreach (IRenderable row in RenderHorizontalChoices(model, state, field,
                showArrow, theme))
            {
                yield return row;
            }
        }
        else
        {
            foreach (IRenderable row in RenderChoices(model, state, field,
                compression, showDerived, showArrow, theme))
            {
                yield return row;
            }
        }
    }

    private static IEnumerable<IRenderable> RenderHorizontalChoices(
        InitFormModel model, InitFormState state, FormField field,
        bool showArrow, ThemeColors theme)
    {
        var choices = new List<IRenderable>();
        for (int index = 0; index < field.Choices.Count; index++)
        {
            bool selected = state.EditChoiceIndex == index;
            choices.Add(BuildChoiceMarkup(field, index, selected, showArrow,
                trailing: null, choiceColumnWidth: 0, showDefaultSuffix: false, theme));
        }

        yield return Indent(ChoiceRowIndent, new Columns(choices));

        if (state.IsCustomChoiceHighlighted)
        {
            yield return Indent(
                ChoiceDetailIndent,
                new Markup(BuildCustomInputMarkup(state.CustomTextBuffer, showArrow, theme)));
        }

        yield return Indent(
            ChoiceDetailIndent,
            HelpMarkup(field.Choices[state.EditChoiceIndex].HelperText, theme));
    }

    // Renders each choice row, its help text, and the highlighted choice's derived details.
    private static IEnumerable<IRenderable> RenderChoices(
        InitFormModel model, InitFormState state, FormField field,
        FormCompressionLevel compression, bool showDerived, bool showArrow, ThemeColors theme)
    {
        bool showHelpInline = field.InlineHelp || compression >= FormCompressionLevel.InlineChoiceHelp;
        int maxChoiceWidth = field.Choices.Select((choice, index) => choice.Title.Length +
            (index == field.DefaultIndex ? DefaultSuffix.Length : 0)).Max();
        int choiceColumnWidth = showHelpInline ? maxChoiceWidth : 0;

        for (int index = 0; index < field.Choices.Count; index++)
        {
            bool selected = state.EditChoiceIndex == index;
            FieldChoice choice = field.Choices[index];

            string? trailing = null;
            if (choice.IsCustomInput && selected)
            {
                trailing = BuildCustomInputMarkup(state.CustomTextBuffer, showArrow, theme);
            }
            else if (choice.IsCustomInput && field.LastCustomText.Length > 0)
            {
                trailing = $"[{theme.Accent}]{field.LastCustomText.EscapeMarkup()}[/]";
            }
            else if (showHelpInline)
            {
                trailing = $"[{theme.Dim}]{choice.HelperText.EscapeMarkup()}[/]";
            }

            yield return Indent(
                ChoiceRowIndent,
                BuildChoiceMarkup(field, index, selected, showArrow,
                    trailing, choiceColumnWidth, showDefaultSuffix: true, theme));

            if (!showHelpInline)
            {
                yield return Indent(ChoiceDetailIndent, HelpMarkup(choice.HelperText, theme));
            }

            if (selected && showDerived)
            {
                foreach (DetailLine line in model.BuildDerivedDetailLines(field, index))
                {
                    Markup detail;
                    if (line.Value is null)
                    {
                        detail = new Markup($"[{theme.Dim}]{line.Label.EscapeMarkup()}[/]");
                    }
                    else
                    {
                        detail = new Markup(
                            $"[{theme.Dim}]{line.Label.EscapeMarkup()}[/] " +
                            $"[{theme.Accent}]{line.Value.EscapeMarkup()}[/]");
                    }

                    yield return Indent(ChoiceDetailIndent, detail);
                }
            }
        }
    }

    internal static int RenderedHeight(IRenderable renderable, RenderOptions renderOptions, int width) =>
        Segment.SplitLines(renderable.Render(renderOptions, width)).Count;

    private static int PositiveOrFallback(int value, int fallback) => value > 0 ? value : fallback;

    // Builds one choice row with its selection arrow, styled title, optional default marker,
    // and optional trailing help or custom input aligned after the choice column.
    private static Markup BuildChoiceMarkup(
        FormField field, int index, bool selected, bool showArrow,
        string? trailing, int choiceColumnWidth, bool showDefaultSuffix, ThemeColors theme)
    {
        FieldChoice choice = field.Choices[index];
        string suffix = showDefaultSuffix && index == field.DefaultIndex ? DefaultSuffix : string.Empty;
        // Green marks the highlighted choice, consistent with the focused row and the Accept action.
        string titleStyle = selected ? $"{theme.Success} bold" : "white";
        string arrow = selected && showArrow ? "> " : "  ";

        string tail = string.Empty;
        if (trailing is not null)
        {
            // Pad so the trailing slot starts at the same column for every choice (a simple table).
            int pad = Math.Max(0, choiceColumnWidth - (choice.Title.Length + suffix.Length));
            tail = new string(' ', pad) + "  " + trailing;
        }

        return new Markup(
            $"[{titleStyle}]{arrow.EscapeMarkup()}{choice.Title.EscapeMarkup()}[/]" +
            $"[{theme.Dim}]{suffix.EscapeMarkup()}[/]" +
            tail);
    }

    // The live custom editor: "> <buffer>▏" (or a placeholder).
    private static string BuildCustomInputMarkup(string buffer, bool showArrow, ThemeColors theme)
    {
        string cursor = showArrow ? "▏" : " ";
        if (buffer.Length == 0)
        {
            return
                $"[{theme.Dim}]> [/]" +
                $"[{theme.Accent}]{cursor}[/]" +
                $"[{theme.Dim} italic]  type a channel[/]";
        }

        return
            $"[{theme.Dim}]> [/]" +
            $"[{theme.Accent}]{buffer.EscapeMarkup()}{cursor}[/]";
    }

    private static Markup HelpMarkup(string text, ThemeColors theme) =>
        new($"[{theme.Dim} italic]{text.EscapeMarkup()}[/]");

    private static Padder Indent(int left, IRenderable content) =>
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
