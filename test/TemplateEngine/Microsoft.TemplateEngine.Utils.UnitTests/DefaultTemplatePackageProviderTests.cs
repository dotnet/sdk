// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;
using Microsoft.TemplateEngine.TestHelper;
using Xunit;

namespace Microsoft.TemplateEngine.Utils.UnitTests
{
    public class DefaultTemplatePackageProviderTests : TestBase
    {
        [Fact]
        public async Task ReturnsFoldersAndNuPkgs()
        {
            var thisDir = Path.GetDirectoryName(typeof(DefaultTemplatePackageProviderTests).Assembly.Location);
            //Pass in 5 folders
            var folders = Directory.GetDirectories(Path.Combine(thisDir, "..", "..", "..", "..", "..", "test", "Microsoft.TemplateEngine.TestTemplates", "test_templates")).Take(5);
            //And one *.nupkg, but that folder contains 2 .nupkg files
            var nupkgs = new[] { Path.Combine(thisDir, "..", "..", "..", "..", "..", "test", "Microsoft.TemplateEngine.TestTemplates", "nupkg_templates", "*.nupkg") };

            var provider = new DefaultTemplatePackageProvider(null, EngineEnvironmentSettings, nupkgs, folders);
            var sources = await provider.GetAllTemplatePackagesAsync(default).ConfigureAwait(false);

            //Total should be 7
            Assert.Equal(7, sources.Count);

            Assert.True(sources[0].LastChangeTime > new DateTime(2000, 1, 1));
            Assert.False(string.IsNullOrWhiteSpace(sources[0].MountPointUri));
            Assert.Equal(provider, sources[0].Provider);
        }
    }
}
