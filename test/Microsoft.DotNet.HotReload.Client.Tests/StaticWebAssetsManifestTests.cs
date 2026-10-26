// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Watch.UnitTests;
using Microsoft.Extensions.Logging.Abstractions;

namespace Microsoft.DotNet.HotReload.UnitTests;

public class StaticWebAssetsManifestTests(ITestOutputHelper testOutput)
{
    private static MemoryStream CreateStream(string content)
        => new(Encoding.UTF8.GetBytes(content));

    private static string GetContentRoot(params string[] segments)
        => (Path.Combine(segments).TrimEnd(Path.DirectorySeparatorChar) + Path.DirectorySeparatorChar).Replace("\\", "\\\\");

    [Fact]
    public void TryParse_Empty()
    {
        using var stream = CreateStream("null");
        var logger = new TestLogger(testOutput);
        Assert.Null(StaticWebAssetsManifest.TryParse(stream, "file.json", logger));
        Assert.True(logger.HasError);
    }

    [Fact]
    public void TryParse_MissingContentRoots()
    {
        using var stream = CreateStream("""
        {
          "Root": {
            "Children": {
              "site.css": {
                "Asset": {
                    "ContentRootIndex": 0,
                    "SubPath": "css/site.css"
                }
              }
            }
          }
        }
        """);

        var logger = new TestLogger(testOutput);
        Assert.Null(StaticWebAssetsManifest.TryParse(stream, "file.json", logger));
        Assert.True(logger.HasError);
    }

    [Fact]
    public void TryParse_InvalidRootIndex()
    {
        var root = Path.GetTempPath();

        using var stream = CreateStream($$"""
        {
          "ContentRoots": [
            "{{GetContentRoot(root)}}"
          ],
          "Root": {
            "Children": {
              "site.css": {
                "Asset": {
                    "ContentRootIndex": 1,
                    "SubPath": "css/site.css"
                }
              }
            }
          }
        }
        """);

        var logger = new TestLogger(testOutput);
        var manifest = StaticWebAssetsManifest.TryParse(stream, "file.json", logger);

        AssertEx.SequenceEqual(
        [
            "[Warning] Failed to parse 'file.json': Invalid value of ContentRootIndex: 1",
        ], logger.GetAndClearMessages());

        Assert.NotNull(manifest);
        Assert.Empty(manifest.UrlToPathMap);
        Assert.Empty(manifest.DiscoveryPatterns);
    }

    [Fact]
    public void TryParse_InvalidCharactersInSubPath()
    {
        var root = Path.GetTempPath();

        using var stream = CreateStream($$"""
        {
          "ContentRoots": [
            "{{GetContentRoot(root)}}"
          ],
          "Root": {
            "Children": {
              "site.css": {
                "Asset": {
                    "ContentRootIndex": 0,
                    "SubPath": "<>.css"
                }
              }
            }
          }
        }
        """);

        var logger = new TestLogger(testOutput);
        var manifest = StaticWebAssetsManifest.TryParse(stream, "file.json", logger);
        Assert.Empty(logger.GetAndClearMessages());
        Assert.NotNull(manifest);

        AssertEx.SequenceEqual(
        [
            new("site.css", Path.Join(root, "<>.css")),
        ], manifest.UrlToPathMap.OrderBy(e => e.Key));
    }

    [Fact]
    public void TryParse_NoChildren()
    {
        var root = Path.GetTempPath();

        using var stream = CreateStream($$"""
        {
          "ContentRoots": [
            "{{GetContentRoot(root, "Classlib2", "bundles")}}"
          ],
          "Root": {
            "Children": {
            }
          }
        }
        """);

        var logger = new TestLogger(testOutput);
        var manifest = StaticWebAssetsManifest.TryParse(stream, "file.json", logger);
        Assert.NotNull(manifest);
        Assert.False(logger.HasWarning);
        Assert.Empty(manifest.UrlToPathMap);
        Assert.Empty(manifest.DiscoveryPatterns);
    }

    [Fact]
    public void TryParse_TopAsset()
    {
        var root = Path.GetTempPath();

        using var stream = CreateStream($$"""
        {
          "ContentRoots": [
            "{{GetContentRoot(root, "Classlib2", "bundles")}}"
          ],
          "Root": {
            "Children": null,
            "Asset": {
                "ContentRootIndex": 0,
                "SubPath": "css/site.css"
            },
            "Patterns": null
          }
        }
        """);

        var logger = new TestLogger(testOutput);
        var manifest = StaticWebAssetsManifest.TryParse(stream, "file.json", logger);
        AssertEx.SequenceEqual(
        [
            "[Warning] Failed to parse 'file.json': Asset has no URL",
        ], logger.GetAndClearMessages());

        Assert.NotNull(manifest);
        Assert.Empty(manifest.UrlToPathMap);
        Assert.Empty(manifest.DiscoveryPatterns);
    }

    [Fact]
    public void TryParse_RootIsNotFullPath()
    {
        using var stream = CreateStream("""
        {
          "ContentRoots": [
            "a/b"
          ],
          "Root": {
            "Children": null,
            "Asset": {
                "ContentRootIndex": 0,
                "SubPath": "css/site.css"
            },
            "Patterns": null
          }
        }
        """);

        var logger = new TestLogger(testOutput);
        var manifest = StaticWebAssetsManifest.TryParse(stream, "file.json", logger);
        Assert.True(logger.HasWarning);
        Assert.NotNull(manifest);
        Assert.Empty(manifest.UrlToPathMap);
        Assert.Empty(manifest.DiscoveryPatterns);
    }

