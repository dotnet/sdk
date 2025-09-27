// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Text.RegularExpressions;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;

namespace Microsoft.AspNetCore.Razor.Tasks;

public class OverrideHtmlAssetPlaceholdersTest
{
    [Theory]
    [InlineData(
        """
        <script src="main#[.{fingerprint}].js"></script>
        """,
        true,
        "main.js"
    )]
    [InlineData(
        """
        <script src="main#[.{fingerprint}].js">
        </script>
        """,
        true,
        "main.js"
    )]
    [InlineData(
        """
        <script    src="main#[.{fingerprint}].js"   >   </script>
        """,
        true,
        "main.js"
    )]
    [InlineData(
        """
        <script src="./main#[.{fingerprint}].js"></script>
        """,
        true,
        "./main.js"
    )]
    [InlineData(
        """
        <script src="./folder/folder/file.name.something#[.{fingerprint}].js"></script>
        """,
        true,
        "./folder/folder/file.name.something.js"
    )]
    [InlineData(
        """
        <script src="main#[.{fingerprint}].suffix.js"></script>
        """,
        true,
        "main.suffix.js"
    )]
    [InlineData(
        """
        <script src="/root/main#[.{fingerprint}].suffix.js"></script>
        """,
        true,
        "/root/main.suffix.js"
    )]
    [InlineData(
        """
        <script src="main.js"></script>
        """,
        false
    )]
    [InlineData(
        """
        <script src='main#[.{fingerprint}].js'></script>
        """,
        false
    )]
    [InlineData(
        """
        <script src=main#[.{fingerprint}].js></script>
        """,
        false
    )]
    [InlineData(
        """
        <h1>main#[.{fingerprint}].js</h1>
        """,
        false
    )]
    [InlineData(
        """
        <script>
          var url = "main#[.{fingerprint}].js"
        </script>
        """,
        true,
        "main.js"
    )]
    [InlineData(
        """
        <script type='importmap'>{
          "imports": {
            "./main.js": "./main#[.{fingerprint}].js"
          }
        }</script>
        """,
        true,
        "./main.js"
    )]
    [InlineData(
        """
        <link href="main#[.{fingerprint}].js" rel="preload" as="script" fetchpriority="high" crossorigin="anonymous">
        """,
        true,
        "main.js"
    )]
    public void ValidateAssetsRegex(string input, bool shouldMatch, string fileName = null)
    {
        var match = OverrideHtmlAssetPlaceholders._assetsRegex.Match(input);
        Assert.Equal(shouldMatch, match.Success);

        if (fileName != null)
        {
            Assert.Equal(fileName, match.Groups["fileName"].Value + match.Groups["fileExtension"].Value);
        }
    }

    [Theory]
    [InlineData(
        """
        <script type="importmap"></script>
        """,
        true
    )]
    [InlineData(
        """
        <script   type="importmap"   >   </script>
        """,
        true
    )]
    [InlineData(
        """
        <script type="importmap">
        </script>
        """,
        true
    )]
    [InlineData(
        """
        <script
         type="importmap"
        >
        </script>
        """,
        true
    )]
    [InlineData(
        """
        <script type="importmap">
        {
            "imports": {
            }
        }
        </script>
        """,
        false
    )]
    [InlineData(
        """
        <script type=importmap></script>
        """,
        false
    )]
    [InlineData(
        """
        <script type='importmap'></script>
        """,
        false
    )]
    public void ValidateImportMapRegex(string input, bool shouldMatch)
    {
        Assert.Equal(shouldMatch, OverrideHtmlAssetPlaceholders._importMapRegex.Match(input).Success);
    }

    [Theory]
    [InlineData(
        """
        <link rel="preload"/>
        """,
        true
    )]
    [InlineData(
        """
        <link   rel="preload"    />
        """,
        true
    )]
    [InlineData(
        """
        <link    rel="preload">
        """,
        true
    )]
    [InlineData(
        """
        <link rel=preload />
        """,
        false
    )]
    [InlineData(
        """
        <link rel='preload' />
        """,
        false
    )]
    [InlineData(
        """
        <link rel="preload"
        """,
        false
    )]
    [InlineData(
        """
        <link />"
        """,
        false
    )]
    [InlineData(
        """
        <link>"
        """,
        false
    )]
    [InlineData(
        """
        <link rel="preload" href="file.png" />
        """,
        false
    )]
    [InlineData(
        """
        <link rel="preload" id="webassembly" />
        """,
        true,
        "webassembly"
    )]
    [InlineData(
        """
        <link rel="preload" id="webassembly">
        """,
        true,
        "webassembly"
    )]
    [InlineData(
        """
        <link rel="preload" id='webassembly'>
        """,
        false
    )]
    [InlineData(
        """
        <link rel="preload"id="webassembly" />
        """,
        false
    )]
    [InlineData(
        """
        <link id="webassembly" rel="preload" />
        """,
        false
    )]
    [InlineData(
        """
        <link
         rel="preload"
         id="webassembly"
        />
        """,
        true,
        "webassembly"
    )]
    public void ValidatePreloadRegex(string input, bool shouldMatch, string group = null)
    {
        var match = OverrideHtmlAssetPlaceholders._preloadRegex.Match(input);
        Assert.Equal(shouldMatch, match.Success);

        if (group != null)
        {
            Assert.Equal(group, match.Groups["group"]?.Value);
        }
    }
}
