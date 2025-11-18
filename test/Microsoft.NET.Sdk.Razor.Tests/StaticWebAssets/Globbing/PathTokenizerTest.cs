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
        var tokenizer = new PathTokenizer(path.AsMemory().Span);
        var segments = new List<PathTokenizer.Segment>();
        var collection = tokenizer.Fill(segments);
        Assert.Equal("a", collection[0]);
        Assert.Equal("b", collection[1]);
        Assert.Equal("c", collection[2]);
    }

    [Fact]
    public void NonRootSeparator_ProducesInitialSegment()
    {
        var path = "a/b/c";
        var tokenizer = new PathTokenizer(path.AsMemory().Span);
        var segments = new List<PathTokenizer.Segment>();
        var collection = tokenizer.Fill(segments);
        Assert.Equal("a", collection[0]);
        Assert.Equal("b", collection[1]);
        Assert.Equal("c", collection[2]);
    }

    [Fact]
    public void NonRootSeparator_MatchesMultipleCharacters()
    {
        var path = "aa/b/c";
        var tokenizer = new PathTokenizer(path.AsMemory().Span);
        var segments = new List<PathTokenizer.Segment>();
        var collection = tokenizer.Fill(segments);
        Assert.Equal("aa", collection[0]);
        Assert.Equal("b", collection[1]);
        Assert.Equal("c", collection[2]);
    }

    [Fact]
    public void NonRootSeparator_HandlesConsecutivePathSeparators()
    {
        var path = "aa//b/c";
        var tokenizer = new PathTokenizer(path.AsMemory().Span);
        var segments = new List<PathTokenizer.Segment>();
        var collection = tokenizer.Fill(segments);
        Assert.Equal("aa", collection[0]);
        Assert.Equal("b", collection[1]);
        Assert.Equal("c", collection[2]);
    }

    [Fact]
    public void NonRootSeparator_HandlesFinalPathSeparator()
    {
        var path = "aa/b/c/";
        var tokenizer = new PathTokenizer(path.AsMemory().Span);
        var segments = new List<PathTokenizer.Segment>();
        var collection = tokenizer.Fill(segments);
        Assert.Equal("aa", collection[0]);
        Assert.Equal("b", collection[1]);
        Assert.Equal("c", collection[2]);
    }

    [Fact]
    public void NonRootSeparator_HandlesAlternativePathSeparators()
    {
        var path = "aa\\b\\c\\";
        var tokenizer = new PathTokenizer(path.AsMemory().Span);
        var segments = new List<PathTokenizer.Segment>();
        var collection = tokenizer.Fill(segments);
        Assert.Equal("aa", collection[0]);
        Assert.Equal("b", collection[1]);
        Assert.Equal("c", collection[2]);
    }

    [Fact]
    public void NonRootSeparator_HandlesMixedPathSeparators()
    {
        var path = "aa/b\\c/";
        var tokenizer = new PathTokenizer(path.AsMemory().Span);
        var segments = new List<PathTokenizer.Segment>();
        var collection = tokenizer.Fill(segments);
        Assert.Equal("aa", collection[0]);
        Assert.Equal("b", collection[1]);
        Assert.Equal("c", collection[2]);
    }

    [Fact]
    public void Ignores_EmpySegments()
    {
        var path = "aa//b//c";
        var tokenizer = new PathTokenizer(path.AsMemory().Span);
        var segments = new List<PathTokenizer.Segment>();
        var collection = tokenizer.Fill(segments);
        Assert.Equal("aa", collection[0]);
        Assert.Equal("b", collection[1]);
        Assert.Equal("c", collection[2]);
    }

    [Fact]
    public void Ignores_DotSegments()
    {
        var path = "./aa/./b/./c/.";
        var tokenizer = new PathTokenizer(path.AsMemory().Span);
        var segments = new List<PathTokenizer.Segment>();
        var collection = tokenizer.Fill(segments);
        Assert.Equal("aa", collection[0]);
        Assert.Equal("b", collection[1]);
        Assert.Equal("c", collection[2]);
    }

    [Fact]
    public void Ignores_DotDotSegments()
    {
        var path = "../aa/../b/../c/..";
        var tokenizer = new PathTokenizer(path.AsMemory().Span);
        var segments = new List<PathTokenizer.Segment>();
        var collection = tokenizer.Fill(segments);
        Assert.Equal("aa", collection[0]);
        Assert.Equal("b", collection[1]);
        Assert.Equal("c", collection[2]);
    }
}
