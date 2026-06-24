// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using Microsoft.VisualStudio.TestTools.UnitTesting;

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
[TestClass]
public partial class StaticWebAssetGlobMatcherTest
{
        [TestMethod]
        [DataRow("**/*.razor.js", "Components/Pages/RegularComponent.razor.js", "Components/Pages/RegularComponent.razor.js")]
        [DataRow("**/*.razor.js", "Components/User.Profile.Details.razor.js", "Components/User.Profile.Details.razor.js")]
        [DataRow("**/*.razor.js", "Components/Area/Sub/Feature/User.Profile.Details.razor.js", "Components/Area/Sub/Feature/User.Profile.Details.razor.js")]
        [DataRow("**/*.razor.js", "Components/Area/Sub/Feature/Deep.Component.Name.With.Many.Parts.razor.js", "Components/Area/Sub/Feature/Deep.Component.Name.With.Many.Parts.razor.js")]
        [DataRow("**/*.cshtml.js", "Pages/Shared/_Host.cshtml.js", "Pages/Shared/_Host.cshtml.js")]
        [DataRow("**/*.cshtml.js", "Areas/Admin/Pages/Dashboard.cshtml.js", "Areas/Admin/Pages/Dashboard.cshtml.js")]
        [DataRow("*.lib.module.js", "Widget.lib.module.js", "Widget.lib.module.js")]
        [DataRow("*.razor.css", "Component.razor.css", "Component.razor.css")]
        [DataRow("*.cshtml.css", "View.cshtml.css", "View.cshtml.css")]
        [DataRow("*.modules.json", "app.modules.json", "app.modules.json")]
        [DataRow("*.lib.module.js", "Rcl.Client.Feature.lib.module.js", "Rcl.Client.Feature.lib.module.js")]
        public void Can_Match_WellKnownExistingPatterns(string pattern, string path, string expectedStem)
        {
            var matcher = new StaticWebAssetGlobMatcherBuilder();
            matcher.AddIncludePatterns(pattern);
            var globMatcher = matcher.Build();

            var match = globMatcher.Match(path);
            Assert.IsTrue(match.IsMatch);
            Assert.AreEqual(pattern, match.Pattern);
            Assert.AreEqual(expectedStem, match.Stem);
        }
    [TestMethod]
    public void CanMatchLiterals()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("a");
        var globMatcher = matcher.Build();

