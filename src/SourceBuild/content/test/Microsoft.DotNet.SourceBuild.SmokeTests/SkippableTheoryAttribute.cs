// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System.Runtime.CompilerServices;
using Xunit;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

/// <summary>
/// A Theory that will be skipped based on the specified environment variable's value.
/// </summary>
internal class SkippableTheoryAttribute : TheoryAttribute
{
    public SkippableTheoryAttribute([CallerMemberName] string testName = "") =>
        SkippableFactAttribute.CheckIncluded(testName, (skip) => Skip = skip);

    public SkippableTheoryAttribute(string envName, bool skipOnNullOrWhiteSpace = false, bool skipOnTrue = false, [CallerMemberName] string testName = "") =>
        SkippableFactAttribute.CheckEnvs(skipOnNullOrWhiteSpace, skipOnTrue, (skip) => Skip = skip, testName, envName);

    public SkippableTheoryAttribute(string[] envNames, bool skipOnNullOrWhiteSpace = false, bool skipOnTrue = false, [CallerMemberName] string testName = "") =>
        SkippableFactAttribute.CheckEnvs(skipOnNullOrWhiteSpace, skipOnTrue, (skip) => Skip = skip, testName, envNames);
}
