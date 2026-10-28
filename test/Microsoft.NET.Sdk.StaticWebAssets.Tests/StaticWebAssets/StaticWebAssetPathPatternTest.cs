// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Globalization;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.NET.Sdk.StaticWebAssets.Tests.StaticWebAssets;

[TestClass]
public class StaticWebAssetPathPatternTest
{
    [TestMethod]
    public void CanParse_PathWithNoExpressions()
    {
        var pattern = StaticWebAssetPathPattern.Parse("css/site.css", "MyApp");
        var expected = new StaticWebAssetPathPattern("css/site.css")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = "css/site.css".AsMemory(), IsLiteral = true }] }
            ]
        };
        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_ComplexFingerprintExpression_Middle()
    {
        var pattern = StaticWebAssetPathPattern.Parse("css/site#[.{fingerprint}].css", "MyApp");
        var expected = new StaticWebAssetPathPattern("css/site#[.{fingerprint}].css")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = "css/site".AsMemory(), IsLiteral = true }] },
                new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "fingerprint".AsMemory(), IsLiteral = false }] },
                new (){ Parts = [ new() { Name = ".css".AsMemory(), IsLiteral = true }] }
                ]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_ComplexFingerprintExpression_Start()
    {
        var pattern = StaticWebAssetPathPattern.Parse("#[.{fingerprint}].css", "MyApp");
        var expected = new StaticWebAssetPathPattern("#[.{fingerprint}].css")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "fingerprint".AsMemory(), IsLiteral = false }] },
                new (){ Parts = [ new() { Name = ".css".AsMemory(), IsLiteral = true }] }
                ]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_ComplexFingerprintExpression_End()
    {
        var pattern = StaticWebAssetPathPattern.Parse("site#[.{fingerprint}]", "MyApp");
        var expected = new StaticWebAssetPathPattern("site#[.{fingerprint}]")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = "site".AsMemory(), IsLiteral = true }] },
                new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "fingerprint".AsMemory(), IsLiteral = false }] }
                ]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_ComplexFingerprintExpression_Only()
    {
        var pattern = StaticWebAssetPathPattern.Parse("#[.{fingerprint}]", "MyApp");
        var expected = new StaticWebAssetPathPattern("#[.{fingerprint}]")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "fingerprint".AsMemory(), IsLiteral = false }] }
                ]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_ComplexFingerprintExpression_Multiple()
    {
        var pattern = StaticWebAssetPathPattern.Parse("css/site#[.{fingerprint}]-#[.{version}].css", "MyApp");
        var expected = new StaticWebAssetPathPattern("css/site#[.{fingerprint}]-#[.{version}].css")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = "css/site".AsMemory(), IsLiteral = true }] },
                new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "fingerprint".AsMemory(), IsLiteral = false }] },
                new (){ Parts = [ new() { Name = "-".AsMemory(), IsLiteral = true }] },
                new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "version".AsMemory(), IsLiteral = false }] },
                new (){ Parts = [ new() { Name = ".css".AsMemory(), IsLiteral = true }] }
                ]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_ComplexFingerprintExpression_ConsecutiveExpressions()
    {
        var pattern = StaticWebAssetPathPattern.Parse("css/site#[.{fingerprint}]#[.{version}].css", "MyApp");
        var expected = new StaticWebAssetPathPattern("css/site#[.{fingerprint}]#[.{version}].css")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = "css/site".AsMemory(), IsLiteral = true }] },
                new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "fingerprint".AsMemory(), IsLiteral = false }] },
                new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "version".AsMemory(), IsLiteral = false }] },
                new (){ Parts = [ new() { Name = ".css".AsMemory(), IsLiteral = true }] }
                ]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_SimpleFingerprintExpression_Start()
    {
        var pattern = StaticWebAssetPathPattern.Parse("#[{fingerprint}].css", "MyApp");
        var expected = new StaticWebAssetPathPattern("#[{fingerprint}].css")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = "fingerprint".AsMemory(), IsLiteral = false }] },
                new (){ Parts = [ new() { Name = ".css".AsMemory(), IsLiteral = true }] }
                ]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_SimpleFingerprintExpression_Middle()
    {
        var pattern = StaticWebAssetPathPattern.Parse("css/site#[{fingerprint}].css", "MyApp");
        var expected = new StaticWebAssetPathPattern("css/site#[{fingerprint}].css")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = "css/site".AsMemory(), IsLiteral = true }] },
                new (){ Parts = [ new() { Name = "fingerprint".AsMemory(), IsLiteral = false }] },
                new (){ Parts = [ new() { Name = ".css".AsMemory(), IsLiteral = true }] }
                ]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_SimpleFingerprintExpression_End()
    {
        var pattern = StaticWebAssetPathPattern.Parse("site#[{fingerprint}]", "MyApp");
        var expected = new StaticWebAssetPathPattern("site#[{fingerprint}]")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = "site".AsMemory(), IsLiteral = true }] },
                new (){ Parts = [ new() { Name = "fingerprint".AsMemory(), IsLiteral = false }] }
            ]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_SimpleFingerprintExpression_Only()
    {
        var pattern = StaticWebAssetPathPattern.Parse("#[{fingerprint}]", "MyApp");
        var expected = new StaticWebAssetPathPattern("#[{fingerprint}]")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = "fingerprint".AsMemory(), IsLiteral = false }] }
            ]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_SimpleFingerprintExpression_WithEmbeddedValues()
    {
        var pattern = StaticWebAssetPathPattern.Parse("#[{fingerprint=value}]", "MyApp");
        var expected = new StaticWebAssetPathPattern("#[{fingerprint=value}]")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = "fingerprint".AsMemory(), Value = "value".AsMemory(), IsLiteral = false }] }
                ]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_ComplexExpression_MultipleVariables()
    {
        var pattern = StaticWebAssetPathPattern.Parse("css/site#[.{fingerprint}-{version}].css", "MyApp");
        var expected = new StaticWebAssetPathPattern("css/site#[.{fingerprint}-{version}].css")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = "css/site".AsMemory(), IsLiteral = true }] },
                new (){ Parts = [
                    new() { Name = ".".AsMemory(), IsLiteral = true },
                    new() { Name = "fingerprint".AsMemory(), IsLiteral = false },
                    new() { Name = "-".AsMemory(), IsLiteral = true },
                    new() { Name = "version".AsMemory(), IsLiteral = false }
                ] },
                new (){ Parts = [ new() { Name = ".css".AsMemory(), IsLiteral = true }] }
            ]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_ComplexExpression_MultipleConsecutiveVariables()
    {
        var pattern = StaticWebAssetPathPattern.Parse("css/site#[.{fingerprint}{version}].css", "MyApp");
        var expected = new StaticWebAssetPathPattern("css/site#[.{fingerprint}{version}].css")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = "css/site".AsMemory(), IsLiteral = true }] },
                new (){ Parts = [
                    new() { Name = ".".AsMemory(), IsLiteral = true },
                    new() { Name = "fingerprint".AsMemory(), IsLiteral = false },
                    new() { Name = "version".AsMemory(), IsLiteral = false }
                ] },
                new (){ Parts = [ new() { Name = ".css".AsMemory(), IsLiteral = true }] }
            ]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_ComplexExpression_StartsWithVariable()
    {
        var pattern = StaticWebAssetPathPattern.Parse("#[{fingerprint}.]css", "MyApp");
        var expected = new StaticWebAssetPathPattern("#[{fingerprint}.]css")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = "fingerprint".AsMemory(), IsLiteral = false }, new() { Name = ".".AsMemory(), IsLiteral = true }] },
                new (){ Parts = [ new() { Name = "css".AsMemory(), IsLiteral = true }] }
            ]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_OptionalExpressions_End()
    {
        var pattern = StaticWebAssetPathPattern.Parse("site#[.{fingerprint}]?", "MyApp");
        var expected = new StaticWebAssetPathPattern("site#[.{fingerprint}]?")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = "site".AsMemory(), IsLiteral = true }] },
                new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "fingerprint".AsMemory(), IsLiteral = false }], IsOptional = true }
            ]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_OptionalPreferredExpressions()
    {
        var pattern = StaticWebAssetPathPattern.Parse("site#[.{fingerprint}]!", "MyApp");
        var expected = new StaticWebAssetPathPattern("site#[.{fingerprint}]!")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = "site".AsMemory(), IsLiteral = true }] },
                new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "fingerprint".AsMemory(), IsLiteral = false }], IsOptional = true, IsPreferred = true }
            ]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_OptionalExpressions_Start()
    {
        var pattern = StaticWebAssetPathPattern.Parse("#[.{fingerprint}]?site", "MyApp");
        var expected = new StaticWebAssetPathPattern("#[.{fingerprint}]?site")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "fingerprint".AsMemory(), IsLiteral = false }], IsOptional = true },
                new (){ Parts = [ new() { Name = "site".AsMemory(), IsLiteral = true }]
            }]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_OptionalExpressions_Middle()
    {
        var pattern = StaticWebAssetPathPattern.Parse("site#[.{fingerprint}]?site", "MyApp");
        var expected = new StaticWebAssetPathPattern("site#[.{fingerprint}]?site")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = "site".AsMemory(), IsLiteral = true }] },
                new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "fingerprint".AsMemory(), IsLiteral = false }], IsOptional = true },
                new (){ Parts = [ new() { Name = "site".AsMemory(), IsLiteral = true }]
                           }]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_OptionalExpressions_Only()
    {
        var pattern = StaticWebAssetPathPattern.Parse("#[.{fingerprint}]?", "MyApp");
        var expected = new StaticWebAssetPathPattern("#[.{fingerprint}]?")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "fingerprint".AsMemory(), IsLiteral = false }], IsOptional = true }
            ]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_MultipleOptionalExpressions()
    {
        var pattern = StaticWebAssetPathPattern.Parse("#[.{fingerprint}]?site#[.{version}]?", "MyApp");
        var expected = new StaticWebAssetPathPattern("#[.{fingerprint}]?site#[.{version}]?")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "fingerprint".AsMemory(), IsLiteral = false }], IsOptional = true },
                new (){ Parts = [ new() { Name = "site".AsMemory(), IsLiteral = true }], IsOptional = false },
                new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "version".AsMemory(), IsLiteral = false }], IsOptional = true }
            ]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanParse_ConsecutiveOptionalExpressions()
    {
        var pattern = StaticWebAssetPathPattern.Parse("#[.{fingerprint}]?#[.{version}]?", "MyApp");
        var expected = new StaticWebAssetPathPattern("#[.{fingerprint}]?#[.{version}]?")
        {
            Segments =
            [
                new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "fingerprint".AsMemory(), IsLiteral = false }], IsOptional = true },
                new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "version".AsMemory(), IsLiteral = false }], IsOptional = true }
            ]
        };

        Assert.AreEqual(expected, pattern);
    }

    [TestMethod]
    public void CanReplaceTokens_PathWithNoExpressions()
    {
        var pattern = StaticWebAssetPathPattern.Parse("css/site.css", "MyApp");
        var tokens = new StaticWebAsset
        {
            Fingerprint = "asdf1234"
        };
        var (path, _) = pattern.ReplaceTokens(tokens, CreateTestResolver());

        Assert.AreEqual("css/site.css", path);
    }

    [TestMethod]
    public void CanReplaceTokens_ComplexFingerprintExpression_Middle()
    {
        var pattern = StaticWebAssetPathPattern.Parse("css/site#[.{fingerprint}].css", "MyApp");
        var tokens = new StaticWebAsset
        {
            Fingerprint = "asdf1234"
        };
        var (path, _) = pattern.ReplaceTokens(tokens, CreateTestResolver());

        Assert.AreEqual("css/site.asdf1234.css", path);
    }

    [TestMethod]
    public void CanReplaceTokens_ComplexFingerprintExpression_Start()
    {
        var pattern = StaticWebAssetPathPattern.Parse("#[.{fingerprint}].css", "MyApp");
        var tokens = new StaticWebAsset
        {
            Fingerprint = "asdf1234"
        };
        var (path, _) = pattern.ReplaceTokens(tokens, CreateTestResolver());

        Assert.AreEqual(".asdf1234.css", path);
    }

    [TestMethod]
    public void CanReplaceTokens_ComplexFingerprintExpression_End()
    {
        var pattern = StaticWebAssetPathPattern.Parse("site#[.{fingerprint}]", "MyApp");
        var tokens = new StaticWebAsset
        {
            Fingerprint = "asdf1234"
        };
        var (path, _) = pattern.ReplaceTokens(tokens, CreateTestResolver());

        Assert.AreEqual("site.asdf1234", path);
    }

    [TestMethod]
    public void CanReplaceTokens_ComplexFingerprintExpression_Only()
    {
        var pattern = StaticWebAssetPathPattern.Parse("#[.{fingerprint}]", "MyApp");
        var tokens = new StaticWebAsset
        {
            Fingerprint = "asdf1234"
        };
        var (path, _) = pattern.ReplaceTokens(tokens, CreateTestResolver());

        Assert.AreEqual(".asdf1234", path);
    }

    [TestMethod]
    public void CanReplaceTokens_ComplexFingerprintExpression_Multiple()
    {
        var pattern = StaticWebAssetPathPattern.Parse("css/site#[.{fingerprint}]-#[.{version}].css", "MyApp");
        var tokens = new StaticWebAsset
        {
            Fingerprint = "asdf1234",
        };
        var (path, _) = pattern.ReplaceTokens(
            tokens,
            CreateTestResolver(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["version"] = "v1" }));

        Assert.AreEqual("css/site.asdf1234-.v1.css", path);
    }

    [TestMethod]
    public void CanReplaceTokens_ComplexFingerprintExpression_ConsecutiveExpressions()
    {
        var pattern = StaticWebAssetPathPattern.Parse("css/site#[.{fingerprint}]#[.{version}].css", "MyApp");
        var tokens = new StaticWebAsset
        {
            Fingerprint = "asdf1234",
        };
        var (path, _) = pattern.ReplaceTokens(
            tokens,
            CreateTestResolver(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["version"] = "v1" }));

        Assert.AreEqual("css/site.asdf1234.v1.css", path);
    }

    [TestMethod]
    public void CanReplaceTokens_SimpleFingerprintExpression_Start()
    {
        var pattern = StaticWebAssetPathPattern.Parse("#[{fingerprint}].css", "MyApp");
        var tokens = new StaticWebAsset
        {
            Fingerprint = "asdf1234"
        };
        var (path, _) = pattern.ReplaceTokens(tokens, CreateTestResolver());

        Assert.AreEqual("asdf1234.css", path);
    }

    [TestMethod]
    public void CanReplaceTokens_SimpleFingerprintExpression_Middle()
    {
        var pattern = StaticWebAssetPathPattern.Parse("css/site#[{fingerprint}].css", "MyApp");
        var tokens = new StaticWebAsset
        {
            Fingerprint = "asdf1234"
        };
        var (path, _) = pattern.ReplaceTokens(tokens, CreateTestResolver());

        Assert.AreEqual("css/siteasdf1234.css", path);
    }

    [TestMethod]
    public void CanReplaceTokens_SimpleFingerprintExpression_End()
    {
        var pattern = StaticWebAssetPathPattern.Parse("site#[{fingerprint}]", "MyApp");
        var tokens = new StaticWebAsset
        {
            Fingerprint = "asdf1234"
        };
        var (path, _) = pattern.ReplaceTokens(tokens, CreateTestResolver());

        Assert.AreEqual("siteasdf1234", path);
    }

    [TestMethod]
    public void CanReplaceTokens_SimpleFingerprintExpression_Only()
    {
        var pattern = StaticWebAssetPathPattern.Parse("#[{fingerprint}]", "MyApp");
        var tokens = new StaticWebAsset
        {
            Fingerprint = "asdf1234"
        };
        var (path, _) = pattern.ReplaceTokens(tokens, CreateTestResolver());

        Assert.AreEqual("asdf1234", path);
    }

    [TestMethod]
    public void CanReplaceTokens_SimpleFingerprintExpression_WithEmbeddedValues()
    {
        var pattern = StaticWebAssetPathPattern.Parse("#[{fingerprint=embedded}]", "MyApp");
        var tokens = new StaticWebAsset
        {
            Fingerprint = "asdf1234"
        };
        var (path, _) = pattern.ReplaceTokens(tokens, CreateTestResolver());

        Assert.AreEqual("embedded", path);
    }

    [TestMethod]
    public void CanReplaceTokens_ComplexExpression_MultipleVariables()
    {
        var pattern = StaticWebAssetPathPattern.Parse("css/site#[.{fingerprint}-{version}].css", "MyApp");
        var tokens = new StaticWebAsset
        {
            Fingerprint = "asdf1234",
        };
        var (path, _) = pattern.ReplaceTokens(
            tokens,
            CreateTestResolver(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["version"] = "v1" }));

        Assert.AreEqual("css/site.asdf1234-v1.css", path);
    }

    [TestMethod]
    public void CanReplaceTokens_ComplexExpression_MultipleConsecutiveVariables()
    {
        var pattern = StaticWebAssetPathPattern.Parse("css/site#[.{fingerprint}{version}].css", "MyApp");
        var tokens = new StaticWebAsset
        {
            Fingerprint = "asdf1234",
        };
        var (path, _) = pattern.ReplaceTokens(
            tokens,
            CreateTestResolver(new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase) { ["version"] = "v1" }));

        Assert.AreEqual("css/site.asdf1234v1.css", path);
    }

    [TestMethod]
    public void CanReplaceTokens_ComplexExpression_StartsWithVariable()
    {
        var pattern = StaticWebAssetPathPattern.Parse("#[{fingerprint}.]css", "MyApp");
        var tokens = new StaticWebAsset
        {
            Fingerprint = "asdf1234"
        };
        var (path, _) = pattern.ReplaceTokens(tokens, CreateTestResolver());

        Assert.AreEqual("asdf1234.css", path);
    }

    [TestMethod]
    public void CanReplaceTokens_ThrowsException_IfRequiredExpressionIsValue()
    {
        var pattern = StaticWebAssetPathPattern.Parse("css/site#[.{fingerprint}].css", "MyApp");
        var tokens = new StaticWebAsset();
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => pattern.ReplaceTokens(tokens, CreateTestResolver()));
        Assert.AreEqual("Token 'fingerprint' not provided for 'css/site#[.{fingerprint}].css'.", exception.Message);
    }

    [TestMethod]
    public void CanReplaceTokens_ThrowsException_MultipleTokenComplexExpression_MissingAtLeastOneValue()
    {
        var pattern = StaticWebAssetPathPattern.Parse("css/site#[.{fingerprint}-{version}].css", "MyApp");
        var tokens = new StaticWebAsset
        {
            Fingerprint = "asdf1234"
        };
        var exception = Assert.ThrowsExactly<InvalidOperationException>(() => pattern.ReplaceTokens(tokens, CreateTestResolver()));
        Assert.AreEqual("Token 'version' not provided for 'css/site#[.{fingerprint}-{version}].css'.", exception.Message);
    }

    [TestMethod]
    public void CanReplaceTokens_OptionalExpression_OmittedWhenValueNotProvided()
    {
        var pattern = StaticWebAssetPathPattern.Parse("site#[.{fingerprint}]?", "MyApp");
        var tokens = new StaticWebAsset();
        var (path, _) = pattern.ReplaceTokens(tokens, CreateTestResolver());

        Assert.AreEqual("site", path);
    }

    [TestMethod]
    public void CanReplaceTokens_OptionalMultipleTokenComplexExpression_OmittedWhenMissingAtLeastOneValue()
    {
        var pattern = StaticWebAssetPathPattern.Parse("site#[.{fingerprint}.{version}]?", "MyApp");
        var tokens = new StaticWebAsset
        {
            Fingerprint = "asdf1234"
        };
        var (path, _) = pattern.ReplaceTokens(tokens, CreateTestResolver());

        Assert.AreEqual("site", path);
    }

    [TestMethod]
    public void CanExpandRoutes_LiteralPatterns()
    {
        var pattern = StaticWebAssetPathPattern.Parse("css/site.css", "MyApp");
        var routePatterns = pattern.ExpandPatternExpression();

        Assert.AreSequenceEqual([pattern], routePatterns);
    }

    [TestMethod]
    public void CanExpandRoutes_SingleRequiredExpression()
    {
        var pattern = StaticWebAssetPathPattern.Parse("css/site#[.{fingerprint}].css", "MyApp");
        var routePatterns = pattern.ExpandPatternExpression();

        Assert.AreSequenceEqual([pattern], routePatterns);
    }

    [TestMethod]
    public void CanExpandRoutes_SingleOptionalExpression()
    {
        var pattern = StaticWebAssetPathPattern.Parse("site#[.{fingerprint}]?.css", "MyApp");
        var routePatterns = pattern.ExpandPatternExpression();

        var expected = new[]
        {
            new StaticWebAssetPathPattern("site.css")
            {
                Segments =
                [
                    new (){ Parts = [ new() { Name = "site".AsMemory(), IsLiteral = true }] },
                    new (){ Parts = [ new() { Name = ".css".AsMemory(), IsLiteral = true }] }
                ]
            },
            new StaticWebAssetPathPattern("site#[.{fingerprint}].css")
            {
                Segments =
                [
                    new (){ Parts = [ new() { Name = "site".AsMemory(), IsLiteral = true }] },
                    new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "fingerprint".AsMemory(), IsLiteral = false }] },
                    new (){ Parts = [ new() { Name = ".css".AsMemory(), IsLiteral = true }] }
                ]
            }
        };

        Assert.AreSequenceEqual(expected, routePatterns);
    }

    [TestMethod]
    public void CanExpandRoutes_MultipleOptionalExpressions()
    {
        var pattern = StaticWebAssetPathPattern.Parse("site#[.{fingerprint}]?#[.{version}]?.css", "MyApp");
        var routePatterns = pattern.ExpandPatternExpression();

        var expected = new[]
        {
            new StaticWebAssetPathPattern("site.css")
            {
                Segments =
                [
                    new (){ Parts = [ new() { Name = "site".AsMemory(), IsLiteral = true }] },
                    new (){ Parts = [ new() { Name = ".css".AsMemory(), IsLiteral = true }] }
                ]
            },
            new StaticWebAssetPathPattern("site#[.{fingerprint}].css")
            {
                Segments =
                [
                    new (){ Parts = [ new() { Name = "site".AsMemory(), IsLiteral = true }] },
                    new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "fingerprint".AsMemory(), IsLiteral = false }] },
                    new (){ Parts = [ new() { Name = ".css".AsMemory(), IsLiteral = true }] }
                ]
            },
            new StaticWebAssetPathPattern("site#[.{version}].css")
            {
                Segments =
                [
                    new (){ Parts = [ new() { Name = "site".AsMemory(), IsLiteral = true }] },
                    new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "version".AsMemory(), IsLiteral = false }] },
                    new (){ Parts = [ new() { Name = ".css".AsMemory(), IsLiteral = true }] }
                ]
            },
            new StaticWebAssetPathPattern("site#[.{fingerprint}]#[.{version}].css")
            {
                Segments =
                [
                    new (){ Parts = [ new() { Name = "site".AsMemory(), IsLiteral = true }] },
                    new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "fingerprint".AsMemory(), IsLiteral = false }] },
                    new (){ Parts = [ new() { Name = ".".AsMemory(), IsLiteral = true }, new() { Name = "version".AsMemory(), IsLiteral = false }] },
                    new (){ Parts = [ new() { Name = ".css".AsMemory(), IsLiteral = true }] }
                ]
            }
        };

        Assert.AreSequenceEqual(expected, routePatterns);
    }

    private static StaticWebAssetTokenResolver CreateTestResolver(Dictionary<string, string> additionalTokens = null) => new(additionalTokens);
}
