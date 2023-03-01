// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Linq;
using System.Runtime.CompilerServices;
using Xunit;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

/// <summary>
/// A Fact that will be skipped based on the specified environment variable's value.
/// </summary>
internal class SkippableFactAttribute : FactAttribute
{
    public SkippableFactAttribute([CallerMemberName] string name = "") =>
        CheckIncluded(name, (skip) => Skip = skip);

    public SkippableFactAttribute(string envName, bool skipOnNullOrWhiteSpace = false, bool skipOnTrue = false, [CallerMemberName] string testName = "") =>
        CheckEnvs(skipOnNullOrWhiteSpace, skipOnTrue, (skip) => Skip = skip, testName, envName);

    public SkippableFactAttribute(string[] envNames, bool skipOnNullOrWhiteSpace = false, bool skipOnTrue = false, [CallerMemberName] string testName = "") =>
        CheckEnvs(skipOnNullOrWhiteSpace, skipOnTrue, (skip) => Skip = skip, testName, envNames);

    public static void CheckIncluded(string methodName, Action<string> setSkip)
    {
        var included = Config.IncludedTests;
        if (included.Length != 0 && !included.Contains(methodName))
        {
            setSkip($"Skipping because `{methodName}` is not included");
            return;
        }
    }

    public static void CheckEnvs(bool skipOnNullOrWhiteSpace, bool skipOnTrue, Action<string> setSkip, string testName, params string[] envNames)
    {
        CheckIncluded(testName, setSkip);

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
