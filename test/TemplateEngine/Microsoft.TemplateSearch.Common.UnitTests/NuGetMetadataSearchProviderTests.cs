// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateSearch.Common.Abstractions;
using Microsoft.TemplateSearch.Common.Providers;
using Newtonsoft.Json.Linq;
using Xunit;

namespace Microsoft.TemplateSearch.Common.UnitTests
{
    public class NuGetMetadataSearchProviderTests : IClassFixture<EnvironmentSettingsHelper>
    {
        private EnvironmentSettingsHelper _environmentSettingsHelper;

        public NuGetMetadataSearchProviderTests(EnvironmentSettingsHelper environmentSettingsHelper)
        {
            _environmentSettingsHelper = environmentSettingsHelper;
        }

        [Fact]
        public async Task SearchOnlineCache()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            const string templateName = "api";

            Func<TemplatePackageSearchData, IReadOnlyList<ITemplateInfo>> filter =
                templatePack => templatePack.Templates
                    .Where(t => ((ITemplateInfo)t).Name.Contains(templateName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var searchCoordinator = new TemplateSearchCoordinator(engineEnvironmentSettings);
            var searchResult = await searchCoordinator.SearchAsync(p => true, filter, default).ConfigureAwait(false);

            Assert.NotNull(searchResult);
            Assert.Equal(1, searchResult.Count);
            Assert.True(searchResult[0].Success);
            Assert.True(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.True(searchResult[0].SearchHits.Count > 0);
        }

        [Fact]
        public async Task SearchOverrideCache()
        {
            string searchFilePath = GenerateLocalCache();
            var environment = new MockEnvironment();
            environment.SetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", searchFilePath);

            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true, environment: environment);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            const string templateName = "foo";

            Func<TemplatePackageSearchData, IReadOnlyList<ITemplateInfo>> filter =
                templatePack => templatePack.Templates
                    .Where(t => ((ITemplateInfo)t).Name.Contains(templateName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var searchCoordinator = new TemplateSearchCoordinator(engineEnvironmentSettings);
            var searchResult = await searchCoordinator.SearchAsync(p => true, filter, default).ConfigureAwait(false);

            Assert.NotNull(searchResult);
            Assert.Equal(1, searchResult.Count);
            Assert.True(searchResult[0].Success);
            Assert.True(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.True(searchResult[0].SearchHits.Count > 0);

            //provider should not copy local file to settings
            Assert.False(engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(engineEnvironmentSettings.Paths.HostVersionSettingsDir, "nugetTemplateSearchInfo.json")));
        }

        [Fact]
        public async Task SearchOverrideCache_FailsWhenFileDoesntExist()
        {
            string searchFilePath = "do-not-exist";
            var environment = new MockEnvironment();
            environment.SetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", searchFilePath);

            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true, environment: environment);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            const string templateName = "foo";

            Func<TemplatePackageSearchData, IReadOnlyList<ITemplateInfo>> filter =
                templatePack => templatePack.Templates
                    .Where(t => ((ITemplateInfo)t).Name.Contains(templateName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var searchCoordinator = new TemplateSearchCoordinator(engineEnvironmentSettings);
            var searchResult = await searchCoordinator.SearchAsync(p => true, filter, default).ConfigureAwait(false);

            Assert.NotNull(searchResult);
            Assert.Equal(1, searchResult.Count);
            Assert.False(searchResult[0].Success);
            Assert.False(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.True(searchResult[0].SearchHits.Count == 0);
            Assert.Equal("Local search cache 'do-not-exist' does not exist.", searchResult[0].ErrorMessage);

            //provider should not copy local file to settings
            Assert.False(engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(engineEnvironmentSettings.Paths.HostVersionSettingsDir, "nugetTemplateSearchInfo.json")));
        }

        [Fact]
        public async Task SearchLocalCache()
        {
            var environment = new MockEnvironment();
            environment.SetEnvironmentVariable("DOTNET_NEW_LOCAL_SEARCH_FILE_ONLY", "true");

            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true, environment: environment);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            const string templateName = "api";

            Func<TemplatePackageSearchData, IReadOnlyList<ITemplateInfo>> filter =
                templatePack => templatePack.Templates
                    .Where(t => ((ITemplateInfo)t).Name.Contains(templateName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var searchCoordinator = new TemplateSearchCoordinator(engineEnvironmentSettings);
            var searchResult = await searchCoordinator.SearchAsync(p => true, filter, default).ConfigureAwait(false);

            Assert.NotNull(searchResult);
            Assert.Equal(1, searchResult.Count);
            Assert.False(searchResult[0].Success);
            Assert.False(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.True(searchResult[0].SearchHits.Count == 0);
            Assert.Equal($"Local search cache '{Path.Combine(engineEnvironmentSettings.Paths.HostVersionSettingsDir, "nugetTemplateSearchInfo.json")}' does not exist.", searchResult[0].ErrorMessage);

            environment.SetEnvironmentVariable("DOTNET_NEW_LOCAL_SEARCH_FILE_ONLY", null);
            searchResult = await searchCoordinator.SearchAsync(p => true, filter, default).ConfigureAwait(false);

            Assert.NotNull(searchResult);
            Assert.Equal(1, searchResult.Count);
            Assert.True(searchResult[0].Success);
            Assert.True(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.True(searchResult[0].SearchHits.Count > 0);

            environment.SetEnvironmentVariable("DOTNET_NEW_LOCAL_SEARCH_FILE_ONLY", "true");
            searchResult = await searchCoordinator.SearchAsync(p => true, filter, default).ConfigureAwait(false);

            Assert.NotNull(searchResult);
            Assert.Equal(1, searchResult.Count);
            Assert.True(searchResult[0].Success);
            Assert.True(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.True(searchResult[0].SearchHits.Count > 0);
        }

        [Fact]
        public async Task SearchReturnsErrorOnIncorrectCache()
        {
            var jsonObject = JObject.FromObject(new { randomField = "smth" });
            string searchFilePath = Path.Combine(TestUtils.CreateTemporaryFolder(), "searchCacheV2.json");
            File.WriteAllText(searchFilePath, jsonObject.ToString());

            var environment = new MockEnvironment();
            environment.SetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", searchFilePath);

            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true, environment: environment);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            const string templateName = "foo";

            Func<TemplatePackageSearchData, IReadOnlyList<ITemplateInfo>> filter =
                templatePack => templatePack.Templates
                    .Where(t => ((ITemplateInfo)t).Name.Contains(templateName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var searchCoordinator = new TemplateSearchCoordinator(engineEnvironmentSettings);
            var searchResult = await searchCoordinator.SearchAsync(p => true, filter, default).ConfigureAwait(false);

            Assert.NotNull(searchResult);
            Assert.Equal(1, searchResult.Count);
            Assert.False(searchResult[0].Success);
            Assert.False(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.Equal("The template search cache data is not supported.", searchResult[0].ErrorMessage);
            Assert.True(searchResult[0].SearchHits.Count == 0);

            //provider should not copy local file to settings
            Assert.False(engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(engineEnvironmentSettings.Paths.HostVersionSettingsDir, "nugetTemplateSearchInfo.json")));
        }

        [Fact]
        public async Task SearchReturnsErrorOnIncorrectV1Cache()
        {
            var jsonObject = JObject.FromObject(new { version = "1.0.0.0", randomField = "smth" });
            string searchFilePath = Path.Combine(TestUtils.CreateTemporaryFolder(), "searchCache.json");
            File.WriteAllText(searchFilePath, jsonObject.ToString());

            var environment = new MockEnvironment();
            environment.SetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", searchFilePath);

            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true, environment: environment);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            const string templateName = "foo";

            Func<TemplatePackageSearchData, IReadOnlyList<ITemplateInfo>> filter =
                templatePack => templatePack.Templates
                    .Where(t => ((ITemplateInfo)t).Name.Contains(templateName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var searchCoordinator = new TemplateSearchCoordinator(engineEnvironmentSettings);
            var searchResult = await searchCoordinator.SearchAsync(p => true, filter, default).ConfigureAwait(false);

            Assert.NotNull(searchResult);
            Assert.Equal(1, searchResult.Count);
            Assert.False(searchResult[0].Success);
            Assert.False(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.True(searchResult[0].SearchHits.Count == 0);
            Assert.Equal("The template search cache data is not valid.", searchResult[0].ErrorMessage);

            //provider should not copy local file to settings
            Assert.False(engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(engineEnvironmentSettings.Paths.HostVersionSettingsDir, "nugetTemplateSearchInfo.json")));
        }

        [Fact]
        public async Task SearchReturnsErrorOnIncorrectV2Cache()
        {
            var jsonObject = JObject.FromObject(new { version = "2.0", randomField = "smth" });
            string searchFilePath = Path.Combine(TestUtils.CreateTemporaryFolder(), "searchCache.json");
            File.WriteAllText(searchFilePath, jsonObject.ToString());

            var environment = new MockEnvironment();
            environment.SetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", searchFilePath);

            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true, environment: environment);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            const string templateName = "foo";

            Func<TemplatePackageSearchData, IReadOnlyList<ITemplateInfo>> filter =
                templatePack => templatePack.Templates
                    .Where(t => ((ITemplateInfo)t).Name.Contains(templateName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var searchCoordinator = new TemplateSearchCoordinator(engineEnvironmentSettings);
            var searchResult = await searchCoordinator.SearchAsync(p => true, filter, default).ConfigureAwait(false);

            Assert.NotNull(searchResult);
            Assert.Equal(1, searchResult.Count);
            Assert.False(searchResult[0].Success);
            Assert.False(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.True(searchResult[0].SearchHits.Count == 0);
            Assert.Equal("The template search cache data is not valid.", searchResult[0].ErrorMessage);

            //provider should not copy local file to settings
            Assert.False(engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(engineEnvironmentSettings.Paths.HostVersionSettingsDir, "nugetTemplateSearchInfo.json")));
        }

        [Fact]
        public async Task SearchReturnsErrorOnIncorrectVersionCache()
        {
            var jsonObject = JObject.FromObject(new { version = "3.0", TemplatePackages = Array.Empty<string>() });
            string searchFilePath = Path.Combine(TestUtils.CreateTemporaryFolder(), "searchCache.json");
            File.WriteAllText(searchFilePath, jsonObject.ToString());

            var environment = new MockEnvironment();
            environment.SetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", searchFilePath);

            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: this.GetType().Name, virtualize: true, environment: environment);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            const string templateName = "foo";

            Func<TemplatePackageSearchData, IReadOnlyList<ITemplateInfo>> filter =
                templatePack => templatePack.Templates
                    .Where(t => ((ITemplateInfo)t).Name.Contains(templateName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var searchCoordinator = new TemplateSearchCoordinator(engineEnvironmentSettings);
            var searchResult = await searchCoordinator.SearchAsync(p => true, filter, default).ConfigureAwait(false);

            Assert.NotNull(searchResult);
            Assert.Equal(1, searchResult.Count);
            Assert.False(searchResult[0].Success);
            Assert.False(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.True(searchResult[0].SearchHits.Count == 0);
            Assert.Equal("The template search cache data is not supported.", searchResult[0].ErrorMessage);

            //provider should not copy local file to settings
            Assert.False(engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(engineEnvironmentSettings.Paths.HostVersionSettingsDir, "nugetTemplateSearchInfo.json")));
        }

        private string GenerateLocalCache()
        {
            ITemplatePackageInfo packOneInfo = new MockTemplatePackageInfo("PackOne", "1.0.0");
            ITemplatePackageInfo packTwoInfo = new MockTemplatePackageInfo("PackTwo", "1.6.0");
            ITemplatePackageInfo packThreeInfo = new MockTemplatePackageInfo("PackThree", "2.1");

            ITemplateInfo fooOneTemplate =
                new MockTemplateInfo("foo1", name: "MockFooTemplateOne", identity: "Mock.Foo.1", groupIdentity: "Mock.Foo", author: "TestAuthor")
                    .WithClassifications("CSharp", "Library")
                    .WithDescription("Mock Foo template one")
                    .WithChoiceParameter("Framework", "netcoreapp3.0", "netcoreapp3.1")
                    .WithTag("language", "C#")
                    .WithTag("type", "project");

            ITemplateInfo fooTwoTemplate =
                new MockTemplateInfo("foo2", name: "MockFooTemplateTwo", identity: "Mock.Foo.2", groupIdentity: "Mock.Foo")
                    .WithClassifications("CSharp", "Console")
                    .WithDescription("Mock Foo template two")
                    .WithChoiceParameter("Framework", "netcoreapp2.0", "netcoreapp2.1", "netcoreapp3.1")
                    .WithTag("language", "C#");

            ITemplateInfo barCSharpTemplate =
                new MockTemplateInfo("barC", name: "MockBarCsharpTemplate", identity: "Mock.Bar.1.Csharp", groupIdentity: "Mock.Bar")
                    .WithClassifications("CSharp")
                    .WithDescription("Mock Bar CSharp template")
                    .WithTag("language", "C#");

            ITemplateInfo barFSharpTemplate =
                new MockTemplateInfo("barF", name: "MockBarFSharpTemplate", identity: "Mock.Bar.1.FSharp", groupIdentity: "Mock.Bar")
                    .WithClassifications("FSharp")
                    .WithDescription("Mock Bar FSharp template")
                    .WithTag("language", "F#");

            var fooOneTemplateData = new TemplateSearchData(fooOneTemplate);
            var fooTwoTemplateData = new TemplateSearchData(fooTwoTemplate);
            var barCSharpTemplateData = new TemplateSearchData(barCSharpTemplate);
            var barFSharpTemplateData = new TemplateSearchData(barFSharpTemplate);

            var packOne = new TemplatePackageSearchData(packOneInfo, new[] { fooOneTemplateData });
            var packTwo = new TemplatePackageSearchData(packTwoInfo, new[] { fooTwoTemplateData });
            var packThree = new TemplatePackageSearchData(packThreeInfo, new[] { barCSharpTemplateData, barFSharpTemplateData });

            var cache = new TemplateSearchCache(new[] { packOne, packTwo, packThree });

            string targetPath = Path.Combine(TestUtils.CreateTemporaryFolder(), "searchCacheV2.json");
            File.WriteAllText(targetPath, cache.ToJObject().ToString());
            return targetPath;
        }

        private class MockEnvironment : IEnvironment
        {
            private readonly Dictionary<string, string> _envVars = new Dictionary<string, string>();
            private readonly IReadOnlyList<string> _fallbackVars = new[] { "USERPROFILE", "HOME" };

            public string NewLine => Environment.NewLine;

            public int ConsoleBufferWidth => 100;

            public void SetEnvironmentVariable(string name, string? value)
            {
                if (value != null)
                {
                    _envVars[name] = value;
                }
                else
                {
                    _envVars.Remove(name);
                }    
            }

            //not supported as the mock, but not needed.
            public string ExpandEnvironmentVariables(string name) => Environment.ExpandEnvironmentVariables(name);

            public string? GetEnvironmentVariable(string name)
            {
                if (_fallbackVars.Contains(name))
                {
                    return Environment.GetEnvironmentVariable(name);
                }

                if (_envVars.TryGetValue(name, out string? value))
                {
                    return value;
                }
                return null;
            }

            public IReadOnlyDictionary<string, string> GetEnvironmentVariables()
            {
                return _envVars;
            }
        }
    }
}
