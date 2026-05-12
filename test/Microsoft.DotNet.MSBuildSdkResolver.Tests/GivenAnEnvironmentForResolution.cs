// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Utils.Tests
{
    public class GivenAnEnvironmentForResolution : SdkTest
    {
        public GivenAnEnvironmentForResolution(ITestOutputHelper log) : base(log)
        {
        }

        [Fact]
        public void ItIgnoresInvalidPath()
        {
            Func<string, string> getPathEnvVarFunc = (string var) => { return $"{Directory.GetCurrentDirectory()}Dir{Path.GetInvalidPathChars().First()}Name"; };
            var environmentProvider = new NativeWrapper.EnvironmentProvider(getPathEnvVarFunc);
            var pathResult = environmentProvider.GetCommandPath("nonexistantCommand");
            pathResult.Should().BeNull();
        }

        [Fact]
        public void ItDoesNotReturnNullDotnetRootOnExtraPathSeparator()
        {
            File.Create(Path.Combine(Directory.GetCurrentDirectory(), "dotnet.exe")).Close();
            Func<string, string> getPathEnvVarFunc = (input) => input.Equals("PATH") ? $"fake{Path.PathSeparator}" : string.Empty;
            var result = NativeWrapper.EnvironmentProvider.GetDotnetExeDirectory(getPathEnvVarFunc);
            result.Should().NotBeNullOrWhiteSpace();
        }

        [Fact]
        public void ItDoesNotMistakeDotnetPrefixedProcessForDotnetHost()
        {
            // Use separate directories for the dotnet host and the dotnet-prefixed process
            // to verify the resolver finds dotnet via PATH, not from the process path.
            var dotnetDir = TestAssetsManager.CreateTestDirectory(identifier: "dotnetHost").Path;
            var processDir = TestAssetsManager.CreateTestDirectory(identifier: "processDir").Path;

            var dotnetFileName = "dotnet" + (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? ".exe" : string.Empty);
            File.Create(Path.Combine(dotnetDir, dotnetFileName)).Close();

            // Simulate a dotnet-prefixed process (e.g. dotnet.Tests from xunit v3)
            Func<string, string?> getEnvVar = (input) => input.Equals("PATH") ? dotnetDir : null;
            Func<string?> getProcessPath = () => Path.Combine(processDir, "dotnet.Tests");

            var result = NativeWrapper.EnvironmentProvider.GetDotnetExeDirectory(getEnvVar, getProcessPath);

            // Should resolve via PATH to the real dotnet directory, not the process directory
            result.Should().Be(dotnetDir);
        }
    }
}
