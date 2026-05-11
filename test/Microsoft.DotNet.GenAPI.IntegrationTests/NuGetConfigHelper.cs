// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.GenAPI.IntegrationTests
{
    /// <summary>
    /// Writes a NuGet.config in a test working directory that adds
    /// <see cref="SdkTestContext.TestPackages"/> as a feed so a test asset can
    /// <c>dotnet add package Microsoft.DotNet.GenAPI.Task --prerelease</c> and resolve to the
    /// dev-versioned <c>.nupkg</c> produced by the build. All other sources (nuget.org,
    /// etc.) are inherited from the surrounding environment's NuGet.config.
    /// </summary>
    internal static class NuGetConfigHelper
    {
        public static void WriteNuGetConfigWithTestPackages(string directory)
        {
            string configPath = Path.Combine(directory, "NuGet.Config");
            string testPackages = SdkTestContext.Current.TestPackages;
            string content = $@"<?xml version=""1.0"" encoding=""utf-8""?>
<configuration>
  <packageSources>
    <add key=""testpackages"" value=""{testPackages}"" />
  </packageSources>
</configuration>
";
            File.WriteAllText(configPath, content);
        }
    }
}
