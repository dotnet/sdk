// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
namespace Microsoft.NET.Sdk.StaticWebAssets.Tests;

[TestClass]
public class RewriteCssTest
{
    [TestMethod]
    public void HandlesEmptyFile()
    {
        // Arrange/act
        var result = RewriteCss.AddScopeToSelectors("file.css", string.Empty, "TestScope", out var errors);

        // Assert
        Assert.IsEmpty(errors);
        Assert.AreEqual(string.Empty, result);
    }

    [TestMethod]
    public void AddsScopeAfterSelector()
    {
        // Arrange/act
        var result = RewriteCss.AddScopeToSelectors("file.css", @"
    .myclass { color: red; }
", "TestScope", out var errors);

        // Assert
        Assert.IsEmpty(errors);
        Assert.AreEqual(@"
    .myclass[TestScope] { color: red; }
", result);
    }

    [TestMethod]
    public void HandlesMultipleSelectors()
    {
        // Arrange/act
        var result = RewriteCss.AddScopeToSelectors("file.css", @"
    .first, .second { color: red; }
    .third { color: blue; }
    :root { color: green; }
    * { color: white; }
    #some-id { color: yellow; }
", "TestScope", out var errors);

        // Assert
        Assert.IsEmpty(errors);
        Assert.AreEqual(@"
    .first[TestScope], .second[TestScope] { color: red; }
    .third[TestScope] { color: blue; }
    :root[TestScope] { color: green; }
    *[TestScope] { color: white; }
    #some-id[TestScope] { color: yellow; }
", result);
    }

    [TestMethod]
    public void HandlesComplexSelectors()
    {
        // Arrange/act
        var result = RewriteCss.AddScopeToSelectors("file.css", @"
    .first div > li, body .second:not(.fancy)[attr~=whatever] { color: red; }
", "TestScope", out var errors);

        // Assert
        Assert.IsEmpty(errors);
        Assert.AreEqual(@"
    .first div > li[TestScope], body .second:not(.fancy)[attr~=whatever][TestScope] { color: red; }
", result);
    }

    [TestMethod]
    public void HandlesSpacesAndCommentsWithinSelectors()
    {
        // Arrange/act
        var result = RewriteCss.AddScopeToSelectors("file.css", @"
    .first /* space at end {} */ div , .myclass /* comment at end */ { color: red; }
", "TestScope", out var errors);

        // Assert
        Assert.IsEmpty(errors);
        Assert.AreEqual(@"
    .first /* space at end {} */ div[TestScope] , .myclass[TestScope] /* comment at end */ { color: red; }
", result);
    }

    [TestMethod]
    public void HandlesPseudoClasses()
    {
        // Arrange/act
        var result = RewriteCss.AddScopeToSelectors("file.css", @"
    a:fake-pseudo-class { color: red; }
    a:focus b:hover { color: green; }
    tr:nth-child(4n + 1) { color: blue; }
    a:has(b > c) { color: yellow; }
    a:last-child > ::deep b { color: pink; }
    a:not(#something) { color: purple; }
", "TestScope", out var errors);

        // Assert
        Assert.IsEmpty(errors);
        Assert.AreEqual(@"
    a:fake-pseudo-class[TestScope] { color: red; }
    a:focus b:hover[TestScope] { color: green; }
    tr:nth-child(4n + 1)[TestScope] { color: blue; }
    a:has(b > c)[TestScope] { color: yellow; }
    a:last-child[TestScope] >  b { color: pink; }
    a:not(#something)[TestScope] { color: purple; }
", result);
    }

    [TestMethod]
    public void HandlesPseudoElements()
    {
        // Arrange/act
        var result = RewriteCss.AddScopeToSelectors("file.css", @"
    a::before { content: ""✋""; }
    a::after::placeholder { content: ""🐯""; }
    custom-element::part(foo) { content: ""🤷‍""; }
    a::before > ::deep another { content: ""👞""; }
    a::fake-PsEuDo-element { content: ""🐔""; }
    ::selection { content: ""😾""; }
    other, ::selection { content: ""👂""; }
", "TestScope", out var errors);

        // Assert
        Assert.IsEmpty(errors);
        Assert.AreEqual(@"
    a[TestScope]::before { content: ""✋""; }
    a[TestScope]::after::placeholder { content: ""🐯""; }
    custom-element[TestScope]::part(foo) { content: ""🤷‍""; }
    a[TestScope]::before >  another { content: ""👞""; }
    a[TestScope]::fake-PsEuDo-element { content: ""🐔""; }
    [TestScope]::selection { content: ""😾""; }
    other[TestScope], [TestScope]::selection { content: ""👂""; }
", result);
    }

    [TestMethod]
    public void HandlesSingleColonPseudoElements()
    {
        // Arrange/act
        var result = RewriteCss.AddScopeToSelectors("file.css", @"
    a:after { content: ""x""; }
    a:before { content: ""x""; }
    a:first-letter { content: ""x""; }
    a:first-line { content: ""x""; }
    a:AFTER { content: ""x""; }
    a:not(something):before { content: ""x""; }
", "TestScope", out var errors);

        // Assert
        Assert.IsEmpty(errors);
        Assert.AreEqual(@"
    a[TestScope]:after { content: ""x""; }
    a[TestScope]:before { content: ""x""; }
    a[TestScope]:first-letter { content: ""x""; }
    a[TestScope]:first-line { content: ""x""; }
    a[TestScope]:AFTER { content: ""x""; }
    a:not(something)[TestScope]:before { content: ""x""; }
", result);
    }

    [TestMethod]
    public void RespectsDeepCombinator()
    {
        // Arrange/act
        var result = RewriteCss.AddScopeToSelectors("file.css", @"
    .first ::deep .second { color: red; }
    a ::deep b, c ::deep d { color: blue; }
", "TestScope", out var errors);

        // Assert
        Assert.IsEmpty(errors);
        Assert.AreEqual(@"
    .first[TestScope]  .second { color: red; }
    a[TestScope]  b, c[TestScope]  d { color: blue; }
", result);
    }

    [TestMethod]
    public void RespectsDeepCombinatorWithDirectDescendant()
    {
        // Arrange/act
        var result = RewriteCss.AddScopeToSelectors("file.css", @"
    a  >  ::deep b { color: red; }
    c ::deep  >  d { color: blue; }
", "TestScope", out var errors);

        // Assert
        Assert.IsEmpty(errors);
        Assert.AreEqual(@"
    a[TestScope]  >   b { color: red; }
    c[TestScope]   >  d { color: blue; }
", result);
    }

    [TestMethod]
    public void RespectsDeepCombinatorWithAdjacentSibling()
    {
        // Arrange/act
        var result = RewriteCss.AddScopeToSelectors("file.css", @"
    a + ::deep b { color: red; }
    c ::deep + d { color: blue; }
", "TestScope", out var errors);

        // Assert
        Assert.IsEmpty(errors);
        Assert.AreEqual(@"
    a[TestScope] +  b { color: red; }
    c[TestScope]  + d { color: blue; }
", result);
    }

    [TestMethod]
    public void RespectsDeepCombinatorWithGeneralSibling()
    {
        // Arrange/act
        var result = RewriteCss.AddScopeToSelectors("file.css", @"
    a ~ ::deep b { color: red; }
    c ::deep ~ d { color: blue; }
", "TestScope", out var errors);

        // Assert
        Assert.IsEmpty(errors);
        Assert.AreEqual(@"
    a[TestScope] ~  b { color: red; }
    c[TestScope]  ~ d { color: blue; }
", result);
    }

    [TestMethod]
    public void IgnoresMultipleDeepCombinators()
    {
        // Arrange/act
        var result = RewriteCss.AddScopeToSelectors("file.css", @"
    .first ::deep .second ::deep .third { color:red; }
", "TestScope", out var errors);

        // Assert
        Assert.IsEmpty(errors);
        Assert.AreEqual(@"
    .first[TestScope]  .second ::deep .third { color:red; }
", result);
    }

    [TestMethod]
    public void RespectsDeepCombinatorWithSpacesAndComments()
    {
        // Arrange/act
        var result = RewriteCss.AddScopeToSelectors("file.css", @"
    .a .b /* comment ::deep 1 */  ::deep  /* comment ::deep 2 */  .c /* ::deep */ .d { color: red; }
    ::deep * { color: blue; } /* Leading deep combinator */
    another ::deep { color: green }  /* Trailing deep combinator */
", "TestScope", out var errors);

        // Assert
        Assert.IsEmpty(errors);
        Assert.AreEqual(@"
    .a .b[TestScope] /* comment ::deep 1 */    /* comment ::deep 2 */  .c /* ::deep */ .d { color: red; }
    [TestScope] * { color: blue; } /* Leading deep combinator */
    another[TestScope]  { color: green }  /* Trailing deep combinator */
", result);
    }

    [TestMethod]
    public void HandlesAtBlocks()
    {
        // Arrange/act
        var result = RewriteCss.AddScopeToSelectors("file.css", @"
    .myclass { color: red; }

    @media only screen and (max-width: 600px) {
        .another .thing {
            content: 'This should not be a selector: .fake-selector { color: red }'
        }
    }
", "TestScope", out var errors);

        // Assert
        Assert.IsEmpty(errors);
        Assert.AreEqual(@"
    .myclass[TestScope] { color: red; }

    @media only screen and (max-width: 600px) {
        .another .thing[TestScope] {
            content: 'This should not be a selector: .fake-selector { color: red }'
        }
    }
", result);
    }

    [TestMethod]
    public void AddsScopeToKeyframeNames()
    {
        // Arrange/act
        var result = RewriteCss.AddScopeToSelectors("file.css", @"
    @keyframes my-animation { /* whatever */ }
", "TestScope", out var errors);

        // Assert
        Assert.IsEmpty(errors);
        Assert.AreEqual(@"
    @keyframes my-animation-TestScope { /* whatever */ }
", result);
    }

    [TestMethod]
    public void RewritesAnimationNamesWhenMatchingKnownKeyframes()
    {
        // Arrange/act
        var result = RewriteCss.AddScopeToSelectors("file.css", @"
    .myclass {
        color: red;
        animation: /* ignore comment */ my-animation 1s infinite;
    }

    .another-thing { animation-name: different-animation; }

    h1 { animation: unknown-animation; } /* Should not be scoped */

    @keyframes my-animation { /* whatever */ }
    @keyframes different-animation { /* whatever */ }
    @keyframes unused-animation { /* whatever */ }
", "TestScope", out var errors);

        // Assert
        Assert.IsEmpty(errors);
        Assert.AreEqual(@"
    .myclass[TestScope] {
        color: red;
        animation: /* ignore comment */ my-animation-TestScope 1s infinite;
    }

    .another-thing[TestScope] { animation-name: different-animation-TestScope; }

    h1[TestScope] { animation: unknown-animation; } /* Should not be scoped */

    @keyframes my-animation-TestScope { /* whatever */ }
    @keyframes different-animation-TestScope { /* whatever */ }
    @keyframes unused-animation-TestScope { /* whatever */ }
", result);
    }

    [TestMethod]
    public void RewritesMultipleAnimationNames()
    {
        // Arrange/act
        var result = RewriteCss.AddScopeToSelectors("file.css", @"
    .myclass1 { animation-name: my-animation , different-animation }
    .myclass2 { animation: 4s linear 0s alternate my-animation infinite, different-animation 0s }
    @keyframes my-animation { }
    @keyframes different-animation { }
", "TestScope", out var errors);

        // Assert
        Assert.IsEmpty(errors);
        Assert.AreEqual(@"
    .myclass1[TestScope] { animation-name: my-animation-TestScope , different-animation-TestScope }
    .myclass2[TestScope] { animation: 4s linear 0s alternate my-animation-TestScope infinite, different-animation-TestScope 0s }
    @keyframes my-animation-TestScope { }
    @keyframes different-animation-TestScope { }
", result);
    }

    [TestMethod]
    public void RejectsImportStatements()
    {
        // Arrange/act
        RewriteCss.AddScopeToSelectors("file.css", @"
    @import ""basic-import.css"";
    @import ""import-with-media-type.css"" print;
    @import ""import-with-media-query.css"" screen and (orientation:landscape);
    @ImPoRt /* comment */ ""scheme://path/to/complex-import"" /* another-comment */ screen;
    @otheratrule ""should-not-cause-error.css"";
    /* @import ""should-be-ignored-because-it-is-in-a-comment.css""; */
    .myclass { color: red; }
", "TestScope", out var errors);

        // Assert
        var errorList = errors.ToList();
        Assert.HasCount(4, errorList);
        Assert.AreEqual("file.css(2,5): @import rules are not supported within scoped CSS files because the loading order would be undefined. @import may only be placed in non-scoped CSS files.", errorList[0].ToString());
        Assert.AreEqual("file.css(3,5): @import rules are not supported within scoped CSS files because the loading order would be undefined. @import may only be placed in non-scoped CSS files.", errorList[1].ToString());
        Assert.AreEqual("file.css(4,5): @import rules are not supported within scoped CSS files because the loading order would be undefined. @import may only be placed in non-scoped CSS files.", errorList[2].ToString());
        Assert.AreEqual("file.css(5,5): @import rules are not supported within scoped CSS files because the loading order would be undefined. @import may only be placed in non-scoped CSS files.", errorList[3].ToString());
    }
}
