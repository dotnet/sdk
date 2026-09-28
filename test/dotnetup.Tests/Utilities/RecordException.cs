// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Tools.Dotnetup.Tests;

/// <summary>
/// Minimal replacement for xUnit's <c>Record</c> helper, used by tests that captured an
/// exception (or the absence of one) from a delegate. MSTest has no built-in equivalent.
/// </summary>
internal static class Record
{
    /// <summary>
    /// Runs <paramref name="testCode"/> and returns the exception it throws, or
    /// <see langword="null"/> if it completes without throwing.
    /// </summary>
    public static Exception? Exception(Action testCode)
    {
        try
        {
            testCode();
            return null;
        }
        catch (Exception ex)
        {
            return ex;
        }
    }
}
