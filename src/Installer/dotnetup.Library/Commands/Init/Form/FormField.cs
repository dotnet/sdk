// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init.Form;

/// <summary>
/// A single field in the init form: a label, the values it can take, the default value, and the
/// currently selected value.
/// </summary>
internal sealed class FormField
{
    private readonly Func<bool>? _isVisible;

    public FormField(
        string label,
        IReadOnlyList<FieldChoice> choices,
        int defaultIndex,
        bool inlineHelp = false,
        Func<bool>? isVisible = null)
    {
        if (choices.Count == 0)
        {
            throw new ArgumentException("A field needs at least one choice.", nameof(choices));
        }

        if (defaultIndex < 0 || defaultIndex >= choices.Count)
        {
            throw new ArgumentOutOfRangeException(nameof(defaultIndex));
        }

        Label = label;
        Choices = choices;
        DefaultIndex = defaultIndex;
        SelectedIndex = defaultIndex;
        InlineHelp = inlineHelp;
        _isVisible = isVisible;
    }

    /// <summary>
    /// Whether this field is currently shown. A conditional field (e.g. one that only applies when
    /// another field has a particular value) supplies a predicate; fields without one are always
    /// visible.
    /// </summary>
    public bool IsVisible => _isVisible?.Invoke() ?? true;

    /// <summary>The field label shown to the left of the value (e.g. "SDK Channel").</summary>
    public string Label { get; }

    /// <summary>
    /// When true, each choice's (short) help text is rendered on the same line as the choice in the
    /// expanded picker, rather than on a separate line below it.
    /// </summary>
    public bool InlineHelp { get; }

    /// <summary>The values this field can take.</summary>
    public IReadOnlyList<FieldChoice> Choices { get; }

    /// <summary>The index of the recommended default value.</summary>
    public int DefaultIndex { get; }

    /// <summary>The index of the currently selected value. Updated when the user picks a value.</summary>
    public int SelectedIndex { get; set; }

    /// <summary>
    /// The free-text value typed for a custom-input choice, or null when a fixed value is selected.
    /// When set, it overrides the displayed value (see <see cref="DisplayValue"/>).
    /// </summary>
    public string? CustomValue { get; private set; }

    /// <summary>
    /// The most recently typed custom text, remembered even when a fixed value is currently selected
    /// so re-opening the custom input restores what the user last typed.
    /// </summary>
    public string LastCustomText { get; private set; } = string.Empty;

    /// <summary>The currently selected value's choice.</summary>
    public FieldChoice Selected => Choices[SelectedIndex];

    /// <summary>The value shown for the field: the typed custom value if any, else the selected title.</summary>
    public string DisplayValue => CustomValue ?? Selected.Title;

    /// <summary>True when the selection differs from the recommended default (drives coloring).</summary>
    public bool IsChangedFromDefault => CustomValue is not null || SelectedIndex != DefaultIndex;

    /// <summary>Selects a fixed (non-custom) value, clearing any previously typed custom value.</summary>
    public void SelectChoice(int index)
    {
        SelectedIndex = index;
        CustomValue = null;
    }

    /// <summary>Records a typed custom value and points the selection at its custom-input choice.</summary>
    public void SetCustomValue(int customChoiceIndex, string value)
    {
        SelectedIndex = customChoiceIndex;
        CustomValue = value;
        LastCustomText = value;
    }

    /// <summary>
    /// Sets the remembered custom text (the content of the custom-input choice). Setting it to an
    /// empty string clears it, returning the choice to its initial placeholder state.
    /// </summary>
    public void RememberCustomText(string value)
    {
        LastCustomText = value;
    }
}
