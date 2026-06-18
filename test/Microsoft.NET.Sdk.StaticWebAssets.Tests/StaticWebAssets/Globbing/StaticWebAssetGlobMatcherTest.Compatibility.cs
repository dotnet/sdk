// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;
// Licensed to the .NET Foundation under one or more agreements.


// The .NET Foundation licenses this file to you under the MIT license.





namespace Microsoft.AspNetCore.StaticWebAssets.Tasks.Test;





public partial class StaticWebAssetGlobMatcherTest


{


    [TestMethod]


    public void MatchingFileIsFound()


    {


        var matcher = new StaticWebAssetGlobMatcherBuilder();


        matcher.AddIncludePatterns("alpha.txt");


        var globMatcher = matcher.Build();





        var match = globMatcher.Match("alpha.txt");


        Assert.IsTrue(match.IsMatch);


        Assert.AreEqual("alpha.txt", match.Pattern);


    }





    [TestMethod]


    public void MismatchedFileIsIgnored()


    {


        var matcher = new StaticWebAssetGlobMatcherBuilder();


        matcher.AddIncludePatterns("alpha.txt");


        var globMatcher = matcher.Build();





        var match = globMatcher.Match("omega.txt");


        Assert.IsFalse(match.IsMatch);


    }





    [TestMethod]


    public void FolderNamesAreTraversed()


    {


        var matcher = new StaticWebAssetGlobMatcherBuilder();


        matcher.AddIncludePatterns("beta/alpha.txt");


        var globMatcher = matcher.Build();





        var match = globMatcher.Match("beta/alpha.txt");


        Assert.IsTrue(match.IsMatch);


        Assert.AreEqual("beta/alpha.txt", match.Pattern);


    }





    [TestMethod]


    [DataRow(@"beta/alpha.txt", @"beta/alpha.txt")]


    [DataRow(@"beta\alpha.txt", @"beta/alpha.txt")]


    [DataRow(@"beta/alpha.txt", @"beta\alpha.txt")]


    [DataRow(@"beta\alpha.txt", @"beta\alpha.txt")]


    [DataRow(@"\beta\alpha.txt", @"beta/alpha.txt")]


    public void SlashPolarityIsIgnored(string includePattern, string filePath)


    {


        var matcher = new StaticWebAssetGlobMatcherBuilder();


        matcher.AddIncludePatterns(includePattern);


        var globMatcher = matcher.Build();





        var match = globMatcher.Match(filePath);


        Assert.IsTrue(match.IsMatch);


        //Assert.AreEqual("beta/alpha.txt", match.Pattern);


    }





    [TestMethod]


    [DataRow(@"alpha.*", new[] { "alpha.txt" })]


    [DataRow(@"*", new[] { "alpha.txt", "beta.txt", "gamma.dat" })]


    [DataRow(@"*et*", new[] { "beta.txt" })]


    [DataRow(@"*.*", new[] { "alpha.txt", "beta.txt", "gamma.dat" })]


    [DataRow(@"b*et*x", new string[0])]


    [DataRow(@"*.txt", new[] { "alpha.txt", "beta.txt" })]


    [DataRow(@"b*et*t", new[] { "beta.txt" })]


    public void CanPatternMatch(string includes, string[] expected)


    {


        var matcher = new StaticWebAssetGlobMatcherBuilder();


        matcher.AddIncludePatterns(includes);


        var globMatcher = matcher.Build();





        var matches = new List<string> { "alpha.txt", "beta.txt", "gamma.dat" }


            .Where(file => globMatcher.Match(file).IsMatch)


            .ToArray();





        Assert.AreSequenceEqual(expected, matches);


    }





    [TestMethod]


    [DataRow(@"12345*5678", new string[0])]


    [DataRow(@"1234*5678", new[] { "12345678" })]


    [DataRow(@"12*23*", new string[0])]


    [DataRow(@"12*3456*78", new[] { "12345678" })]


    [DataRow(@"*45*56", new string[0])]


    [DataRow(@"*67*78", new string[0])]


    public void PatternBeginAndEndCantOverlap(string includes, string[] expected)


    {


        var matcher = new StaticWebAssetGlobMatcherBuilder();


        matcher.AddIncludePatterns(includes);


        var globMatcher = matcher.Build();





        var matches = new List<string> { "12345678" }


            .Where(file => globMatcher.Match(file).IsMatch)


            .ToArray();





        Assert.AreSequenceEqual(expected, matches);


    }





    [TestMethod]


    [DataRow(@"*alpha*/*", new[] { "alpha/hello.txt" })]


    [DataRow(@"/*/*", new[] { "alpha/hello.txt", "beta/hello.txt", "gamma/hello.txt" })]


