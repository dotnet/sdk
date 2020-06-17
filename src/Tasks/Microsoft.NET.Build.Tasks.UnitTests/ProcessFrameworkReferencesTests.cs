using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class ProcessFrameworkReferencesTests
    {
        [Fact]
        public void It_resolves_FrameworkReferences()
        {
            var task = new ProcessFrameworkReferences();

            task.EnableTargetingPackDownload = true;
            task.TargetFrameworkIdentifier = ".NETCoreApp";
            task.TargetFrameworkVersion = "3.0";
            task.FrameworkReferences = new[]
            {
                new MockTaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>())
            };

            task.KnownFrameworkReferences = new[]
            {
                new MockTaskItem("Microsoft.AspNetCore.App",
                    new Dictionary<string, string>()
                    {
                        { "TargetFramework", "netcoreapp3.0" },
                        { "RuntimeFrameworkName", "Microsoft.AspNetCore.App" },
                        { "DefaultRuntimeFrameworkVersion", "1.9.5" },
                        { "LatestRuntimeFrameworkVersion", "1.9.6" },
                        { "TargetingPackName", "Microsoft.AspNetCore.App" },
                        { "TargetingPackVersion", "1.9.0" }
                    })
            };

            task.Execute().Should().BeTrue();

            task.PackagesToDownload.Length.Should().Be(1);

            task.RuntimeFrameworks.Length.Should().Be(1);
            task.RuntimeFrameworks[0].ItemSpec.Should().Be("Microsoft.AspNetCore.App");
            task.RuntimeFrameworks[0].GetMetadata(MetadataKeys.Version).Should().Be("1.9.5");
        }

        [Fact]
        public void It_does_not_resolve_FrameworkReferences_if_targetframework_doesnt_match()
        {
            var task = new ProcessFrameworkReferences();

            task.TargetFrameworkIdentifier = ".NETCoreApp";
            task.TargetFrameworkVersion = "2.0";
            task.FrameworkReferences = new[]
            {
                new MockTaskItem("Microsoft.AspNetCore.App", new Dictionary<string, string>())
            };

            task.KnownFrameworkReferences = new[]
            {
                new MockTaskItem("Microsoft.AspNetCore.App",
                    new Dictionary<string, string>()
                    {
                        { "TargetFramework", "netcoreapp3.0" },
                        { "RuntimeFrameworkName", "Microsoft.AspNetCore.App" },
                        { "DefaultRuntimeFrameworkVersion", "1.9.5" },
                        { "LatestRuntimeFrameworkVersion", "1.9.6" },
                        { "TargetingPackName", "Microsoft.AspNetCore.App" },
                        { "TargetingPackVersion", "1.9.0" }
                    })
            };

            task.Execute().Should().BeTrue();

            task.PackagesToDownload.Should().BeNull();
            task.RuntimeFrameworks.Should().BeNull();
        }

        [Fact]
        public void AppleSource()
        {
            var task = new ProcessFrameworkReferences();

            task.EnableTargetingPackDownload = true;
            task.TargetFrameworkIdentifier = ".NETCoreApp";
            task.TargetFrameworkVersion = "5.0";
            task.FrameworkReferences = new[]
            {
                new MockTaskItem("Microsoft.Windows.Ref", new Dictionary<string, string>())
            };

            task.KnownFrameworkReferences = new[]
            {
                new MockTaskItem("Microsoft.Windows.Ref",
                    new Dictionary<string, string>()
                    {
                        { "TargetFramework", "netcoreapp5.0" },
                        { "DefaultRuntimeFrameworkVersion", "5.0.0-preview1" },
                        { "LatestRuntimeFrameworkVersion", "5.0.0-preview1" },
                        { "TargetingPackCombinedAndEmbedRuntime", "true" },
                    })
            };

            task.Execute().Should().BeTrue();

            task.PackagesToDownload.Length.Should().Be(1);

            task.RuntimeFrameworks.Length.Should().Be(1);
            task.RuntimeFrameworks[0].ItemSpec.Should().Be("Microsoft.Windows.Ref");
            task.RuntimeFrameworks[0].GetMetadata(MetadataKeys.Version).Should().Be("5.0.0-preview1");
            
            // also assert targeting pack
        }
    }
}
