// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Bootstrapper.Commands.Init.Form;

/// <summary>
/// Ordered steps used to compress the init form. Each level includes all reductions from the
/// preceding levels, so individual form elements can consistently compare against this sequence.
/// </summary>
internal enum FormCompressionLevel
{
    /// <summary>Shows the complete form with descriptions, details, and generous spacing.</summary>
    Rich,

    /// <summary>Removes extra spacing and details beneath fields that are not focused.</summary>
    Compact,

    /// <summary>Removes the welcome message from the header.</summary>
    WithoutWelcome,

    /// <summary>Removes the installation-location message from the header.</summary>
    WithoutInstallLocation,

    /// <summary>Removes the remaining details shown beneath field rows.</summary>
    WithoutFieldDetails,

    /// <summary>Removes the question displayed immediately before the accept action.</summary>
    WithoutConfirmationPrompt,

    /// <summary>While editing, shows only the focused field and removes the accept action.</summary>
    FocusedEdit,

    /// <summary>Moves choice descriptions beside their choices instead of placing them below.</summary>
    InlineChoiceHelp,

    /// <summary>Removes details describing the changes produced by the selected choice.</summary>
    WithoutDerivedDetails,

    /// <summary>Displays choices horizontally for fields that support the horizontal layout.</summary>
    HorizontalChoices,

    /// <summary>Removes the keyboard-navigation instructions at the bottom of the form.</summary>
    WithoutNavigationLegend,
}
