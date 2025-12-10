// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.DotNet.Cli.Utils;

namespace Microsoft.DotNet.Cli.Telemetry;

/// <summary>
/// Base class for environment detection rules that can be evaluated against environment variables.
/// </summary>
internal abstract class EnvironmentDetectionRule
{
    /// <summary>
    /// Evaluates the rule against the current environment.
    /// </summary>
    /// <returns>True if the rule matches the current environment; otherwise, false.</returns>
    public abstract bool IsMatch();
}

/// <summary>
/// Rule that matches when any of the specified environment variables is set to "true".
/// </summary>
internal class BooleanEnvironmentRule : EnvironmentDetectionRule
{
    private readonly string[] _variables;

    public BooleanEnvironmentRule(params string[] variables)
    {
        _variables = variables ?? throw new ArgumentNullException(nameof(variables));
    }

    public override bool IsMatch()
    {
        return _variables.Any(variable => Env.GetEnvironmentVariableAsBool(variable));
    }
}

/// <summary>
/// Rule that matches when all specified environment variables are present and not null/empty.
/// </summary>
internal class AllPresentEnvironmentRule : EnvironmentDetectionRule
{
    private readonly string[] _variables;

    public AllPresentEnvironmentRule(params string[] variables)
    {
        _variables = variables ?? throw new ArgumentNullException(nameof(variables));
    }

    public override bool IsMatch()
    {
        return _variables.All(variable => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable)));
    }
}

/// <summary>
/// Rule that matches when any of the specified environment variables is present and not null/empty.
/// </summary>
internal class AnyPresentEnvironmentRule : EnvironmentDetectionRule
{
    private readonly string[] _variables;

    public AnyPresentEnvironmentRule(params string[] variables)
    {
        _variables = variables ?? throw new ArgumentNullException(nameof(variables));
    }

    public override bool IsMatch()
    {
        return _variables.Any(variable => !string.IsNullOrEmpty(Environment.GetEnvironmentVariable(variable)));
    }
}

/// <summary>
/// Rule that matches when any of the specified environment variables is present and not null/empty,
/// and returns the associated result value.
/// </summary>
/// <typeparam name="T">The type of the result value.</typeparam>
internal class EnvironmentDetectionRuleWithResult<T> where T : class
{
    private readonly EnvironmentDetectionRule _rule;
    private readonly T _result;

    public EnvironmentDetectionRuleWithResult(T result, EnvironmentDetectionRule rule)
    {
        _rule = rule ?? throw new ArgumentNullException(nameof(rule));
        _result = result ?? throw new ArgumentNullException(nameof(result));
    }

    /// <summary>
    /// Evaluates the rule and returns the result if matched.
    /// </summary>
    /// <returns>The result value if the rule matches; otherwise, null.</returns>
    public T? GetResult()
    {
        return _rule.IsMatch()
            ? _result 
            : null;
    }
}