// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Xunit;

namespace Microsoft.DotNet.SourceBuild.SmokeTests;

/// <summary>
/// A Theory that will be skipped based on the specified environment variable's value.
/// </summary>
internal class SkippableTheoryAttribute : TheoryAttribute
{
    public SkippableTheoryAttribute(string envName, bool skipOnNullOrWhiteSpace = false, bool skipOnTrue = false) =>
        SkippableFactAttribute.CheckEnvs(skipOnNullOrWhiteSpace, skipOnTrue, (skip) => Skip = skip, envName);

    public SkippableTheoryAttribute(string[] envNames, bool skipOnNullOrWhiteSpace = false, bool skipOnTrue = false) =>
        SkippableFactAttribute.CheckEnvs(skipOnNullOrWhiteSpace, skipOnTrue, (skip) => Skip = skip, envNames);
}
