// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init.Form;

/// <summary>
/// A single informational line shown in a field's detail area (e.g. "Installs to: ~/.dotnet").
/// </summary>
/// <param name="Label">The descriptive text (or a full bullet line when <paramref name="Value"/> is null).</param>
/// <param name="Value">An optional value to emphasize after the label (e.g. a path); null for plain lines.</param>
internal sealed record DetailLine(string Label, string? Value = null);
