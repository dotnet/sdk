// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init.Form;

/// <summary>
/// The display/interaction mode of the form selector.
/// </summary>
internal enum FormMode
{
    /// <summary>Browsing the collapsed form: navigation moves between fields and the Accept row.</summary>
    Form,

    /// <summary>A field is expanded: navigation moves between its choices, Enter commits one.</summary>
    EditingField,
}

/// <summary>
/// Pure, console-free state machine for the init form selector. Owns the focus/edit state and the
/// transitions for arrow navigation, committing a choice, typing into a custom-input choice, and
/// accepting the form. Kept separate from the Spectre rendering so the behavior is unit-testable.
///
/// A custom-input choice is "live" whenever it is highlighted: typing edits its text buffer in
/// place and Enter commits the field to that text (just like committing any fixed choice). Moving
/// off it remembers the typed text so returning restores it.
///
/// Row layout (Form mode): rows <c>0..Fields.Count-1</c> are the fields; the final row
/// (<c>Fields.Count</c>) is the Accept action, which is the initial focus so a single Enter accepts.
/// </summary>
internal sealed class FormSelectorState
{
    private IReadOnlyList<FormField> Fields { get; }

    public FormSelectorState(IReadOnlyList<FormField> fields)
    {
        if (fields.Count == 0)
        {
            throw new ArgumentException("The form needs at least one field.", nameof(fields));
        }

        Fields = fields;

        // Focus starts on the Accept row so the recommended path is a single Enter.
        FocusedRow = AcceptRow;
    }

    /// <summary>
    /// The fields currently shown, in display order. Conditional fields may drop in or out as other
    /// fields change, so navigation, the Accept row position, and rendering all use this view rather
    /// than the full field list.
    /// </summary>
    public IReadOnlyList<FormField> VisibleFields => Fields.Where(static f => f.IsVisible).ToList();

    /// <summary>The row index representing the Accept action (immediately after the last field).</summary>
    public int AcceptRow => VisibleFields.Count;

    /// <summary>Current interaction mode.</summary>
    public FormMode Mode { get; private set; } = FormMode.Form;

    /// <summary>
    /// The focused row in Form mode: <c>0..Fields.Count-1</c> for fields, <see cref="AcceptRow"/>
    /// for Accept.
    /// </summary>
    public int FocusedRow { get; private set; }

    /// <summary>The choice index highlighted while editing a field; otherwise -1.</summary>
    public int EditChoiceIndex { get; private set; } = -1;

    /// <summary>The live text for a highlighted custom-input choice; empty otherwise.</summary>
    public string CustomTextBuffer { get; private set; } = string.Empty;

    // The edited field's remembered custom text captured when the field was opened, so Esc can
    // revert to it (arrowing between choices saves; only Esc cancels).
    private string _customTextAtEditStart = string.Empty;

    /// <summary>True once the user accepted the form; the input loop should stop.</summary>
    public bool IsDone { get; private set; }

    /// <summary>True when <see cref="FocusedRow"/> is the Accept row (Form mode only).</summary>
    public bool IsAcceptFocused => Mode == FormMode.Form && FocusedRow == AcceptRow;

    /// <summary>The field currently focused (Form mode) or being edited; null when Accept is focused.</summary>
    public FormField? FocusedField
    {
        get
        {
            IReadOnlyList<FormField> visible = VisibleFields;
            return FocusedRow >= 0 && FocusedRow < visible.Count ? visible[FocusedRow] : null;
        }
    }

    /// <summary>True when editing a field and the highlighted choice accepts free-text input.</summary>
    public bool IsCustomChoiceHighlighted =>
        Mode == FormMode.EditingField
        && FocusedField is { } focused
        && EditChoiceIndex >= 0
        && EditChoiceIndex < focused.Choices.Count
        && focused.Choices[EditChoiceIndex].IsCustomInput;

