// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.NET.TestFramework
{
    /// <summary>
    /// Controls which platforms and architectures a theory should run on or be skipped on.
    /// See <see cref="PlatformSpecificFact"/> for full documentation on the filtering parameters.
    /// </summary>
    public class PlatformSpecificTheory : TheoryAttribute
    {
        public PlatformSpecificTheory(
            TestPlatforms platforms = TestPlatforms.Any,
            TestPlatforms skipPlatforms = 0,
            Architecture architecture = PlatformSpecificFact.NoArchitectureFilter,
            Architecture skipArchitecture = PlatformSpecificFact.NoArchitectureFilter,
            string? skipReason = null,
            [CallerFilePath] string? sourceFilePath = null,
            [CallerLineNumber] int sourceLineNumber = 0)
            : base(sourceFilePath, sourceLineNumber)
        {
            Skip = PlatformSpecificFact.EvaluateSkip(platforms, skipPlatforms, architecture, skipArchitecture, skipReason);
        }
    }
}
