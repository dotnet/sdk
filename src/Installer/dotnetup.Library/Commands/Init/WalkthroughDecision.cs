// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init;

/// <summary>
/// The outcome the user selects on the init walkthrough summary screen.
/// </summary>
internal enum WalkthroughDecision
{
    /// <summary>Install using the recommended defaults shown in the summary, with no further prompts.</summary>
    Proceed,

    /// <summary>Run the full step-by-step walkthrough (channel, mode, and migration prompts).</summary>
    Customize,

    /// <summary>Make no changes and exit.</summary>
    Exit,
}
