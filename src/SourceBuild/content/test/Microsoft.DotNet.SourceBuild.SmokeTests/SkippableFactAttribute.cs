// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using Xunit;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

/// <summary>
/// A Fact that will be skipped based on the specified environment variable's value.
/// </summary>
internal class SkippableFactAttribute : FactAttribute
{
    public SkippableFactAttribute(string envName, bool skipOnNullOrWhiteSpace = false, bool skipOnTrue = false) =>
        CheckEnvs(skipOnNullOrWhiteSpace, skipOnTrue, (skip) => Skip = skip, envName);

    public SkippableFactAttribute(string[] envNames, bool skipOnNullOrWhiteSpace = false, bool skipOnTrue = false) =>
        CheckEnvs(skipOnNullOrWhiteSpace, skipOnTrue, (skip) => Skip = skip, envNames);

    public static void CheckEnvs(bool skipOnNullOrWhiteSpace, bool skipOnTrue, Action<string> setSkip, params string[] envNames)
    {
        foreach (string envName in envNames)
        {
            string? envValue = Environment.GetEnvironmentVariable(envName);

            if (skipOnNullOrWhiteSpace && string.IsNullOrWhiteSpace(envValue))
            {
                setSkip($"Skipping because `{envName}` is null or whitespace");
                break;
            }
            else if (skipOnTrue && bool.TryParse(envValue, out bool boolValue) && boolValue)
            {
                setSkip($"Skipping because `{envName}` is set to True");
                break;
            }
        }
    }
}
