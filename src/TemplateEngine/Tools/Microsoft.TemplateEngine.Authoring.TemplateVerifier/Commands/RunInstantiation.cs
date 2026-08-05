// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Authoring.TemplateVerifier.Commands;

/// <summary>
/// Runs the template instantiation (and installation if needed) based on given options.
/// </summary>
/// <param name="options"></param>
/// <returns></returns>
public delegate Task<IInstantiationResult> RunInstantiation(TemplateVerifierOptions options);
