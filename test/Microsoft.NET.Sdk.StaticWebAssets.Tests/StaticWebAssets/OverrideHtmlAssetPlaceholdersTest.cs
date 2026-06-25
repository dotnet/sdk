// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;

using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using System.Text.RegularExpressions;

namespace Microsoft.AspNetCore.Razor.Tasks;

[TestClass]

public class OverrideHtmlAssetPlaceholdersTest
{
    [TestMethod]
    [DataRow(
        """
        <script src="main#[.{fingerprint}].js"></script>
        """,
        true,
        "main.js"
    )]
    [DataRow(
        """
        <script src="main#[.{fingerprint}].js">
        </script>
        """,
        true,
        "main.js"
    )]
    [DataRow(
        """
        <script    src="main#[.{fingerprint}].js"   >   </script>
        """,
        true,
        "main.js"
    )]
    [DataRow(
        """
        <script src="./main#[.{fingerprint}].js"></script>
        """,
        true,
        "./main.js"
    )]
    [DataRow(
        """
        <script src="./folder/folder/file.name.something#[.{fingerprint}].js"></script>
        """,
        true,
        "./folder/folder/file.name.something.js"
    )]
    [DataRow(
        """
        <script src="main#[.{fingerprint}].suffix.js"></script>
        """,
        true,
        "main.suffix.js"
    )]
    [DataRow(
        """
        <script src="/root/main#[.{fingerprint}].suffix.js"></script>
        """,
        true,
        "/root/main.suffix.js"
    )]
    [DataRow(
        """
        <script src="main.js"></script>
        """,
        false
    )]
    [DataRow(
        """
        <script src='main#[.{fingerprint}].js'></script>
        """,
        false
    )]
    [DataRow(
        """
        <script src=main#[.{fingerprint}].js></script>
        """,
        false
    )]
    [DataRow(
        """
        <h1>main#[.{fingerprint}].js</h1>
        """,
        false
    )]
    [DataRow(
        """
        <script>
          var url = "main#[.{fingerprint}].js"
        </script>
        """,
        true,
        "main.js"
    )]
    [DataRow(
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
    [DataRow(
        """
        <link href="main#[.{fingerprint}].js" rel="preload" as="script" fetchpriority="high" crossorigin="anonymous">
        """,
        true,
        "main.js"
    )]
    public void ValidateAssetsRegex(string input, bool shouldMatch, string fileName = null)
    {
        var match = OverrideHtmlAssetPlaceholders._assetsRegex.Match(input);
        Assert.AreEqual(shouldMatch, match.Success);

        if (fileName != null)
        {
            Assert.AreEqual(fileName, match.Groups["fileName"].Value + match.Groups["fileExtension"].Value);
        }
    }

    [TestMethod]
    [DataRow(
        """
        <script type="importmap"></script>
        """,
        true
    )]
    [DataRow(
        """
        <script   type="importmap"   >   </script>
        """,
        true
    )]
    [DataRow(
        """
        <script type="importmap">
        </script>
        """,
        true
    )]
    [DataRow(
        """
        <script
         type="importmap"
        >
        </script>
        """,
        true
    )]
    [DataRow(
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
    [DataRow(
        """
        <script type=importmap></script>
        """,
        false
    )]
    [DataRow(
        """
        <script type='importmap'></script>
        """,
        false
    )]
    public void ValidateImportMapRegex(string input, bool shouldMatch)
    {
        Assert.AreEqual(shouldMatch, OverrideHtmlAssetPlaceholders._importMapRegex.Match(input).Success);
    }

    [TestMethod]
    [DataRow(
        """
        <link rel="preload"/>
        """,
        true
    )]
    [DataRow(
        """
        <link   rel="preload"    />
        """,
        true
    )]
    [DataRow(
        """
        <link    rel="preload">
        """,
        true
    )]
    [DataRow(
        """
        <link rel=preload />
        """,
        false
    )]
    [DataRow(
        """
        <link rel='preload' />
        """,
        false
    )]
    [DataRow(
        """
        <link rel="preload"
        """,
        false
    )]
    [DataRow(
        """
        <link />"
        """,
        false
    )]
    [DataRow(
        """
        <link>"
        """,
        false
    )]
    [DataRow(
        """
        <link rel="preload" href="file.png" />
        """,
        false
    )]
    [DataRow(
        """
        <link rel="preload" id="webassembly" />
        """,
        true,
        "webassembly"
    )]
    [DataRow(
        """
        <link rel="preload" id="webassembly">
        """,
        true,
        "webassembly"
    )]
    [DataRow(
        """
        <link rel="preload" id='webassembly'>
        """,
        false
    )]
    [DataRow(
        """
        <link rel="preload"id="webassembly" />
        """,
        false
    )]
    [DataRow(
        """
        <link id="webassembly" rel="preload" />
        """,
        false
    )]
    [DataRow(
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
        Assert.AreEqual(shouldMatch, match.Success);

        if (group != null)
        {
            Assert.AreEqual(group, match.Groups["group"]?.Value);
        }
    }
}