    [DataRow(@"*/*", new[] { "alpha/hello.txt", "beta/hello.txt", "gamma/hello.txt" })]


    [DataRow(@"/*.*/*", new string[] { })]


    [DataRow(@"*.*/*", new string[] { })]


    [DataRow(@"/*mm*/*", new[] { "gamma/hello.txt" })]


    [DataRow(@"*mm*/*", new[] { "gamma/hello.txt" })]


    [DataRow(@"/*alpha*/*", new[] { "alpha/hello.txt" })]


    public void PatternMatchingWorksInFolders(string includes, string[] expected)


    {


        var matcher = new StaticWebAssetGlobMatcherBuilder();


        matcher.AddIncludePatterns(includes);


        var globMatcher = matcher.Build();





        var matches = new List<string> { "alpha/hello.txt", "beta/hello.txt", "gamma/hello.txt" }


            .Where(file => globMatcher.Match(file).IsMatch)


            .ToArray();





        Assert.AreSequenceEqual(expected, matches);


    }





    [TestMethod]


    [DataRow(@"", new string[] { })]


    [DataRow(@"./", new string[] { "alpha/hello.txt", "beta/hello.txt", "gamma/hello.txt" })]


    [DataRow(@"./alpha/hello.txt", new string[] { "alpha/hello.txt" })]


    [DataRow(@"./**/hello.txt", new string[] { "alpha/hello.txt", "beta/hello.txt", "gamma/hello.txt" })]


    [DataRow(@"././**/hello.txt", new string[] { "alpha/hello.txt", "beta/hello.txt", "gamma/hello.txt" })]


    [DataRow(@"././**/./hello.txt", new string[] { "alpha/hello.txt", "beta/hello.txt", "gamma/hello.txt" })]


    [DataRow(@"././**/./**/hello.txt", new string[] { "alpha/hello.txt", "beta/hello.txt", "gamma/hello.txt" })]


    [DataRow(@"./*mm*/hello.txt", new string[] { "gamma/hello.txt" })]


    [DataRow(@"./*mm*/*", new string[] { "gamma/hello.txt" })]


    public void PatternMatchingCurrent(string includePattern, string[] matchesExpected)


    {


        var matcher = new StaticWebAssetGlobMatcherBuilder();


        matcher.AddIncludePatterns(includePattern);


        var globMatcher = matcher.Build();





        var matches = new List<string> { "alpha/hello.txt", "beta/hello.txt", "gamma/hello.txt" }


            .Where(file => globMatcher.Match(file).IsMatch)


            .ToArray();





        Assert.AreSequenceEqual(matchesExpected, matches);


    }





    [TestMethod]


    public void StarDotStarIsSameAsStar()


    {


        var matcher = new StaticWebAssetGlobMatcherBuilder();


        matcher.AddIncludePatterns("*.*");


        var globMatcher = matcher.Build();





        var matches = new List<string> { "alpha.txt", "alpha.", ".txt", ".", "alpha", "txt" }


            .Where(file => globMatcher.Match(file).IsMatch)


            .ToArray();





        Assert.AreSequenceEqual(new[] { "alpha.txt", "alpha.", ".txt" }, matches);


    }





    [TestMethod]


    public void IncompletePatternsDoNotInclude()


    {


        var matcher = new StaticWebAssetGlobMatcherBuilder();


        matcher.AddIncludePatterns("*/*.txt");


        var globMatcher = matcher.Build();





        var matches = new List<string> { "one/x.txt", "two/x.txt", "x.txt" }


            .Where(file => globMatcher.Match(file).IsMatch)


            .ToArray();





        Assert.AreSequenceEqual(new[] { "one/x.txt", "two/x.txt" }, matches);


    }





    [TestMethod]


    public void IncompletePatternsDoNotExclude()


    {


        var matcher = new StaticWebAssetGlobMatcherBuilder();


        matcher.AddIncludePatterns("*/*.txt");


        matcher.AddExcludePatterns("one/hello.txt");


        var globMatcher = matcher.Build();





        var matches = new List<string> { "one/x.txt", "two/x.txt" }


            .Where(file => globMatcher.Match(file).IsMatch)


            .ToArray();





        Assert.AreSequenceEqual(new[] { "one/x.txt", "two/x.txt" }, matches);


    }





    [TestMethod]


    public void TrailingRecursiveWildcardMatchesAllFiles()


    {


        var matcher = new StaticWebAssetGlobMatcherBuilder();


        matcher.AddIncludePatterns("one/**");


        var globMatcher = matcher.Build();





        var matches = new List<string> { "one/x.txt", "two/x.txt", "one/x/y.txt" }


            .Where(file => globMatcher.Match(file).IsMatch)


            .ToArray();





        Assert.AreSequenceEqual(new[] { "one/x.txt", "one/x/y.txt" }, matches);


    }





