// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using FluentAssertions.Primitives;
using DiffPlex.Renderer;
using System.Diagnostics.CodeAnalysis;

namespace Microsoft.NET.TestFramework.Assertions
{
    public static class StringAssertionsExtensions
    {
        private static string NormalizeLineEndings(string s)
        {
            return s.Replace("\r\n", "\n");
        }

        /// <summary>
        /// Checks that two strings look to same to humans - if not a git-style diff will be reported.
        /// </summary>
        /// <param name="because">Supply a non-default reason for the failure. By default, a git-style diff is reported. If you override this, the <c>diff</c> string will be added to the <paramref name="becauseArgs"/>
        /// so you can use it in your template string.</param>
        public static AndConstraint<StringAssertions> BeVisuallyEquivalentTo(
            this StringAssertions assertions,
            string expected,
#if NET
            [StringSyntax(nameof(CompositeFormat))]
#endif
            string because = "",
            params object[] becauseArgs)
        {
            var normalizedActual = NormalizeLineEndings(assertions.Subject);
            var normalizedExpected = NormalizeLineEndings(expected);
            var areSame = string.Compare(normalizedActual, normalizedExpected, CultureInfo.CurrentCulture, CompareOptions.IgnoreSymbols) == 0;
            if (!areSame)
            {
                var diff = UnidiffRenderer.GenerateUnidiff(oldText: normalizedExpected, newText: normalizedActual, oldFileName: "expected", newFileName: "actual", ignoreWhitespace: true);
                areSame.Should().Be(true, because: string.IsNullOrEmpty(because) ? $"The input strings are not visually equivalent. Diff is:\n{diff}" : because, becauseArgs: [.. becauseArgs, diff]);
            }

            return new AndConstraint<StringAssertions>(assertions);
        }

        /// <summary>
        /// Checks that two strings look to same to humans - if not a git-style diff will be reported.
        /// </summary>
        /// <param name="because">Supply a non-default reason for the failure. By default, a git-style diff is reported. If you override this, the <c>diff</c> string will be added to the <paramref name="becauseArgs"/>
        /// so you can use it in your template string.</param>
        public static AndConstraint<StringAssertions> BeVisuallyEquivalentToIfNotLocalized(
            this StringAssertions assertions,
            string expected,
#if NET
            [StringSyntax(nameof(CompositeFormat))]
#endif
            string because = "",
            params object[] becauseArgs
        )
        {
            if (!TestContext.IsLocalized())
            {
                return BeVisuallyEquivalentTo(assertions, expected, because, becauseArgs);
            }
            return new AndConstraint<StringAssertions>(assertions);
        }

        public static AndConstraint<StringAssertions> ContainVisuallySameFragment(this StringAssertions assertions, string expected, string because = "", params object[] becauseArgs)
        {
            NormalizeLineEndings(assertions.Subject).Contains(NormalizeLineEndings(expected))
                .Should().BeTrue($"String \"{assertions.Subject}\" does not contain visually same fragment string \"{expected}\".");
            return new AndConstraint<StringAssertions>(assertions);
        }

        public static AndConstraint<StringAssertions> ContainVisuallySameFragmentIfNotLocalized(this StringAssertions assertions, string expected, string because = "", params object[] becauseArgs)
        {
            if (!TestContext.IsLocalized())
            {
                return ContainVisuallySameFragment(assertions, expected, because, becauseArgs);
            }
            return new AndConstraint<StringAssertions>(assertions);
        }
    }
}
