// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;

namespace Microsoft.TemplateEngine.Abstractions.Parameters;

/// <summary>
/// Data model for bound and merged dataset to be used to substitute and evaluate active sections within templates.
///  Data are possibly merged from multiple sources (default values in definition, values supplied by host, user, etc.).
/// </summary>
public interface IParameterSetData : IReadOnlyDictionary<ITemplateParameter, ParameterData>
{
    /// <summary>
    /// Descriptors for the parameters - inferred from the template.
    /// </summary>
    IParameterDefinitionSet ParametersDefinition { get; }
}
