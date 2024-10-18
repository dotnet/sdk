// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks;

public ref struct PathTokenizer(ReadOnlyMemory<char> path)
{
    ReadOnlyMemory<char> _path = path;
    int _index = -1;
    int _nextSeparatorIndex = -1;

    public readonly ReadOnlyMemory<char> Current => _nextSeparatorIndex == -1 ?
        _path.Slice(_index) :
        _path.Slice(_index, _nextSeparatorIndex - _index);

    public bool MoveNext()
    {
        if (_index != -1 && _nextSeparatorIndex == -1)
        {
            return false;
        }

        _index = _nextSeparatorIndex + 1;
        _nextSeparatorIndex = GetSeparator();
        return true;
    }

    internal void Fill(List<ReadOnlyMemory<char>> segments)
    {
        while (MoveNext())
        {
            if (Current.Length > 0 &&
                !Current.Span.Equals(".".AsSpan(), StringComparison.Ordinal) &&
                !Current.Span.Equals("..".AsSpan(), StringComparison.Ordinal))
            {
                segments.Add(Current);
            }
        }
    }

    private int GetSeparator()
    {
        var separatorIndex = _path.Span.Slice(_index).IndexOfAny(OSPath.DirectoryPathSeparators.Span);
        if (separatorIndex == -1)
        {
            return -1;
        }
        return separatorIndex + _index;
    }
}