        var match = globMatcher.Match("a");
        Assert.IsTrue(match.IsMatch);
        Assert.AreEqual("a", match.Pattern);
        Assert.AreEqual("a", match.Stem);
    }

    [TestMethod]
    public void CanMatchMultipleLiterals()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("a/b");
        var globMatcher = matcher.Build();

        var match = globMatcher.Match("a/b");
        Assert.IsTrue(match.IsMatch);
        Assert.AreEqual("a/b", match.Pattern);
        Assert.AreEqual("b", match.Stem);
    }

    [TestMethod]
    public void CanMatchExtensions()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("*.a");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a.a");
        Assert.IsTrue(match.IsMatch);
        Assert.AreEqual("*.a", match.Pattern);
        Assert.AreEqual("a.a", match.Stem);
    }

    [TestMethod]
    public void MatchesLongerExtensionsFirst()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("*.a", "*.b.a");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("c.b.a");
        Assert.IsTrue(match.IsMatch);
        Assert.AreEqual("*.b.a", match.Pattern);
        Assert.AreEqual("c.b.a", match.Stem);
    }

    [TestMethod]
    public void CanMatchExtensionsAtTheBeginning()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("*.a/b");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("c.a/b");
        Assert.IsTrue(match.IsMatch);
        Assert.AreEqual("*.a/b", match.Pattern);
        Assert.AreEqual("b", match.Stem);
    }

    [TestMethod]
    public void CanMatchExtensionsAtTheEnd()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("a/*.b");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a/c.b");
        Assert.IsTrue(match.IsMatch);
        Assert.AreEqual("a/*.b", match.Pattern);
        Assert.AreEqual("c.b", match.Stem);
    }

    [TestMethod]
    public void CanMatchExtensionsInMiddle()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("a/*.b/c");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a/d.b/c");
        Assert.IsTrue(match.IsMatch);
        Assert.AreEqual("a/*.b/c", match.Pattern);
        Assert.AreEqual("c", match.Stem);
    }

    [TestMethod]
    [DataRow("a*")]
    [DataRow("*a")]
    [DataRow("?")]
    [DataRow("*?")]
    [DataRow("?*")]
    [DataRow("**a")]
    [DataRow("a**")]
    [DataRow("**?")]
    [DataRow("?**")]
    public void CanMatchComplexSegments(string pattern)
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns(pattern);
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a");
        Assert.IsTrue(match.IsMatch);
        Assert.AreEqual(pattern, match.Pattern);
        Assert.AreEqual("a", match.Stem);
    }

    [TestMethod]
    [DataRow("a?", "aa", true)]
    [DataRow("a?", "a", false)]
    [DataRow("a?", "aaa", false)]
    [DataRow("?a", "aa", true)]
    [DataRow("?a", "a", false)]
    [DataRow("?a", "aaa", false)]
    [DataRow("a?a", "aaa", true)]
    [DataRow("a?a", "aba", true)]
    [DataRow("a?a", "abaa", false)]
    [DataRow("a?a", "ab", false)]
    public void QuestionMarksMatchSingleCharacter(string pattern, string input, bool expectedMatchResult)
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns(pattern);
        var globMatcher = matcher.Build();
        var match = globMatcher.Match(input);
        Assert.AreEqual(expectedMatchResult, match.IsMatch);
        if(expectedMatchResult)
        {
            Assert.AreEqual(pattern, match.Pattern);
            Assert.AreEqual(input, match.Stem);
        }
        else
        {
            Assert.IsNull(match.Pattern);
            Assert.IsNull(match.Stem);
        }
    }

    [TestMethod]
    [DataRow("a??", "aaa", true)]
    [DataRow("a??", "aa", false)]
    [DataRow("a??", "aaaa", false)]
    [DataRow("?a?", "aaa", true)]
    [DataRow("?a?", "aa", false)]
    [DataRow("?a?", "aaaa", false)]
    [DataRow("??a", "aaa", true)]
    [DataRow("??a", "aa", false)]
    [DataRow("??a", "aaaa", false)]
    [DataRow("a??a", "aaaa", true)]
    [DataRow("a??a", "aaba", true)]
    [DataRow("a??a", "aabaa", false)]
    [DataRow("a??a", "aba", false)]
    public void MultipleQuestionMarksMatchExactlyTheNumberOfCharacters(string pattern, string input, bool expectedMatchResult)
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns(pattern);
        var globMatcher = matcher.Build();
        var match = globMatcher.Match(input);
        Assert.AreEqual(expectedMatchResult, match.IsMatch);
        if (expectedMatchResult)
        {
            Assert.AreEqual(pattern, match.Pattern);
            Assert.AreEqual(input, match.Stem);
        }
        else
        {
            Assert.IsNull(match.Pattern);
            Assert.IsNull(match.Stem);
        }
    }

    [TestMethod]
    [DataRow("a*", "a", true)]
    [DataRow("a*", "aa", true)]
    [DataRow("a*", "aaa", true)]
    [DataRow("a*", "aaaa", true)]
    [DataRow("a*", "aaaaa", true)]
    [DataRow("a*", "aaaaaa", true)]
    [DataRow("*a", "a", true)]
    [DataRow("*a", "aa", true)]
    [DataRow("*a", "aaa", true)]
    [DataRow("*a", "aaaa", true)]
    [DataRow("*a", "aaaaa", true)]
    [DataRow("a*a", "a", false)]
    [DataRow("a*a", "aa", true)]
    [DataRow("a*a", "aaa", true)]
    [DataRow("a*a", "aaaaa", true)]
    [DataRow("a*a", "aaaaaa", true)]
    [DataRow("a*a", "aba", true)]
    [DataRow("a*a", "abaa", true)]
    [DataRow("a*a", "abba", true)]
    [DataRow("a*b", "ab", true)]
    public void WildCardsMatchZeroOrMoreCharacters(string pattern, string input, bool expectedMatchResult)
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns(pattern);
        var globMatcher = matcher.Build();
        var match = globMatcher.Match(input);
        Assert.AreEqual(expectedMatchResult, match.IsMatch);
        if (expectedMatchResult)
        {
            Assert.AreEqual(pattern, match.Pattern);
            Assert.AreEqual(input, match.Stem);
        }
        else
        {
            Assert.IsNull(match.Pattern);
            Assert.IsNull(match.Stem);
        }
    }

    [TestMethod]
    [DataRow("*?a", "a", false)]
    [DataRow("*?a", "aa", true)]
    [DataRow("*?a", "aaa", true)]
    [DataRow("*?a", "aaaa", true)]
    [DataRow("*?a", "aaaaa", true)]
    [DataRow("*?a", "aaaaaa", true)]
    [DataRow("*??a", "aa", false)]
    [DataRow("*??a", "aaa", true)]
    [DataRow("*???a", "aaa", false)]
    [DataRow("*???a", "aaaa", true)]
    [DataRow("*????a", "aaaa", false)]
    [DataRow("*????a", "aaaaa", true)]
    [DataRow("*?????a", "aaaaa", false)]
    [DataRow("*?????a", "aaaaaa", true)]
    [DataRow("*??????a", "aaaaaa", false)]
    [DataRow("*??????a", "aaaaaaa", true)]
    [DataRow("a*?", "a", false)]
    [DataRow("a*?", "aa", true)]
    [DataRow("a*?", "aaa", true)]
    [DataRow("a*?", "aaaa", true)]
    [DataRow("a*?", "aaaaa", true)]
    [DataRow("a*?", "aaaaaa", true)]
    [DataRow("a*??", "aa", false)]
    [DataRow("a*??", "aaa", true)]
    [DataRow("a*???", "aaa", false)]
    [DataRow("a*???", "aaaa", true)]
    [DataRow("a*????", "aaaa", false)]
    [DataRow("a*????", "aaaaa", true)]
    [DataRow("a*?????", "aaaaa", false)]
    [DataRow("a*?????", "aaaaaa", true)]
    [DataRow("a*??????", "aaaaaa", false)]
    [DataRow("a*??????", "aaaaaaa", true)]

    public void SingleWildcardPrecededOrSucceededByQuestionMarkRequireMinimumNumberOfCharacters(string pattern, string input, bool expectedMatchResult)
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns(pattern);
        var globMatcher = matcher.Build();
        var match = globMatcher.Match(input);
        Assert.AreEqual(expectedMatchResult, match.IsMatch);
        if (expectedMatchResult)
        {
            Assert.AreEqual(pattern, match.Pattern);
            Assert.AreEqual(input, match.Stem);
        }
        else
        {
            Assert.IsNull(match.Pattern);
            Assert.IsNull(match.Stem);
        }
    }

    [TestMethod]
    public void CanMatchWildCard()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("*");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a");
        Assert.IsTrue(match.IsMatch);
        Assert.AreEqual("*", match.Pattern);
        Assert.AreEqual("a", match.Stem);
    }

    [TestMethod]
    public void CanMatchWildCardAtTheBeginning()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("*/a");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("c/a");
        Assert.IsTrue(match.IsMatch);
        Assert.AreEqual("*/a", match.Pattern);
        Assert.AreEqual("a", match.Stem);
    }

    [TestMethod]
    public void CanMatchWildCardAtTheEnd()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("a/*");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a/c");
        Assert.IsTrue(match.IsMatch);
        Assert.AreEqual("a/*", match.Pattern);
        Assert.AreEqual("c", match.Stem);
    }

    [TestMethod]
    public void CanMatchWildCardInMiddle()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("a/*/c");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a/b/c");
        Assert.IsTrue(match.IsMatch);
        Assert.AreEqual("a/*/c", match.Pattern);
        Assert.AreEqual("c", match.Stem);
    }

    [TestMethod]
    public void CanMatchRecursiveWildCard()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("**");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a/b/c");
        Assert.IsTrue(match.IsMatch);
        Assert.AreEqual("**", match.Pattern);
        Assert.AreEqual("a/b/c", match.Stem);
    }

    [TestMethod]
    public void CanMatchRecursiveWildCardAtTheBeginning()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("**/a");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("c/b/a");
        Assert.IsTrue(match.IsMatch);
        Assert.AreEqual("**/a", match.Pattern);
        Assert.AreEqual("c/b/a", match.Stem);
    }

    [TestMethod]
    public void CanMatchRecursiveWildCardAtTheEnd()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("a/**");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a/b/c");
        Assert.IsTrue(match.IsMatch);
        Assert.AreEqual("a/**", match.Pattern);
        Assert.AreEqual("b/c", match.Stem);
    }

    [TestMethod]
    public void CanMatchRecursiveWildCardInMiddle()
    {
        var matcher = new StaticWebAssetGlobMatcherBuilder();
        matcher.AddIncludePatterns("a/**/c");
        var globMatcher = matcher.Build();
        var match = globMatcher.Match("a/b/c");
        Assert.IsTrue(match.IsMatch);
        Assert.AreEqual("a/**/c", match.Pattern);
        Assert.AreEqual("b/c", match.Stem);
    }
}