    [Fact]
    public void TryParse_ValidFile()
    {
        var root = Path.GetTempPath();

        using var stream = CreateStream($$"""
        {
          "ContentRoots": [
            "{{GetContentRoot(root, "Classlib2", "bundles")}}",
            "{{GetContentRoot(root, "Classlib2", "scopedcss")}}",
            "{{GetContentRoot(root, "Classlib2", "SomePath")}}",
            "{{GetContentRoot(root, "Classlib", "wwwroot")}}",
            "{{GetContentRoot(root, "Classlib2", "wwwroot")}}",
            "{{GetContentRoot(root, "Classlib3", "somePath")}}"
          ],
          "Root": {
            "Children": {
              "css": {
                "Children": {
                  "site.css": {
                    "Children": null,
                    "Asset": {
                      "ContentRootIndex": 0,
                      "SubPath": "css/site.css"
                    },
                    "Patterns": null
                  }
                },
                "Asset": null,
                "Patterns": null
              },
              "_content": {
                "Children": {
                  "Classlib": {
                    "Children": {
                      "css": {
                        "Children": {
                          "site.css": {
                            "Children": null,
                            "Asset": {
                              "ContentRootIndex": 3,
                              "SubPath": "css/site.css"
                            },
                            "Patterns": null
                          }
                        },
                        "Asset": null,
                        "Patterns": null
                      }
                    },
                    "Asset": null,
                    "Patterns": null
                  },
                  "Classlib2": {
                    "Children": {
                      "Classlib2.bundle.fingerprint.scp.css": {
                        "Children": null,
                        "Asset": {
                          "ContentRootIndex": 2,
                          "SubPath": "Classlib2.bundle.scp.css"
                        },
                        "Patterns": null
                      },
                      "background.png": {
                        "Children": null,
                        "Asset": {
                          "ContentRootIndex": 4,
                          "SubPath": "background.png"
                        },
                        "Patterns": null
                      }
                    },
                    "Asset": null,
                    "Patterns": null
                  },
                  "Classlib3": {
                    "Children": {
                      "background.png": {
                        "Children": null,
                        "Asset": {
                          "ContentRootIndex": 5,
                          "SubPath": "background.png"
                        },
                        "Patterns": null
                      }
                    },
                    "Asset": null,
                    "Patterns": null
                  }
                },
                "Asset": null,
                "Patterns": null
              },
              "Classlib2.styles.css": {
                "Children": null,
                "Asset": {
                  "ContentRootIndex": 0,
                  "SubPath": "Classlib2.styles.css"
                },
                "Patterns": null
              },
              "Classlib2.scopedstyles.css": {
                "Children": null,
                "Asset": {
                  "ContentRootIndex": 1,
                  "SubPath": "Classlib2.scopedstyles.css"
                },
                "Patterns": null
              }
            }
          }
        }
        """);

        var manifest = StaticWebAssetsManifest.TryParse(stream, "file.json", NullLogger.Instance);

        Assert.NotNull(manifest);
        AssertEx.SequenceEqual(
        [
            new("_content/Classlib/css/site.css", Path.Combine(root, "Classlib", "wwwroot", "css", "site.css")),
            new("_content/Classlib2/background.png", Path.Combine(root, "Classlib2", "wwwroot", "background.png")),
            new("_content/Classlib2/Classlib2.bundle.fingerprint.scp.css", Path.Combine(root, "Classlib2", "SomePath", "Classlib2.bundle.scp.css")),
            new("_content/Classlib3/background.png", Path.Combine(root, "Classlib3", "somePath", "background.png")),
            new("Classlib2.scopedstyles.css", Path.Combine(root, "Classlib2", "scopedcss", "Classlib2.scopedstyles.css")),
            new("Classlib2.styles.css", Path.Combine(root, "Classlib2", "bundles", "Classlib2.styles.css")),
            new("css/site.css", Path.Combine(root, "Classlib2", "bundles", "css", "site.css")),
        ], manifest.UrlToPathMap.OrderBy(e => e.Key));

        Assert.Empty(manifest.DiscoveryPatterns);
    }

    [Fact]
    public void TryParse_Patterns()
    {
        var root = Path.GetTempPath();

        using var stream = CreateStream($$"""
        {
          "ContentRoots": [
            "{{GetContentRoot(root)}}"
          ],
          "Root": {
            "Children": {
              "site.css" : {
                "Asset": {
                  "ContentRootIndex": 0,
                  "SubPath": "css/site.css"
                }
              }
            },
            "Patterns": [
              {
                "ContentRootIndex": 0,
                "Pattern": "**",
                "Depth": 0
              }
            ]
          }
        }
        """);

        var logger = new TestLogger(testOutput);
        var manifest = StaticWebAssetsManifest.TryParse(stream, "file.json", logger);
        Assert.False(logger.HasWarning);
        Assert.NotNull(manifest);

        AssertEx.SequenceEqual(
        [
            new("site.css", Path.Combine(root, "css", "site.css")),
        ], manifest.UrlToPathMap.OrderBy(e => e.Key));

        AssertEx.SequenceEqual(
        [
            $"{root};**;"
        ], manifest.DiscoveryPatterns.Select(p => $"{p.Directory};{p.Pattern};{p.BaseUrl}"));
    }
}
