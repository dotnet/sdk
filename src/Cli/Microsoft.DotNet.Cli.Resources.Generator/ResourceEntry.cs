// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Resources.Generator;

/// <summary>
///  Describes one generated string resource member.
/// </summary>
internal sealed class ResourceEntry
{
    /// <summary>
    ///  Initializes a new instance of the <see cref="ResourceEntry"/> class.
    /// </summary>
    /// <param name="name">The resource key.</param>
    /// <param name="value">The resource value.</param>
    /// <param name="identifier">The generated C# member identifier.</param>
    /// <param name="line">The zero-based source line.</param>
    /// <param name="column">The zero-based source column.</param>
    /// <param name="formatArguments">The format arguments found in the value.</param>
    internal ResourceEntry(
        string name,
        string value,
        string identifier,
        int line,
        int column,
        FormatArguments formatArguments)
    {
        Name = name;
        Value = value;
        Identifier = identifier;
        Line = line;
        Column = column;
        FormatArguments = formatArguments;
        EmitFormatMethod = true;
    }

    /// <summary>
    ///  Gets the resource key.
    /// </summary>
    internal string Name { get; }

    /// <summary>
    ///  Gets the resource value.
    /// </summary>
    internal string Value { get; }

    /// <summary>
    ///  Gets the generated C# member identifier.
    /// </summary>
    internal string Identifier { get; }

    /// <summary>
    ///  Gets the zero-based source line.
    /// </summary>
    internal int Line { get; }

    /// <summary>
    ///  Gets the zero-based source column.
    /// </summary>
    internal int Column { get; }

    /// <summary>
    ///  Gets the format arguments found in the value.
    /// </summary>
    internal FormatArguments FormatArguments { get; }

    /// <summary>
    ///  Gets or sets a value indicating whether a format method should be generated.
    /// </summary>
    internal bool EmitFormatMethod { get; set; }
}