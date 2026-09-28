// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init.Form;

/// <summary>
/// One selectable value of a <see cref="FormField"/>.
/// </summary>
/// <param name="Title">The short value label shown both collapsed (as the field's value) and in the expanded picker.</param>
/// <param name="HelperText">Detailed helper/tooltip text shown for this value while the field is expanded.</param>
/// <param name="IsCustomInput">
/// When true, choosing this entry lets the user type a free-text value (e.g. a custom channel name)
/// instead of selecting a fixed value; the typed text becomes the field's displayed value.
/// </param>
internal sealed record FieldChoice(string Title, string HelperText, bool IsCustomInput = false);
