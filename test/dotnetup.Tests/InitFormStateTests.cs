// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Microsoft.DotNet.Tools.Bootstrapper.Commands.Init.Form;

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

[TestClass]
public class InitFormStateTests
{
    [TestMethod]
    public void InitialFocus_IsAcceptRow()
    {
        var state = new InitFormState(SampleFields());

        state.Mode.Should().Be(FormMode.Form);
        state.FocusedRow.Should().Be(state.AcceptRow);
        state.IsAcceptFocused.Should().BeTrue();
    }

    [TestMethod]
    public void Enter_OnAccept_CompletesForm()
    {
        var state = new InitFormState(SampleFields());

        state.Enter();

        state.IsDone.Should().BeTrue();
    }

    [TestMethod]
    public void FormNavigation_ClampsBetweenFirstFieldAndAccept()
    {
        var fields = SampleFields();
        var state = new InitFormState(fields);

        state.MoveUp();
        state.FocusedField.Should().BeSameAs(fields[1]);
        state.MoveUp();
        state.MoveUp();
        state.FocusedField.Should().BeSameAs(fields[0]);

        state.MoveDown();
        state.MoveDown();
        state.MoveDown();
        state.IsAcceptFocused.Should().BeTrue();
    }

    [TestMethod]
    public void Enter_OnField_OpensEditorAtCurrentSelection()
    {
        var fields = SampleFields();
        fields[0].SelectChoice(1);
        var state = FocusFirstField(fields);

        state.Enter();

        state.Mode.Should().Be(FormMode.EditingField);
        state.EditChoiceIndex.Should().Be(1);
    }

    [TestMethod]
    public void EditThenEnter_CommitsSelectionAndReturnsToForm()
    {
        var fields = SampleFields();
        var state = FocusFirstField(fields);
        state.Enter();

        state.MoveDown();
        state.MoveDown();
        state.Enter();

        state.Mode.Should().Be(FormMode.Form);
        state.EditChoiceIndex.Should().Be(-1);
        fields[0].SelectedIndex.Should().Be(2);
        fields[0].IsChangedFromDefault.Should().BeTrue();
    }

    [TestMethod]
    public void EditChoiceNavigation_ClampsWithinChoices()
    {
        var state = FocusFirstField(SampleFields());
        state.Enter();

        state.MoveUp();
        state.EditChoiceIndex.Should().Be(0);

        state.MoveDown();
        state.MoveDown();
        state.MoveDown();
        state.EditChoiceIndex.Should().Be(2);
    }

    [TestMethod]
    public void Cancel_WhileEditing_DiscardsSelection()
    {
        var fields = SampleFields();
        var state = FocusFirstField(fields);
        state.Enter();
        state.MoveDown();

        state.Cancel();

        state.Mode.Should().Be(FormMode.Form);
        state.FocusedField.Should().BeSameAs(fields[0]);
        fields[0].SelectedIndex.Should().Be(0);
    }

    [TestMethod]
    public void Cancel_InFormMode_IsNoOp()
    {
        var state = new InitFormState(SampleFields());

        state.Cancel();

        state.Mode.Should().Be(FormMode.Form);
        state.IsDone.Should().BeFalse();
    }

    [TestMethod]
    public void TypingThenEnter_CommitsTrimmedCustomValue()
    {
        var fields = FieldsWithCustom();
        var state = FocusFirstField(fields);
        state.Enter();
        state.MoveDown();

        foreach (char character in " 8.0x")
        {
            state.AppendChar(character);
        }
        state.Backspace();
        state.Enter();

        state.Mode.Should().Be(FormMode.Form);
        fields[0].CustomValue.Should().Be("8.0");
        fields[0].DisplayValue.Should().Be("8.0");
    }

    [TestMethod]
    public void EnterWithEmptyCustomText_KeepsFieldOpen()
    {
        var fields = FieldsWithCustom();
        var state = FocusFirstField(fields);
        state.Enter();
        state.MoveDown();

        state.Enter();

        state.Mode.Should().Be(FormMode.EditingField);
        fields[0].CustomValue.Should().BeNull();
    }

