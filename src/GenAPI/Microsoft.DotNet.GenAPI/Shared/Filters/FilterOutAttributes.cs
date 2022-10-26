// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.IO;
using System.Collections.Generic;
using Microsoft.CodeAnalysis;
using System.Linq;

namespace Microsoft.DotNet.GenAPI.Shared;

public class FilterOutAttributes : IncludeAllFilter
{
    private readonly HashSet<string> _attributes;

    public FilterOutAttributes(IEnumerable<string> attributes)
    {
        _attributes = new HashSet<string>(attributes);
    }

    public FilterOutAttributes(string attributeDocIdsFile)
    {
        _attributes = new HashSet<string>(ReadDocIdsAttributes(attributeDocIdsFile));
    }

    /// <inheritdoc />
    public override bool Includes(AttributeData at)
    {
        if (at.AttributeClass == null) return true;
        return !_attributes.Contains(at.AttributeClass.ToString()!);
    }

    private static IEnumerable<string> ReadDocIdsAttributes(string docIdsFile)
    {
        if (!File.Exists(docIdsFile))
            yield break;

        foreach (string id in File.ReadAllLines(docIdsFile))
        {
            if (string.IsNullOrWhiteSpace(id) || !id.StartsWith("T:"))
                continue;

            yield return id.Trim().Substring(startIndex: 2); // skip `T:`
        }
    }
}
