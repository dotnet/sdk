// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Commands;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Utilities;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Microsoft.AspNetCore.StaticWebAssets.Tasks;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using Moq;
using System.Text.RegularExpressions;

namespace Microsoft.AspNetCore.Razor.Tasks;

[TestClass]
public class OverrideHtmlAssetPlaceholdersTest : IDisposable
{
    private readonly string _tempDir;

    public OverrideHtmlAssetPlaceholdersTest()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "OverrideHtmlAssetPlaceholders_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { }
        }
    }

    [TestMethod]
    public void Execute_ReplacesPlaceholderOfHardFingerprintedStylesheet()
    {
        var html = OverridePlaceholders(
            """<link href="css/app#[.{fingerprint}].css" rel="stylesheet" />""",
            includeOnlyHardFingerprintedModules: true,
            CreateAsset("css/app.css", "css/app#[.{fingerprint}]!.css"));

        Assert.AreEqual("""<link href="css/app.fingerprint.css" rel="stylesheet" />""", html);
    }

    [TestMethod]
    public void Execute_ReplacesPlaceholderOfHardFingerprintedImage()
    {
        var html = OverridePlaceholders(
            """<link rel="icon" type="image/png" href="favicon#[.{fingerprint}].png" />""",
            includeOnlyHardFingerprintedModules: true,
            CreateAsset("favicon.png", "favicon#[.{fingerprint}]!.png"));

        Assert.AreEqual("""<link rel="icon" type="image/png" href="favicon.fingerprint.png" />""", html);
    }

    [TestMethod]
    public void Execute_KeepsPathOfSoftFingerprintedStylesheet_WhenOnlyHardFingerprintedAssetsAreIncluded()
    {
        var html = OverridePlaceholders(
            """<link href="css/app#[.{fingerprint}].css" rel="stylesheet" />""",
            includeOnlyHardFingerprintedModules: true,
            CreateAsset("css/app.css", "css/app#[.{fingerprint}]?.css"));

        Assert.AreEqual("""<link href="css/app.css" rel="stylesheet" />""", html);
    }

    [TestMethod]
    public void Execute_ReplacesPlaceholderOfSoftFingerprintedStylesheet_WhenAllFingerprintedAssetsAreIncluded()
    {
        var html = OverridePlaceholders(
            """<link href="css/app#[.{fingerprint}].css" rel="stylesheet" />""",
            includeOnlyHardFingerprintedModules: false,
            CreateAsset("css/app.css", "css/app#[.{fingerprint}]?.css"));

        Assert.AreEqual("""<link href="css/app.fingerprint.css" rel="stylesheet" />""", html);
    }

    [TestMethod]
    public void Execute_AddsOnlyJavaScriptModulesToImportMap()
    {
        var html = OverridePlaceholders(
            """<script type="importmap"></script>""",
            includeOnlyHardFingerprintedModules: true,
            CreateAsset("css/app.css", "css/app#[.{fingerprint}]!.css"),
            CreateAsset("js/main.js", "js/main#[.{fingerprint}]!.js"));

        Assert.Contains("\"./js/main.js\": \"./js/main.fingerprint.js\"", html);
        Assert.DoesNotContain("css/app", html);
    }

    private string OverridePlaceholders(string html, bool includeOnlyHardFingerprintedModules, params StaticWebAsset[] assets)
    {
        var htmlPath = Path.Combine(_tempDir, "index.html");
        File.WriteAllText(htmlPath, html);

        var task = new OverrideHtmlAssetPlaceholders
        {
            Assets = assets.Select(a => a.ToTaskItem()).ToArray(),
            Endpoints = CreateEndpoints(assets),
            IncludeOnlyHardFingerprintedModules = includeOnlyHardFingerprintedModules,
            HtmlFiles = [new TaskItem(htmlPath)],
            OutputPath = Path.Combine(_tempDir, "output"),
            BuildEngine = Mock.Of<IBuildEngine>(),
        };

        Assert.IsTrue(task.Execute());
        Assert.HasCount(1, task.HtmlCandidates);
        return File.ReadAllText(task.HtmlCandidates[0].ItemSpec);
    }

    private static ITaskItem[] CreateEndpoints(StaticWebAsset[] assets)
    {
        var defineStaticWebAssetEndpoints = new DefineStaticWebAssetEndpoints
        {
            CandidateAssets = assets.Select(a => a.ToTaskItem()).ToArray(),
            ExistingEndpoints = [],
            ContentTypeMappings =
            [
                CreateContentMapping("*.css", "text/css"),
                CreateContentMapping("*.js", "text/javascript"),
                CreateContentMapping("*.png", "image/png"),
            ],
            BuildEngine = Mock.Of<IBuildEngine>(),
        };

        Assert.IsTrue(defineStaticWebAssetEndpoints.Execute());
        return defineStaticWebAssetEndpoints.Endpoints;
    }

    private static TaskItem CreateContentMapping(string pattern, string contentType)
    {
        return new TaskItem(contentType, new Dictionary<string, string>
        {
            { "Pattern", pattern },
            { "Priority", "0" }
        });
    }

    private StaticWebAsset CreateAsset(string path, string relativePath)
    {
        var contentRoot = Path.Combine(_tempDir, "wwwroot") + Path.DirectorySeparatorChar;
        return new StaticWebAsset()
        {
            Identity = Path.GetFullPath(Path.Combine(contentRoot, path)),
            SourceId = "MyApp",
            SourceType = StaticWebAsset.SourceTypes.Discovered,
            ContentRoot = contentRoot,
            BasePath = "/",
            RelativePath = relativePath,
            AssetKind = StaticWebAsset.AssetKinds.All,
            AssetMode = StaticWebAsset.AssetModes.All,
            AssetRole = StaticWebAsset.AssetRoles.Primary,
            AssetMergeBehavior = StaticWebAsset.MergeBehaviors.PreferTarget,
            AssetMergeSource = "",
            RelatedAsset = "",
            AssetTraitName = "",
            AssetTraitValue = "",
            CopyToOutputDirectory = StaticWebAsset.AssetCopyOptions.Never,
            CopyToPublishDirectory = StaticWebAsset.AssetCopyOptions.PreserveNewest,
            OriginalItemSpec = path,
            // Avoid reading the disk to compute these
            Integrity = "integrity",
            Fingerprint = "fingerprint",
            FileLength = 10,
            LastWriteTime = DateTime.UtcNow,
        };
    }

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
