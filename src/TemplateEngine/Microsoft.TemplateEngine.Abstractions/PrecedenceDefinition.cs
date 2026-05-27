// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Abstractions;

/// <summary>
/// Indicates parameter defined precedence.
/// </summary>
public enum PrecedenceDefinition
{
    // If enable condition is set - parameter is conditionally disabled (regardless if require condition is set or not)
    // Conditionally required is if and only if the only require condition is set.

    /// <summary>
    /// Parameter value is unconditionally required.
    /// </summary>
    Required,

    /// <summary>
    /// Set if and only if only the IsRequiredCondition is set.
    /// </summary>
    ConditionalyRequired,

    /// <summary>
    /// Parameter value is not required from user.
    /// </summary>
    Optional,

    /// <summary>
    /// Parameter value is implicitly populated.
    /// </summary>
    Implicit,

    /// <summary>
    /// Parameter might become disabled - value would not be needed nor used in such case.
    /// </summary>
    ConditionalyDisabled,

    /// <summary>
    /// Parameter is disabled - it's value is not required and will not be used.
    /// </summary>
    Disabled,
}