    [TestMethod]
    public void Cancel_RevertsCustomTextToValueAtEditStart()
    {
        var fields = FieldsWithCustom();
        fields[0].SetCustomValue(1, "8.0");
        var state = FocusFirstField(fields);
        state.Enter();
        state.AppendChar('x');

        state.Cancel();

        state.Mode.Should().Be(FormMode.Form);
        fields[0].LastCustomText.Should().Be("8.0");
    }

    [TestMethod]
    public void MovingOffCustom_RemembersTypedText()
    {
        var fields = FieldsWithCustom();
        var state = FocusFirstField(fields);
        state.Enter();
        state.MoveDown();
        state.AppendChar('9');
        state.AppendChar('.');
        state.AppendChar('0');

        state.MoveUp();
        fields[0].LastCustomText.Should().Be("9.0");
        state.CustomTextBuffer.Should().BeEmpty();

        state.MoveDown();
        state.CustomTextBuffer.Should().Be("9.0");
    }

    [TestMethod]
    public void SelectingFixedChoice_ClearsCommittedCustomValue()
    {
        var fields = FieldsWithCustom();
        fields[0].SetCustomValue(1, "9.9");
        var state = FocusFirstField(fields);
        state.Enter();
        state.MoveUp();

        state.Enter();

        fields[0].SelectedIndex.Should().Be(0);
        fields[0].CustomValue.Should().BeNull();
        fields[0].DisplayValue.Should().Be("a");
    }

    [TestMethod]
    public void ReeditingCommittedCustomValue_SeedsTextBuffer()
    {
        var fields = FieldsWithCustom();
        fields[0].SetCustomValue(1, "8.1");
        var state = FocusFirstField(fields);

        state.Enter();

        state.IsCustomChoiceHighlighted.Should().BeTrue();
        state.CustomTextBuffer.Should().Be("8.1");
    }

    [TestMethod]
    public void ConditionalField_DropsOutOfNavigationWhenHidden()
    {
        var gate = new FormField(
            "Gate",
            [new FieldChoice("Yes", ""), new FieldChoice("No", "")],
            defaultIndex: 0);
        var dependent = new FormField(
            "Dependent",
            [new FieldChoice("a", "")],
            defaultIndex: 0,
            isVisible: () => gate.SelectedIndex == 0);
        var trailing = new FormField("Trailing", [new FieldChoice("x", "")], defaultIndex: 0);
        var state = new InitFormState([gate, dependent, trailing]);

        state.AcceptRow.Should().Be(3);
        state.MoveUp();
        state.MoveUp();
        state.MoveUp();
        state.Enter();
        state.MoveDown();
        state.Enter();

        state.VisibleFields.Should().Equal(gate, trailing);
        state.AcceptRow.Should().Be(2);
        state.FocusedField.Should().BeSameAs(gate);

        state.MoveDown();
        state.FocusedField.Should().BeSameAs(trailing);
    }

    [TestMethod]
    public void ConditionalField_ReturnsToNavigationWhenShown()
    {
        var gate = new FormField(
            "Gate",
            [new FieldChoice("Yes", ""), new FieldChoice("No", "")],
            defaultIndex: 1);
        var dependent = new FormField(
            "Dependent",
            [new FieldChoice("a", "")],
            defaultIndex: 0,
            isVisible: () => gate.SelectedIndex == 0);
        var state = new InitFormState([gate, dependent]);

        state.VisibleFields.Should().ContainSingle();
        state.MoveUp();
        state.Enter();
        state.MoveUp();
        state.Enter();

        state.VisibleFields.Should().Equal(gate, dependent);
        state.AcceptRow.Should().Be(2);
    }

    private static List<FormField> SampleFields() =>
    [
        new FormField(
            "Channel",
            [new FieldChoice("a", ""), new FieldChoice("b", ""), new FieldChoice("c", "")],
            defaultIndex: 0),
        new FormField(
            "Mode",
            [new FieldChoice("x", ""), new FieldChoice("y", "")],
            defaultIndex: 0),
    ];

    private static List<FormField> FieldsWithCustom() =>
    [
        new FormField(
            "Channel",
            [new FieldChoice("a", ""), new FieldChoice("custom", "", IsCustomInput: true)],
            defaultIndex: 0),
    ];

    private static InitFormState FocusFirstField(IReadOnlyList<FormField> fields)
    {
        var state = new InitFormState(fields);
        for (int index = state.AcceptRow; index > 0; index--)
        {
            state.MoveUp();
        }

        return state;
    }
}
