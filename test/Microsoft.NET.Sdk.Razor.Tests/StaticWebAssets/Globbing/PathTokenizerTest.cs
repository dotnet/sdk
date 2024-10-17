// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks.Test;

public class PathTokenizerTest
{
    [Fact]
    public void RootSeparator_ProducesEmptySegment()
    {
        var path = "/a/b/c";
        var tokenizer = new PathTokenizer(path.AsMemory());
        ref var tokenizerRef = ref tokenizer;
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("", tokenizerRef.Current.ToString());
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("a", tokenizerRef.Current.ToString());
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("b", tokenizerRef.Current.ToString());
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("c", tokenizerRef.Current.ToString());
        Assert.False(tokenizerRef.MoveNext());
    }

    [Fact]
    public void NonRootSeparator_ProducesInitialSegment()
    {
        var path = "a/b/c";
        var tokenizer = new PathTokenizer(path.AsMemory());
        ref var tokenizerRef = ref tokenizer;
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("a", tokenizerRef.Current.ToString());
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("b", tokenizerRef.Current.ToString());
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("c", tokenizerRef.Current.ToString());
        Assert.False(tokenizerRef.MoveNext());
    }

    [Fact]
    public void NonRootSeparator_MatchesMultipleCharacters()
    {
        var path = "aa/b/c";
        var tokenizer = new PathTokenizer(path.AsMemory());
        ref var tokenizerRef = ref tokenizer;
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("aa", tokenizerRef.Current.ToString());
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("b", tokenizerRef.Current.ToString());
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("c", tokenizerRef.Current.ToString());
        Assert.False(tokenizerRef.MoveNext());
    }

    [Fact]
    public void NonRootSeparator_HandlesConsecutivePathSeparators()
    {
        var path = "aa//b/c";
        var tokenizer = new PathTokenizer(path.AsMemory());
        ref var tokenizerRef = ref tokenizer;
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("aa", tokenizerRef.Current.ToString());
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("", tokenizerRef.Current.ToString());
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("b", tokenizerRef.Current.ToString());
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("c", tokenizerRef.Current.ToString());
        Assert.False(tokenizerRef.MoveNext());
    }

    [Fact]
    public void NonRootSeparator_HandlesFinalPathSeparator()
    {
        var path = "aa/b/c/";
        var tokenizer = new PathTokenizer(path.AsMemory());
        ref var tokenizerRef = ref tokenizer;
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("aa", tokenizerRef.Current.ToString());
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("b", tokenizerRef.Current.ToString());
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("c", tokenizerRef.Current.ToString());
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("", tokenizerRef.Current.ToString());
        Assert.False(tokenizerRef.MoveNext());
    }

    [ConditionalFact]
    [SkipOnPlatform(TestPlatforms.AnyUnix, "Windows only test")]
    public void NonRootSeparator_HandlesAlternativePathSeparators()
    {
        var path = "aa\\b\\c\\";
        var tokenizer = new PathTokenizer(path.AsMemory());
        ref var tokenizerRef = ref tokenizer;
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("aa", tokenizerRef.Current.ToString());
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("b", tokenizerRef.Current.ToString());
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("c", tokenizerRef.Current.ToString());
        Assert.True(tokenizerRef.MoveNext());
        Assert.Equal("", tokenizerRef.Current.ToString());
        Assert.False(tokenizerRef.MoveNext());
    }
}
