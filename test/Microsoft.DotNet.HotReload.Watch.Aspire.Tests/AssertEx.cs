// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watch.UnitTests;

/// <summary>
/// Small project-local helper that mirrors the shape of the xUnit-based AssertEx
/// in <see cref="!:Microsoft.DotNet.HotReload.Test.Utilities" /> for the subset of
/// helpers used by the unit tests in this project. Keeping it local lets this project
/// stay independent from the xUnit-coupled shared test utilities.
/// </summary>
internal static class AssertEx
{
    public static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string? message = null)
    {
        Assert.IsNotNull(actual);

        if (!expected.SequenceEqual(actual))
        {
            var expectedString = string.Join(Environment.NewLine, expected.Select(FormatItem));
            var actualString = string.Join(Environment.NewLine, actual.Select(FormatItem));
            Assert.Fail(
                (message is null ? string.Empty : message + Environment.NewLine) +
                $"Expected:{Environment.NewLine}{expectedString}{Environment.NewLine}" +
                $"Actual:{Environment.NewLine}{actualString}");
        }

        static string FormatItem(T item) => item?.ToString() ?? "<null>";
    }
}
