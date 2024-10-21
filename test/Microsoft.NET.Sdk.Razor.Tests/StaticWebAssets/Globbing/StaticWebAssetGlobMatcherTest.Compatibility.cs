// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.AspNetCore.StaticWebAssets.Tasks.Test;

public partial class StaticWebAssetGlobMatcherTest
{
    [Fact]
    public void MatchingFileIsFound()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("alpha.txt");
        var globMatcher = matcher.Build();

        var match = globMatcher.Match("alpha.txt");
        Assert.True(match.IsMatch);
        Assert.Equal("alpha.txt", match.Pattern);
    }

    [Fact]
    public void MismatchedFileIsIgnored()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("alpha.txt");
        var globMatcher = matcher.Build();

        var match = globMatcher.Match("omega.txt");
        Assert.False(match.IsMatch);
    }

    [Fact]
    public void FolderNamesAreTraversed()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("beta/alpha.txt");
        var globMatcher = matcher.Build();

        var match = globMatcher.Match("beta/alpha.txt");
        Assert.True(match.IsMatch);
        Assert.Equal("beta/alpha.txt", match.Pattern);
    }

    [Theory]
    [InlineData(@"beta/alpha.txt", @"beta/alpha.txt")]
    [InlineData(@"beta\alpha.txt", @"beta/alpha.txt")]
    [InlineData(@"beta/alpha.txt", @"beta\alpha.txt")]
    [InlineData(@"beta\alpha.txt", @"beta\alpha.txt")]
    [InlineData(@"\beta\alpha.txt", @"beta/alpha.txt")]
    public void SlashPolarityIsIgnored(string includePattern, string filePath)
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns(includePattern);
        var globMatcher = matcher.Build();

        var match = globMatcher.Match(filePath);
        Assert.True(match.IsMatch);
        //Assert.Equal("beta/alpha.txt", match.Pattern);
    }

    [Theory]
    [InlineData(@"alpha.*", new[] { "alpha.txt" })]
    [InlineData(@"*", new[] { "alpha.txt", "beta.txt", "gamma.dat" })]
    [InlineData(@"*et*", new[] { "beta.txt" })]
    [InlineData(@"*.*", new[] { "alpha.txt", "beta.txt", "gamma.dat" })]
    [InlineData(@"b*et*x", new string[0])]
    [InlineData(@"*.txt", new[] { "alpha.txt", "beta.txt" })]
    [InlineData(@"b*et*t", new[] { "beta.txt" })]
    public void CanPatternMatch(string includes, string[] expected)
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns(includes);
        var globMatcher = matcher.Build();

        var matches = new List<string> { "alpha.txt", "beta.txt", "gamma.dat" }
            .Where(file => globMatcher.Match(file).IsMatch)
            .ToArray();

        Assert.Equal(expected, matches);
    }

    [Theory]
    [InlineData(@"12345*5678", new string[0])]
    [InlineData(@"1234*5678", new[] { "12345678" })]
    [InlineData(@"12*23*", new string[0])]
    [InlineData(@"12*3456*78", new[] { "12345678" })]
    [InlineData(@"*45*56", new string[0])]
    [InlineData(@"*67*78", new string[0])]
    public void PatternBeginAndEndCantOverlap(string includes, string[] expected)
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns(includes);
        var globMatcher = matcher.Build();

        var matches = new List<string> { "12345678" }
            .Where(file => globMatcher.Match(file).IsMatch)
            .ToArray();

        Assert.Equal(expected, matches);
    }

    [Theory]
    [InlineData(@"*alpha*/*", new[] { "alpha/hello.txt" })]
    [InlineData(@"/*/*", new[] { "alpha/hello.txt", "beta/hello.txt", "gamma/hello.txt" })]
    [InlineData(@"*/*", new[] { "alpha/hello.txt", "beta/hello.txt", "gamma/hello.txt" })]
    [InlineData(@"/*.*/*", new string[] { })]
    [InlineData(@"*.*/*", new string[] { })]
    [InlineData(@"/*mm*/*", new[] { "gamma/hello.txt" })]
    [InlineData(@"*mm*/*", new[] { "gamma/hello.txt" })]
    [InlineData(@"/*alpha*/*", new[] { "alpha/hello.txt" })]
    public void PatternMatchingWorksInFolders(string includes, string[] expected)
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns(includes);
        var globMatcher = matcher.Build();

        var matches = new List<string> { "alpha/hello.txt", "beta/hello.txt", "gamma/hello.txt" }
            .Where(file => globMatcher.Match(file).IsMatch)
            .ToArray();

        Assert.Equal(expected, matches);
    }

    [Theory]
    [InlineData(@"", new string[] { })]
    [InlineData(@"./", new string[] { "alpha/hello.txt", "beta/hello.txt", "gamma/hello.txt" })]
    [InlineData(@"./alpha/hello.txt", new string[] { "alpha/hello.txt" })]
    [InlineData(@"./**/hello.txt", new string[] { "alpha/hello.txt", "beta/hello.txt", "gamma/hello.txt" })]
    [InlineData(@"././**/hello.txt", new string[] { "alpha/hello.txt", "beta/hello.txt", "gamma/hello.txt" })]
    [InlineData(@"././**/./hello.txt", new string[] { "alpha/hello.txt", "beta/hello.txt", "gamma/hello.txt" })]
    [InlineData(@"././**/./**/hello.txt", new string[] { "alpha/hello.txt", "beta/hello.txt", "gamma/hello.txt" })]
    [InlineData(@"./*mm*/hello.txt", new string[] { "gamma/hello.txt" })]
    [InlineData(@"./*mm*/*", new string[] { "gamma/hello.txt" })]
    public void PatternMatchingCurrent(string includePattern, string[] matchesExpected)
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns(includePattern);
        var globMatcher = matcher.Build();

        var matches = new List<string> { "alpha/hello.txt", "beta/hello.txt", "gamma/hello.txt" }
            .Where(file => globMatcher.Match(file).IsMatch)
            .ToArray();

        Assert.Equal(matchesExpected, matches);
    }

    [Fact]
    public void StarDotStarIsSameAsStar()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("*.*");
        var globMatcher = matcher.Build();

        var matches = new List<string> { "alpha.txt", "alpha.", ".txt", ".", "alpha", "txt" }
            .Where(file => globMatcher.Match(file).IsMatch)
            .ToArray();

        Assert.Equal(new[] { "alpha.txt", "alpha.", ".txt" }, matches);
    }

    [Fact]
    public void IncompletePatternsDoNotInclude()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("*/*.txt");
        var globMatcher = matcher.Build();

        var matches = new List<string> { "one/x.txt", "two/x.txt", "x.txt" }
            .Where(file => globMatcher.Match(file).IsMatch)
            .ToArray();

        Assert.Equal(new[] { "one/x.txt", "two/x.txt" }, matches);
    }

    [Fact]
    public void IncompletePatternsDoNotExclude()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("*/*.txt");
        matcher.AddExcludePatterns("one/hello.txt");
        var globMatcher = matcher.Build();

        var matches = new List<string> { "one/x.txt", "two/x.txt" }
            .Where(file => globMatcher.Match(file).IsMatch)
            .ToArray();

        Assert.Equal(new[] { "one/x.txt", "two/x.txt" }, matches);
    }

    [Fact]
    public void TrailingRecursiveWildcardMatchesAllFiles()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("one/**");
        var globMatcher = matcher.Build();

        var matches = new List<string> { "one/x.txt", "two/x.txt", "one/x/y.txt" }
            .Where(file => globMatcher.Match(file).IsMatch)
            .ToArray();

        Assert.Equal(new[] { "one/x.txt", "one/x/y.txt" }, matches);
    }

    [Fact]
    public void LeadingRecursiveWildcardMatchesAllLeadingPaths()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("**/*.cs");
        var globMatcher = matcher.Build();

        var matches = new List<string> { "one/x.cs", "two/x.cs", "one/two/x.cs", "x.cs", "one/x.txt", "two/x.txt", "one/two/x.txt", "x.txt" }
            .Where(file => globMatcher.Match(file).IsMatch)
            .ToArray();

        Assert.Equal(new[] { "one/x.cs", "two/x.cs", "one/two/x.cs", "x.cs" }, matches);
    }

    [Fact]
    public void InnerRecursiveWildcardMustStartWithAndEndWith()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("one/**/*.cs");
        var globMatcher = matcher.Build();

        var matches = new List<string> { "one/x.cs", "two/x.cs", "one/two/x.cs", "x.cs", "one/x.txt", "two/x.txt", "one/two/x.txt", "x.txt" }
            .Where(file => globMatcher.Match(file).IsMatch)
            .ToArray();

        Assert.Equal(new[] { "one/x.cs", "one/two/x.cs" }, matches);
    }

    [Fact]
    public void ExcludeMayEndInDirectoryName()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("*.cs", "*/*.cs", "*/*/*.cs");
        matcher.AddExcludePatterns("bin/", "one/two/");
        var globMatcher = matcher.Build();

        var matches = new List<string> { "one/x.cs", "two/x.cs", "one/two/x.cs", "x.cs", "bin/x.cs", "bin/two/x.cs" }
            .Where(file => globMatcher.Match(file).IsMatch)
            .ToArray();

        Assert.Equal(new[] { "one/x.cs", "two/x.cs", "x.cs" }, matches);
    }

    [Fact]
    public void RecursiveWildcardSurroundingContainsWith()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("**/x/**");
        var globMatcher = matcher.Build();

        var matches = new List<string> { "x/1", "1/x/2", "1/x", "x", "1", "1/2" }
            .Where(file => globMatcher.Match(file).IsMatch)
            .ToArray();

        Assert.Equal(new[] { "x/1", "1/x/2", "1/x", "x" }, matches);
    }

    [Fact]
    public void SequentialFoldersMayBeRequired()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("a/b/**/1/2/**/2/3/**");
        var globMatcher = matcher.Build();

        var matches = new List<string> { "1/2/2/3/x", "1/2/3/y", "a/1/2/4/2/3/b", "a/2/3/1/2/b", "a/b/1/2/2/3/x", "a/b/1/2/3/y", "a/b/a/1/2/4/2/3/b", "a/b/a/2/3/1/2/b" }
            .Where(file => globMatcher.Match(file).IsMatch)
            .ToArray();

        Assert.Equal(new[] { "a/b/1/2/2/3/x", "a/b/a/1/2/4/2/3/b" }, matches);
    }

    [Fact]
    public void RecursiveAloneIncludesEverything()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("**");
        var globMatcher = matcher.Build();

        var matches = new List<string> { "1/2/2/3/x", "1/2/3/y" }
            .Where(file => globMatcher.Match(file).IsMatch)
            .ToArray();

        Assert.Equal(new[] { "1/2/2/3/x", "1/2/3/y" }, matches);
    }

    [Fact]
    public void ExcludeCanHaveSurroundingRecursiveWildcards()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("**");
        matcher.AddExcludePatterns("**/x/**");
        var globMatcher = matcher.Build();

        var matches = new List<string> { "x/1", "1/x/2", "1/x", "x", "1", "1/2" }
            .Where(file => globMatcher.Match(file).IsMatch)
            .ToArray();

        Assert.Equal(new[] { "1", "1/2" }, matches);
    }
}
