// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiDiff;

public interface IDiffGenerator
{
    /// <summary>
    /// Gets the results of the diff. The key is the assembly name and the value is the diff. This dictionary might get populated after calling <see cref="Run" />, depending on the use case.
    /// </summary>
    IReadOnlyDictionary<string, string> Results { get; }

    /// <summary>
    /// Runs the diff generator and may populate the <see cref="Results"/> dictionary depending on the use case.
    /// </summary>
    void Run();
}
