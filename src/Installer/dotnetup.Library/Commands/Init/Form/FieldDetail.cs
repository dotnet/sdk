// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init.Form;

/// <summary>
/// The detail shown for a field's selected (or highlighted) value: its help text plus any derived
/// information lines (e.g. install location, profile file, the system installs that would migrate).
/// </summary>
/// <param name="HelperText">The selected value's descriptive help text.</param>
/// <param name="Lines">Derived informational lines specific to the field/value; may be empty.</param>
internal sealed record FieldDetail(string HelperText, IReadOnlyList<DetailLine> Lines);
