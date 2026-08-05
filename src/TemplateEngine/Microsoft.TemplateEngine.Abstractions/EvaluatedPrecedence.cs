// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions;

/// <summary>
/// Indicates resulting precedence of a parameter (after considering <see cref="PrecedenceDefinition"/> and conditions results).
/// </summary>
public enum EvaluatedPrecedence
{
    /// <summary>
    /// Parameter value is required to be supplied by the host.
    /// </summary>
    Required,

    /// <summary>
    /// Parameter is optional.
    /// </summary>
    Optional,

    /// <summary>
    /// Parameter value is implicitly populated.
    /// </summary>
    Implicit,

    /// <summary>
    /// Parameter is disabled - it's value is not required and will not be used.
    /// </summary>
    Disabled,
}
