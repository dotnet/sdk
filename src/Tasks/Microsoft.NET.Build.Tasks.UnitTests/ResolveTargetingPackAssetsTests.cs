using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using FluentAssertions;
using Xunit;

namespace Microsoft.NET.Build.Tasks.UnitTests
{
    public class ResolveTargetingPackAssetsTests
    {
        [Fact]
        public void Given_ResolvedTargetingPacks_with_TargetingPackCombinedAndEmbedRuntime_It_resolves_TargetingPack()
        {
            string mockPackageDirectory = Path.Combine(Path.GetTempPath(), "dotnetSdkTests", Path.GetRandomFileName());

            string dataDir = Path.Combine(mockPackageDirectory, "data");
            Directory.CreateDirectory(dataDir);

            File.WriteAllText(Path.Combine(dataDir, "FrameworkList.xml"), _frameworkList);

            var task = new ResolveTargetingPackAssets();

            task.FrameworkReferences = new[]
            {
                new MockTaskItem("Microsoft.Windows.Ref", new Dictionary<string, string>())
            };

            task.ResolvedTargetingPacks = new[]
            {
                new MockTaskItem("Microsoft.Windows.Ref",
                    new Dictionary<string, string>()
                    {
                        {MetadataKeys.NuGetPackageId, "Microsoft.Windows.Ref"},
                        {MetadataKeys.NuGetPackageVersion, "5.0.0-preview1"},
                        {MetadataKeys.PackageConflictPreferredPackages, "Microsoft.Windows.Ref;"},
                        {MetadataKeys.PackageDirectory, mockPackageDirectory},
                        {MetadataKeys.Path, mockPackageDirectory},
                        {"TargetFramework", "net5.0"},
                        {MetadataKeys.TargetingPackFormat, MetadataKeys.TargetingPackCombinedAndEmbedRuntime},
                    })
            };

            task.Execute().Should().BeTrue();

            task.ReferencesToAdd[0].ItemSpec.Should().Be(Path.Combine(mockPackageDirectory, "lib/Microsoft.Windows.SDK.NET.dll"));
            task.PlatformManifests.Should().Contain(Path.Combine(mockPackageDirectory, "data\\PlatformManifest.txt"));
        }

        private readonly string _frameworkList =
            "<FileList Name=\"cswinrt .NET Core 5.0\">	<File Type=\"Managed\" Path=\"lib/Microsoft.Windows.SDK.NET.dll\" PublicKeyToken=\"null\" AssemblyName=\"Microsoft.Windows.SDK.NET\" AssemblyVersion=\"10.0.18362.3\" FileVersion=\"10.0.18362.3\" /></FileList>";
    }
}
