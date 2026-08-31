// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// Pairs a summary selector option with the <see cref="WalkthroughDecision"/> it produces, so the
/// option's display order and its resulting decision are defined together in one place instead of
/// being matched up by index across separate methods.
/// </summary>
/// <param name="Option">The option rendered in the selector.</param>
/// <param name="Decision">The decision produced when this option is chosen.</param>
internal sealed record SummaryChoice(SelectableOption Option, WalkthroughDecision Decision);
