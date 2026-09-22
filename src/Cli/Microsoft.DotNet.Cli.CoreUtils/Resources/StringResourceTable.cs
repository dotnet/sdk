// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Resources.Internal;

/// <summary>
///  An immutable-after-publication string table.
/// </summary>
internal sealed class StringResourceTable
{
    private readonly Dictionary<string, string> _strings;

    /// <summary>
    ///  Initializes a resource table from its string values.
    /// </summary>
    /// <param name="strings">The string values.</param>
    internal StringResourceTable(Dictionary<string, string> strings)
    {
        _strings = strings;
    }

    /// <summary>
    ///  The string values in the table.
    /// </summary>
    internal Dictionary<string, string> Strings => _strings;
}