// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.Reflection;
using System.Text.Json.Nodes;
using Microsoft.Build.Framework;
using Microsoft.VisualStudio.TestTools.UnitTesting.Combinatorial;
using static Microsoft.NET.Build.Tasks.ResolvePackageAssets;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    [TestClass]
    public class GivenAResolvePackageAssetsTask
    {
        [TestMethod]
        public void ItHashesAllParameters()
        {
            IEnumerable<PropertyInfo> inputProperties;

            var task = InitializeTask(out inputProperties);

            byte[] oldHash;
            try
            {
                oldHash = task.HashSettings();
            }
            catch (ArgumentNullException ex)
            {
                throw new InvalidOperationException("HashSettings is likely not correctly handling null value of one or more optional task parameters", ex);
            }

            foreach (var property in inputProperties)
            {
                switch (property.PropertyType)
                {
                    case var t when t == typeof(bool):
                        property.SetValue(task, true);
                        break;

                    case var t when t == typeof(string):
                        property.SetValue(task, property.Name);
                        break;

                    case var t when t == typeof(ITaskItem[]):
                        property.SetValue(task, new[] { new MockTaskItem() { ItemSpec = property.Name } });
                        break;

                    default:
                        Assert.Fail($"{property.Name} is not a bool or string or ITaskItem[]. Update the test code to handle that.");
                        throw null; // unreachable
                }

                byte[] newHash = task.HashSettings();
                newHash.Should().NotBeEquivalentTo(
                    oldHash,
                    because: $"{property.Name} should be included in hash.");

                oldHash = newHash;
            }
        }

        [TestMethod]
        public void ItDoesNotHashDesignTimeBuild()
        {
            var task = InitializeTask(out _);

            task.DesignTimeBuild = false;

            byte[] oldHash = task.HashSettings();

            task.DesignTimeBuild = true;

            byte[] newHash = task.HashSettings();

            newHash.Should().BeEquivalentTo(oldHash,
                because: $"{nameof(task.DesignTimeBuild)} should not be included in hash.");
        }

        [TestMethod]
        public void It_does_not_error_on_duplicate_package_names()
        {
            string projectAssetsJsonPath = Path.GetTempFileName();
            var assetsContent = @"{
  `version`: 3,
  `targets`: {
    `net5.0`: {
      `Humanizer.Core/2.8.25`: {
        `type`: `package`
      },
      `Humanizer.Core/2.8.26`: {
        `type`: `package`
      }
    }
  },
  `project`: {
    `version`: `1.0.0`,
    `frameworks`: {
      `net5.0`: {
        `targetAlias`: `net5.0`
      }
    },
    `restore`: {
      `frameworks`: {
        `net5.0`: {
          `targetAlias`: `net5.0`
        }
      }
    }
  }
}".Replace('`', '"');
            File.WriteAllText(projectAssetsJsonPath, assetsContent);

            var task = InitializeTask(out _);
            task.ProjectAssetsFile = projectAssetsJsonPath;
            task.TargetFramework = "net5.0";
            new CacheWriter(task); // Should not error
        }

        private static string AssetsFileWithResourceLocale(string tfm, string locale) => @"
{
  `version`: 3,
  `targets`: {
    `{tfm}`: {
      `JavaScriptEngineSwitcher.Core/3.3.0`: {
        `type`: `package`,
        `compile`: {
          `lib/netstandard2.0/JavaScriptEngineSwitcher.Core.dll`: {}
        },
        `runtime`: {
          `lib/netstandard2.0/JavaScriptEngineSwitcher.Core.dll`: {}
        },
        `resource`: {
          `lib/netstandard2.0/{locale}/JavaScriptEngineSwitcher.Core.resources.dll`: {
            `locale`: `{locale}`
          }
        }
      }
    }
  },
  `project`: {
    `version`: `1.0.0`,
    `frameworks`: {
      `{tfm}`: {
        `targetAlias`: `{tfm}`
      }
    },
    `restore`: {
        `frameworks`: {
          `{tfm}`: {
            `targetAlias`: `{tfm}`
          }
        }
    }
  }
}".Replace("`", "\"").Replace("{tfm}", tfm).Replace("{locale}", locale);

        [DataRow("net7.0", true)]
        [DataRow("net6.0", false)]
        [TestMethod]
        public void It_warns_on_invalid_culture_codes_of_resources(string tfm, bool shouldHaveWarnings)
        {
            string projectAssetsJsonPath = Path.GetTempFileName();
            var assetsContent = AssetsFileWithResourceLocale(tfm, "what is this even");
            File.WriteAllText(projectAssetsJsonPath, assetsContent);
            var task = InitializeTask(out _);
            task.ProjectAssetsFile = projectAssetsJsonPath;
            task.TargetFramework = tfm;
            var writer = new CacheWriter(task, new MockPackageResolver());
            writer.WriteToMemoryStream();
            var engine = task.BuildEngine as MockBuildEngine;

            var invalidContextWarnings = engine.Warnings.Where(msg => msg.Code == "NETSDK1188");
            invalidContextWarnings.Should().HaveCount(shouldHaveWarnings ? 1 : 0);

            var invalidContextMessages = engine.Messages.Where(msg => msg.Code == "NETSDK1188" && msg.Importance == MessageImportance.Low);
            invalidContextMessages.Should().HaveCount(shouldHaveWarnings ? 0 : 1);

        }

        [DataRow("net7.0", true)]
        [DataRow("net6.0", false)]
        [TestMethod]
        public void It_warns_on_incorrectly_cased_culture_codes_of_resources(string tfm, bool shouldHaveWarnings)
        {
            WithResourceAssetsTask(tfm, "ru-ru", task =>
            {
                task.Execute().Should().BeTrue();
                AssertResourceMetadata(task, "ru-ru", "ru-RU");
                var engine = (MockBuildEngine)task.BuildEngine;

                var invalidContextWarnings = engine.Warnings.Where(msg => msg.Code == "NETSDK1187");
                invalidContextWarnings.Should().HaveCount(shouldHaveWarnings ? 1 : 0);

                var invalidContextMessages = engine.Messages.Where(msg => msg.Code == "NETSDK1187" && msg.Importance == MessageImportance.Low);
                invalidContextMessages.Should().HaveCount(shouldHaveWarnings ? 0 : 1);
            });
        }

        [TestMethod]
        [CombinatorialData]
        public void It_normalizes_pseudo_locales_without_casing_diagnostics(
            [CombinatorialValues("net6.0", "net7.0")] string tfm,
            [CombinatorialValues("qps-ploc", "qps-Ploc", "QPS-PLOC",
                "qps-plocm", "qps-Plocm", "qps-PLOCM",
                "qps-ploca", "qps-Ploca", "QPS-PLOCA")] string locale,
            bool filter)
        {
            WithResourceAssetsTask(tfm, locale, task =>
            {
                if (filter)
                {
                    task.SatelliteResourceLanguages = [new MockTaskItem { ItemSpec = locale.ToUpperInvariant() }];
                }

                task.Execute().Should().BeTrue();
                AssertResourceMetadata(task, locale, locale.ToLowerInvariant());
                var engine = (MockBuildEngine)task.BuildEngine;
                engine.Warnings.Should().BeEmpty();
                engine.Messages.Where(msg => msg.Code is "NETSDK1187" or "NETSDK1188").Should().BeEmpty();

                byte[] cache = File.ReadAllBytes(task.ProjectAssetsCacheFile);
                DateTime cacheTime = File.GetLastWriteTimeUtc(task.ProjectAssetsCacheFile);
                task.BuildEngine = new MockBuildEngine();
                task.Execute().Should().BeTrue();
                AssertResourceMetadata(task, locale, locale.ToLowerInvariant());
                File.ReadAllBytes(task.ProjectAssetsCacheFile).Should().Equal(cache);
                File.GetLastWriteTimeUtc(task.ProjectAssetsCacheFile).Should().Be(cacheTime);
                engine = (MockBuildEngine)task.BuildEngine;
                engine.Warnings.Should().BeEmpty();
                engine.Messages.Where(msg => msg.Code is "NETSDK1187" or "NETSDK1188").Should().BeEmpty();
            });
        }

        [TestMethod]
        public void It_invalidates_caches_from_before_pseudo_locale_normalization()
        {
            WithResourceAssetsTask("net7.0", "qps-Ploc", task =>
            {
                task.Execute().Should().BeTrue();
                using (var stream = File.OpenWrite(task.ProjectAssetsCacheFile))
                using (var writer = new BinaryWriter(stream))
                {
                    stream.Position = sizeof(int);
                    writer.Write(12);
                }

                task.BuildEngine = new MockBuildEngine();
                task.Execute().Should().BeTrue();
                AssertResourceMetadata(task, "qps-Ploc", "qps-ploc");
                using var reader = new BinaryReader(File.OpenRead(task.ProjectAssetsCacheFile));
                reader.ReadInt32();
                reader.ReadInt32().Should().BeGreaterThan(12);
            });
        }

        private void WithResourceAssetsTask(string tfm, string locale, Action<ResolvePackageAssets> test)
        {
            string testRoot = Path.Combine(Path.GetTempPath(), "rpa-resources-" + Guid.NewGuid().ToString("N"));
            string packagesPath = Path.Combine(testRoot, "packages");
            string packagePath = Path.Combine(packagesPath, "javascriptengineswitcher.core", "3.3.0");
            Directory.CreateDirectory(packagePath);
            try
            {
                File.WriteAllText(Path.Combine(packagePath, "javascriptengineswitcher.core.3.3.0.nupkg.sha512"), "abc123");
                var assets = JsonNode.Parse(AssetsFileWithResourceLocale(tfm, locale));
                assets["packageFolders"] = new JsonObject { [packagesPath] = new JsonObject() };
                assets["libraries"] = JsonNode.Parse("""
                    {
                      "JavaScriptEngineSwitcher.Core/3.3.0": {
                        "type": "package",
                        "path": "javascriptengineswitcher.core/3.3.0",
                        "files": []
                      }
                    }
                    """);
                var task = InitializeTask(out _);
                task.ProjectAssetsFile = Path.Combine(testRoot, "project.assets.json");
                task.ProjectAssetsCacheFile = Path.Combine(testRoot, "project.assets.cache");
                task.ProjectPath = Path.Combine(testRoot, "test.csproj");
                task.TargetFramework = tfm;
                File.WriteAllText(task.ProjectAssetsFile, assets.ToJsonString());
                File.SetLastWriteTimeUtc(task.ProjectAssetsFile, DateTime.UtcNow.AddMinutes(-1));
                test(task);
            }
            finally
            {
                Directory.Delete(testRoot, recursive: true);
            }
        }

        private static void AssertResourceMetadata(ResolvePackageAssets task, string originalLocale, string outputLocale)
        {
            var resource = task.ResourceAssemblies.Should().ContainSingle().Subject;
            const string assemblyName = "JavaScriptEngineSwitcher.Core.resources.dll";
            resource.ItemSpec.Should().EndWith(Path.Combine("lib", "netstandard2.0", originalLocale, assemblyName));
            resource.GetMetadata(MetadataKeys.PathInPackage).Should().Be($"lib/netstandard2.0/{originalLocale}/{assemblyName}");
            resource.GetMetadata(MetadataKeys.Culture).Should().Be(outputLocale);
            resource.GetMetadata(MetadataKeys.DestinationSubDirectory).Should().Be(outputLocale + Path.DirectorySeparatorChar);
            resource.GetMetadata(MetadataKeys.DestinationSubPath).Should().Be(Path.Combine(outputLocale, assemblyName));
        }

        [DataRow("net7.0", "ckb")]
        [DataRow("net7.0", "ckb-IQ")]
        [DataRow("net6.0", "ckb")]
        [DataRow("net6.0", "ckb-IQ")]
        [TestMethod]
        public void It_does_not_warn_for_remapped_resource_cultures(string tfm, string locale)
        {
            string projectAssetsJsonPath = Path.GetTempFileName();
            try
            {
                File.WriteAllText(projectAssetsJsonPath, AssetsFileWithResourceLocale(tfm, locale));
                var task = InitializeTask(out _);
                task.ProjectAssetsFile = projectAssetsJsonPath;
                task.TargetFramework = tfm;
                using var writer = new CacheWriter(task, new MockPackageResolver());
                using var stream = writer.WriteToMemoryStream();
                var engine = (MockBuildEngine)task.BuildEngine;

                engine.Warnings.Where(msg => msg.Code == "NETSDK1187").Should().BeEmpty();
                engine.Messages.Where(msg => msg.Code == "NETSDK1187").Should().BeEmpty();
            }
            finally
            {
                File.Delete(projectAssetsJsonPath);
            }
        }

        private ResolvePackageAssets InitializeTask(out IEnumerable<PropertyInfo> inputProperties)
        {
            inputProperties = typeof(ResolvePackageAssets)
                .GetProperties(BindingFlags.DeclaredOnly | BindingFlags.Instance | BindingFlags.Public)
                .Where(p => !p.IsDefined(typeof(OutputAttribute)) &&
                            p.Name != nameof(ResolvePackageAssets.DesignTimeBuild) &&
                            p.Name != nameof(ResolvePackageAssets.TaskEnvironment))
                .OrderBy(p => p.Name, StringComparer.Ordinal);

            var requiredProperties = inputProperties
                .Where(p => p.IsDefined(typeof(RequiredAttribute)));

            var task = new ResolvePackageAssets();
            // Initialize all required properties as a genuine task invocation would. We do this
            // because HashSettings need not defend against required parameters being null.
            foreach (var property in requiredProperties)
            {
                property.PropertyType.Should().Be(
                    typeof(string),
                    because: $"this test hasn't been updated to handle non-string required task parameters like {property.Name}");

                property.SetValue(task, "_");
            }

            task.BuildEngine = new MockBuildEngine();
            task.TaskEnvironment = TaskEnvironmentHelper.CreateForTest(Directory.GetCurrentDirectory());

            return task;
        }
    }
}
