// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.ApiCompat.IntegrationTests
{
    /// <summary>
    /// Helpers for writing a NuGet.config in a test working directory that includes
    /// the locally-built test packages feed (`SdkTestContext.Current.TestPackages`)
    /// so test projects can <c>dotnet add package Microsoft.DotNet.ApiCompat.Task --prerelease</c>
    /// and resolve to the dev-versioned <c>.nupkg</c> produced by the build.
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
    <clear />
    <add key=""testpackages"" value=""{testPackages}"" />
    <add key=""nuget.org"" value=""https://api.nuget.org/v3/index.json"" protocolVersion=""3"" />
  </packageSources>
</configuration>
";
            File.WriteAllText(configPath, content);
        }
    }
}
