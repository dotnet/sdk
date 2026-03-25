// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.NET.TestFramework
{
    /// <summary>
    /// Controls which platforms and architectures a theory should run on or be skipped on.
    /// See <see cref="PlatformSpecificFact"/> for full documentation on the filtering properties.
    /// </summary>
    public class PlatformSpecificTheory : TheoryAttribute
    {
        private readonly TestPlatforms _platforms;
        private string? _skip;

        public PlatformSpecificTheory() : this(TestPlatforms.Any)
        {
        }

        public PlatformSpecificTheory(TestPlatforms platforms)
        {
            _platforms = platforms;
        }

        /// <inheritdoc cref="PlatformSpecificFact.SkipPlatforms"/>
        public TestPlatforms SkipPlatforms { get; set; }

        /// <inheritdoc cref="PlatformSpecificFact.Architecture"/>
        public Architecture Architecture { get; set; } = PlatformSpecificFact.NoArchitectureFilter;

        /// <inheritdoc cref="PlatformSpecificFact.SkipArchitecture"/>
        public Architecture SkipArchitecture { get; set; } = PlatformSpecificFact.NoArchitectureFilter;

        /// <inheritdoc cref="PlatformSpecificFact.SkipReason"/>
        public string? SkipReason { get; set; }

        public override string? Skip
        {
            get => _skip ?? PlatformSpecificFact.EvaluateSkip(_platforms, SkipPlatforms, Architecture, SkipArchitecture, SkipReason);
            set => _skip = value;
        }
    }
}