    [TestMethod]


    public void LeadingRecursiveWildcardMatchesAllLeadingPaths()


    {


        var matcher = new StaticWebAssetGlobMatcherBuilder();


        matcher.AddIncludePatterns("**/*.cs");


        var globMatcher = matcher.Build();





        var matches = new List<string> { "one/x.cs", "two/x.cs", "one/two/x.cs", "x.cs", "one/x.txt", "two/x.txt", "one/two/x.txt", "x.txt" }


            .Where(file => globMatcher.Match(file).IsMatch)


            .ToArray();





        Assert.AreSequenceEqual(new[] { "one/x.cs", "two/x.cs", "one/two/x.cs", "x.cs" }, matches);


    }





    [TestMethod]


    public void InnerRecursiveWildcardMustStartWithAndEndWith()


    {


        var matcher = new StaticWebAssetGlobMatcherBuilder();


        matcher.AddIncludePatterns("one/**/*.cs");


        var globMatcher = matcher.Build();





        var matches = new List<string> { "one/x.cs", "two/x.cs", "one/two/x.cs", "x.cs", "one/x.txt", "two/x.txt", "one/two/x.txt", "x.txt" }


            .Where(file => globMatcher.Match(file).IsMatch)


            .ToArray();





        Assert.AreSequenceEqual(new[] { "one/x.cs", "one/two/x.cs" }, matches);


    }





    [TestMethod]


    public void ExcludeMayEndInDirectoryName()


    {


        var matcher = new StaticWebAssetGlobMatcherBuilder();


        matcher.AddIncludePatterns("*.cs", "*/*.cs", "*/*/*.cs");


        matcher.AddExcludePatterns("bin/", "one/two/");


        var globMatcher = matcher.Build();





        var matches = new List<string> { "one/x.cs", "two/x.cs", "one/two/x.cs", "x.cs", "bin/x.cs", "bin/two/x.cs" }


            .Where(file => globMatcher.Match(file).IsMatch)


            .ToArray();





        Assert.AreSequenceEqual(new[] { "one/x.cs", "two/x.cs", "x.cs" }, matches);


    }





    [TestMethod]


    public void RecursiveWildcardSurroundingContainsWith()


    {


        var matcher = new StaticWebAssetGlobMatcherBuilder();


        matcher.AddIncludePatterns("**/x/**");


        var globMatcher = matcher.Build();





        var matches = new List<string> { "x/1", "1/x/2", "1/x", "x", "1", "1/2" }


            .Where(file => globMatcher.Match(file).IsMatch)


            .ToArray();





        Assert.AreSequenceEqual(new[] { "x/1", "1/x/2", "1/x", "x" }, matches);


    }





    [TestMethod]


    public void SequentialFoldersMayBeRequired()


    {


        var matcher = new StaticWebAssetGlobMatcherBuilder();


        matcher.AddIncludePatterns("a/b/**/1/2/**/2/3/**");


        var globMatcher = matcher.Build();





        var matches = new List<string> { "1/2/2/3/x", "1/2/3/y", "a/1/2/4/2/3/b", "a/2/3/1/2/b", "a/b/1/2/2/3/x", "a/b/1/2/3/y", "a/b/a/1/2/4/2/3/b", "a/b/a/2/3/1/2/b" }


            .Where(file => globMatcher.Match(file).IsMatch)


            .ToArray();





        Assert.AreSequenceEqual(new[] { "a/b/1/2/2/3/x", "a/b/a/1/2/4/2/3/b" }, matches);


    }





    [TestMethod]


    public void RecursiveAloneIncludesEverything()


    {


        var matcher = new StaticWebAssetGlobMatcherBuilder();


        matcher.AddIncludePatterns("**");


        var globMatcher = matcher.Build();





        var matches = new List<string> { "1/2/2/3/x", "1/2/3/y" }


            .Where(file => globMatcher.Match(file).IsMatch)


            .ToArray();





        Assert.AreSequenceEqual(new[] { "1/2/2/3/x", "1/2/3/y" }, matches);


    }





    [TestMethod]


    public void ExcludeCanHaveSurroundingRecursiveWildcards()


    {


        var matcher = new StaticWebAssetGlobMatcherBuilder();


        matcher.AddIncludePatterns("**");


        matcher.AddExcludePatterns("**/x/**");


        var globMatcher = matcher.Build();





        var matches = new List<string> { "x/1", "1/x/2", "1/x", "x", "1", "1/2" }


            .Where(file => globMatcher.Match(file).IsMatch)


            .ToArray();





        Assert.AreSequenceEqual(new[] { "1", "1/2" }, matches);


    }


}


