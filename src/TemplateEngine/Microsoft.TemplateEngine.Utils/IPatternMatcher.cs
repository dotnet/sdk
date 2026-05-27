// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.TemplateEngine.Utils;

/// <summary>
/// Interface for pattern matchers.
/// </summary>
public interface IPatternMatcher
{
    /// <summary>
    /// Evaluates the test path against the pattern and returns whether the path is a match.
    /// </summary>
    /// <param name="test"></param>
    /// <returns></returns>
    bool IsMatch(string test);
}