    /// <summary>Moves focus to the previous row (Form) or previous choice (Editing). Clamps at the top.</summary>
    public void MoveUp()
    {
        if (Mode == FormMode.Form)
        {
            if (FocusedRow > 0)
            {
                FocusedRow--;
            }
        }
        else if (EditChoiceIndex > 0)
        {
            RememberCurrentCustomText();
            EditChoiceIndex--;
            SeedCurrentCustomText();
        }
    }

    /// <summary>Moves focus to the next row (Form) or next choice (Editing). Clamps at the bottom.</summary>
    public void MoveDown()
    {
        if (Mode == FormMode.Form)
        {
            if (FocusedRow < AcceptRow)
            {
                FocusedRow++;
            }
        }
        else if (EditChoiceIndex < VisibleFields[FocusedRow].Choices.Count - 1)
        {
            RememberCurrentCustomText();
            EditChoiceIndex++;
            SeedCurrentCustomText();
        }
    }

    /// <summary>
    /// Enter: Form mode opens the focused field or accepts when Accept is focused; EditingField
    /// commits the highlighted choice — for a custom-input choice that means committing the typed
    /// text (ignored when empty).
    /// </summary>
    public void Enter()
    {
        if (Mode == FormMode.Form)
        {
            EnterFromForm();
        }
        else
        {
            EnterFromEditingField();
        }
    }

    /// <summary>
    /// Escape/cancel: EditingField reverts the edited field's custom text to what it was when the
    /// field was opened (only Esc cancels — arrowing between choices saves), then returns to the
    /// form. Form mode is a no-op (the caller decides whether to treat it as quit).
    /// </summary>
    public void Cancel()
    {
        if (Mode == FormMode.EditingField)
        {
            VisibleFields[FocusedRow].RememberCustomText(_customTextAtEditStart);
            CollapseToForm();
        }
    }

    /// <summary>Appends a typed character to the highlighted custom-input choice's buffer.</summary>
    public void AppendChar(char c)
    {
        if (IsCustomChoiceHighlighted)
        {
            CustomTextBuffer += c;
        }
    }

    /// <summary>Removes the last character from the highlighted custom-input choice's buffer.</summary>
    public void Backspace()
    {
        if (IsCustomChoiceHighlighted && CustomTextBuffer.Length > 0)
        {
            CustomTextBuffer = CustomTextBuffer[..^1];
        }
    }

    private void EnterFromForm()
    {
        if (FocusedRow == AcceptRow)
        {
            IsDone = true;
            return;
        }

        FormField field = VisibleFields[FocusedRow];
        EditChoiceIndex = field.SelectedIndex;
        _customTextAtEditStart = field.LastCustomText;
        Mode = FormMode.EditingField;
        SeedCurrentCustomText();
    }

    private void EnterFromEditingField()
    {
        FormField field = VisibleFields[FocusedRow];
        if (field.Choices[EditChoiceIndex].IsCustomInput)
        {
            string trimmed = CustomTextBuffer.Trim();
            if (trimmed.Length == 0)
            {
                // Nothing typed yet: keep the field open so the user can type a value.
                return;
            }

            field.SetCustomValue(EditChoiceIndex, trimmed);
        }
        else
        {
            field.SelectChoice(EditChoiceIndex);
        }

        CollapseToForm();
    }

    // Remembers the in-progress text of a highlighted custom choice so returning to it restores it.
    private void RememberCurrentCustomText()
    {
        if (IsCustomChoiceHighlighted)
        {
            VisibleFields[FocusedRow].RememberCustomText(CustomTextBuffer);
        }
    }

    // Loads the buffer for the now-highlighted choice: its remembered text if custom, else empty.
    private void SeedCurrentCustomText()
    {
        CustomTextBuffer = IsCustomChoiceHighlighted ? VisibleFields[FocusedRow].LastCustomText : string.Empty;
    }

    private void CollapseToForm()
    {
        Mode = FormMode.Form;
        EditChoiceIndex = -1;
        CustomTextBuffer = string.Empty;

        // Committing a choice can change which conditional fields are visible; keep focus in range.
        if (FocusedRow > AcceptRow)
        {
            FocusedRow = AcceptRow;
        }
    }
}
