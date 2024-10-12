// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks.Test;

// Set of things to test:
// Literals 'a'
// Multiple literals 'a/b'
// Extensions '*.a'
// Longer extensions first '*.a', '*.b.a'
// Extensions at the beginning '*.a/b'
// Extensions at the end 'a/*.b'
// Extensions in the middle 'a/*.b/c'
// Wildcard '*'
// Wildcard at the beginning '*/a'
// Wildcard at the end 'a/*'
// Wildcard in the middle 'a/*/c'
// Recursive wildcard '**'
// Recursive wildcard at the beginning '**/a'
// Recursive wildcard at the end 'a/**'
// Recursive wildcard in the middle 'a/**/c'
public partial class StaticWebAssetGlobMatcherTest
{
    [Fact]
    public void CanMatchLiterals()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("a");
        var globMatcher = matcher.Build();

        var match = globMatcher.Match("a");
        Assert.True(match.IsMatch);
        Assert.Equal("a", match.Pattern);
        Assert.Equal("a", match.Stem);
    }

    [Fact]
    public void CanMatchMultipleLiterals()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("a/b");
        var globMatcher = matcher.Build();

        var match = globMatcher.Match("a/b");
        Assert.True(match.IsMatch);
        Assert.Equal("a/b", match.Pattern);
        Assert.Equal("b", match.Stem);
    }

    [Fact]
    public void CanMatchExtensions()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("*.a");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a.a");
        Assert.True(match.IsMatch);
        Assert.Equal("*.a", match.Pattern);
        Assert.Equal("a.a", match.Stem);
    }

    [Fact]
    public void MatchesLongerExtensionsFirst()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("*.a", "*.b.a");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("c.b.a");
        Assert.True(match.IsMatch);
        Assert.Equal("*.b.a", match.Pattern);
        Assert.Equal("c.b.a", match.Stem);
    }

    [Fact]
    public void CanMatchExtensionsAtTheBeginning()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("*.a/b");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("c.a/b");
        Assert.True(match.IsMatch);
        Assert.Equal("*.a/b", match.Pattern);
        Assert.Equal("b", match.Stem);
    }

    [Fact]
    public void CanMatchExtensionsAtTheEnd()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("a/*.b");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a/c.b");
        Assert.True(match.IsMatch);
        Assert.Equal("a/*.b", match.Pattern);
        Assert.Equal("c.b", match.Stem);
    }

    [Fact]
    public void CanMatchExtensionsInMiddle()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("a/*.b/c");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a/d.b/c");
        Assert.True(match.IsMatch);
        Assert.Equal("a/*.b/c", match.Pattern);
        Assert.Equal("c", match.Stem);
    }

    [Theory]
    [InlineData("a*")]
    [InlineData("*a")]
    [InlineData("?")]
    [InlineData("*?")]
    [InlineData("?*")]
    [InlineData("**a")]
    [InlineData("a**")]
    [InlineData("**?")]
    [InlineData("?**")]
    public void CanMatchComplexSegments(string pattern)
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns(pattern);
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a");
        Assert.True(match.IsMatch);
        Assert.Equal(pattern, match.Pattern);
        Assert.Equal("a", match.Stem);
    }

    [Theory]
    [InlineData("a?", "aa", true)]
    [InlineData("a?", "a", false)]
    [InlineData("a?", "aaa", false)]
    [InlineData("?a", "aa", true)]
    [InlineData("?a", "a", false)]
    [InlineData("?a", "aaa", false)]
    [InlineData("a?a", "aaa", true)]
    [InlineData("a?a", "aba", true)]
    [InlineData("a?a", "abaa", false)]
    [InlineData("a?a", "ab", false)]
    public void QuestionMarksMatchSingleCharacter(string pattern, string input, bool expectedMatchResult)
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns(pattern);
        var globMatcher = matcher.Build();
        var match = globMatcher.Match(input);
        Assert.Equal(expectedMatchResult, match.IsMatch);
        if(expectedMatchResult)
        {
            Assert.Equal(pattern, match.Pattern);
            Assert.Equal(input, match.Stem);
        }
        else
        {
            Assert.Null(match.Pattern);
            Assert.Null(match.Stem);
        }
    }

    [Theory]
    [InlineData("a??", "aaa", true)]
    [InlineData("a??", "aa", false)]
    [InlineData("a??", "aaaa", false)]
    [InlineData("?a?", "aaa", true)]
    [InlineData("?a?", "aa", false)]
    [InlineData("?a?", "aaaa", false)]
    [InlineData("??a", "aaa", true)]
    [InlineData("??a", "aa", false)]
    [InlineData("??a", "aaaa", false)]
    [InlineData("a??a", "aaaa", true)]
    [InlineData("a??a", "aaba", true)]
    [InlineData("a??a", "aabaa", false)]
    [InlineData("a??a", "aba", false)]
    public void MultipleQuestionMarksMatchExactlyTheNumberOfCharacters(string pattern, string input, bool expectedMatchResult)
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns(pattern);
        var globMatcher = matcher.Build();
        var match = globMatcher.Match(input);
        Assert.Equal(expectedMatchResult, match.IsMatch);
        if (expectedMatchResult)
        {
            Assert.Equal(pattern, match.Pattern);
            Assert.Equal(input, match.Stem);
        }
        else
        {
            Assert.Null(match.Pattern);
            Assert.Null(match.Stem);
        }
    }

    [Theory]
    [InlineData("a*", "a", true)]
    [InlineData("a*", "aa", true)]
    [InlineData("a*", "aaa", true)]
    [InlineData("a*", "aaaa", true)]
    [InlineData("a*", "aaaaa", true)]
    [InlineData("a*", "aaaaaa", true)]
    [InlineData("*a", "a", true)]
    [InlineData("*a", "aa", true)]
    [InlineData("*a", "aaa", true)]
    [InlineData("*a", "aaaa", true)]
    [InlineData("*a", "aaaaa", true)]
    [InlineData("a*a", "a", false)]
    [InlineData("a*a", "aa", true)]
    [InlineData("a*a", "aaa", true)]
    [InlineData("a*a", "aaaaa", true)]
    [InlineData("a*a", "aaaaaa", true)]
    [InlineData("a*a", "aba", true)]
    [InlineData("a*a", "abaa", true)]
    [InlineData("a*a", "abba", true)]
    [InlineData("a*b", "ab", true)]
    public void WildCardsMatchZeroOrMoreCharacters(string pattern, string input, bool expectedMatchResult)
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns(pattern);
        var globMatcher = matcher.Build();
        var match = globMatcher.Match(input);
        Assert.Equal(expectedMatchResult, match.IsMatch);
        if (expectedMatchResult)
        {
            Assert.Equal(pattern, match.Pattern);
            Assert.Equal(input, match.Stem);
        }
        else
        {
            Assert.Null(match.Pattern);
            Assert.Null(match.Stem);
        }
    }

    [Theory]
    [InlineData("*?a", "a", false)]
    [InlineData("*?a", "aa", true)]
    [InlineData("*?a", "aaa", true)]
    [InlineData("*?a", "aaaa", true)]
    [InlineData("*?a", "aaaaa", true)]
    [InlineData("*?a", "aaaaaa", true)]
    [InlineData("*??a", "aa", false)]
    [InlineData("*??a", "aaa", true)]
    [InlineData("*???a", "aaa", false)]
    [InlineData("*???a", "aaaa", true)]
    [InlineData("*????a", "aaaa", false)]
    [InlineData("*????a", "aaaaa", true)]
    [InlineData("*?????a", "aaaaa", false)]
    [InlineData("*?????a", "aaaaaa", true)]
    [InlineData("*??????a", "aaaaaa", false)]
    [InlineData("*??????a", "aaaaaaa", true)]
    [InlineData("a*?", "a", false)]
    [InlineData("a*?", "aa", true)]
    [InlineData("a*?", "aaa", true)]
    [InlineData("a*?", "aaaa", true)]
    [InlineData("a*?", "aaaaa", true)]
    [InlineData("a*?", "aaaaaa", true)]
    [InlineData("a*??", "aa", false)]
    [InlineData("a*??", "aaa", true)]
    [InlineData("a*???", "aaa", false)]
    [InlineData("a*???", "aaaa", true)]
    [InlineData("a*????", "aaaa", false)]
    [InlineData("a*????", "aaaaa", true)]
    [InlineData("a*?????", "aaaaa", false)]
    [InlineData("a*?????", "aaaaaa", true)]
    [InlineData("a*??????", "aaaaaa", false)]
    [InlineData("a*??????", "aaaaaaa", true)]

    public void SingleWildcardPrecededOrSucceededByQuestionMarkRequireMinimumNumberOfCharacters(string pattern, string input, bool expectedMatchResult)
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns(pattern);
        var globMatcher = matcher.Build();
        var match = globMatcher.Match(input);
        Assert.Equal(expectedMatchResult, match.IsMatch);
        if (expectedMatchResult)
        {
            Assert.Equal(pattern, match.Pattern);
            Assert.Equal(input, match.Stem);
        }
        else
        {
            Assert.Null(match.Pattern);
            Assert.Null(match.Stem);
        }
    }

    [Fact]
    public void CanMatchWildCard()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("*");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a");
        Assert.True(match.IsMatch);
        Assert.Equal("*", match.Pattern);
        Assert.Equal("a", match.Stem);
    }

    [Fact]
    public void CanMatchWildCardAtTheBeginning()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("*/a");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("c/a");
        Assert.True(match.IsMatch);
        Assert.Equal("*/a", match.Pattern);
        Assert.Equal("a", match.Stem);
    }

    [Fact]
    public void CanMatchWildCardAtTheEnd()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("a/*");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a/c");
        Assert.True(match.IsMatch);
        Assert.Equal("a/*", match.Pattern);
        Assert.Equal("c", match.Stem);
    }

    [Fact]
    public void CanMatchWildCardInMiddle()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("a/*/c");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a/b/c");
        Assert.True(match.IsMatch);
        Assert.Equal("a/*/c", match.Pattern);
        Assert.Equal("c", match.Stem);
    }

    [Fact]
    public void CanMatchRecursiveWildCard()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("**");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a/b/c");
        Assert.True(match.IsMatch);
        Assert.Equal("**", match.Pattern);
        Assert.Equal("a/b/c", match.Stem);
    }

    [Fact]
    public void CanMatchRecursiveWildCardAtTheBeginning()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("**/a");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("c/b/a");
        Assert.True(match.IsMatch);
        Assert.Equal("**/a", match.Pattern);
        Assert.Equal("c/b/a", match.Stem);
    }

    [Fact]
    public void CanMatchRecursiveWildCardAtTheEnd()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("a/**");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a/b/c");
        Assert.True(match.IsMatch);
        Assert.Equal("a/**", match.Pattern);
        Assert.Equal("b/c", match.Stem);
    }

    [Fact]
    public void CanMatchRecursiveWildCardInMiddle()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("a/**/c");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a/b/c");
        Assert.True(match.IsMatch);
        Assert.Equal("a/**/c", match.Pattern);
        Assert.Equal("b/c", match.Stem);
    }
}
