// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateEngine.TestHelper;
using Microsoft.TemplateSearch.Common.Abstractions;
using Microsoft.TemplateSearch.Common.Providers;

namespace Microsoft.TemplateSearch.Common.UnitTests
{
    [TestClass]
    public class NuGetMetadataSearchProviderTests
    {
        private static readonly Lazy<EnvironmentSettingsHelper> s_environmentSettingsHelper =
            new(() => new EnvironmentSettingsHelper(NullMessageSink.Instance));

        private readonly EnvironmentSettingsHelper _environmentSettingsHelper;

        public NuGetMetadataSearchProviderTests()
        {
            _environmentSettingsHelper = s_environmentSettingsHelper.Value;
        }

        public TestContext TestContext { get; set; } = null!;

        [ClassCleanup]
        public static void ClassCleanup()
        {
            if (s_environmentSettingsHelper.IsValueCreated)
            {
                s_environmentSettingsHelper.Value.Dispose();
            }
        }

        [TestMethod]
        public async Task SearchOnlineCache()
        {
            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            const string templateName = "api";

            static IReadOnlyList<ITemplateInfo> Filter(TemplatePackageSearchData templatePack) => templatePack.Templates
                    .Where(t => ((ITemplateInfo)t).Name.Contains(templateName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var searchCoordinator = new TemplateSearchCoordinator(engineEnvironmentSettings);
            async Task<IReadOnlyList<SearchResult>> Search()
            {
                var result = await searchCoordinator.SearchAsync(p => true, Filter, default);
                if (result != null && result.Count > 0 && !string.IsNullOrWhiteSpace(result[0].ErrorMessage))
                {
                    var errorMessage = result[0].ErrorMessage;
                    if (errorMessage!.Contains("The SSL connection could not be established"))
                    {
                        throw new HttpRequestException(errorMessage);
                    }
                }
                return result!;
            }

            var searchResult = await TestUtils.AttemptSearch<IReadOnlyList<SearchResult>, HttpRequestException>(3, TimeSpan.FromSeconds(10), Search);

            Assert.IsNotNull(searchResult);
            Assert.ContainsSingle(searchResult);
            Assert.IsTrue(searchResult[0].Success);
            Assert.IsTrue(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.IsNotEmpty(searchResult[0].SearchHits);
        }

        [TestMethod]
        public async Task SearchOverrideCache()
        {
            string searchFilePath = GenerateLocalCache();
            var environment = new MockEnvironment();
            environment.SetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", searchFilePath);

            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true, environment: environment);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            const string templateName = "foo";

            static IReadOnlyList<ITemplateInfo> Filter(TemplatePackageSearchData templatePack) => templatePack.Templates
                    .Where(t => ((ITemplateInfo)t).Name.Contains(templateName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var searchCoordinator = new TemplateSearchCoordinator(engineEnvironmentSettings);
            var searchResult = await searchCoordinator.SearchAsync(p => true, Filter, TestContext.CancellationToken);

            Assert.IsNotNull(searchResult);
            Assert.ContainsSingle(searchResult);
            Assert.IsTrue(searchResult[0].Success);
            Assert.IsTrue(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.IsNotEmpty(searchResult[0].SearchHits);

            //provider should not copy local file to settings
            Assert.IsFalse(engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(engineEnvironmentSettings.Paths.HostVersionSettingsDir, "nugetTemplateSearchInfo.json")));
        }

        [TestMethod]
        public async Task SearchOverrideCache_FailsWhenFileDoesntExist()
        {
            string searchFilePath = "do-not-exist";
            var environment = new MockEnvironment();
            environment.SetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", searchFilePath);

            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true, environment: environment);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            const string templateName = "foo";

            static IReadOnlyList<ITemplateInfo> Filter(TemplatePackageSearchData templatePack) => templatePack.Templates
                    .Where(t => ((ITemplateInfo)t).Name.Contains(templateName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var searchCoordinator = new TemplateSearchCoordinator(engineEnvironmentSettings);
            var searchResult = await searchCoordinator.SearchAsync(p => true, Filter, TestContext.CancellationToken);

            Assert.IsNotNull(searchResult);
            Assert.ContainsSingle(searchResult);
            Assert.IsFalse(searchResult[0].Success);
            Assert.IsFalse(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.IsEmpty(searchResult[0].SearchHits);
            Assert.AreEqual("Local search cache 'do-not-exist' does not exist.", searchResult[0].ErrorMessage);

            //provider should not copy local file to settings
            Assert.IsFalse(engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(engineEnvironmentSettings.Paths.HostVersionSettingsDir, "nugetTemplateSearchInfo.json")));
        }

        [TestMethod]
        public async Task SearchLocalCache()
        {
            var environment = new MockEnvironment();
            environment.SetEnvironmentVariable("DOTNET_NEW_LOCAL_SEARCH_FILE_ONLY", "true");

            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true, environment: environment);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            const string templateName = "api";

            IReadOnlyList<ITemplateInfo> Filter(TemplatePackageSearchData templatePack) => templatePack.Templates
                    .Where(t => ((ITemplateInfo)t).Name.Contains(templateName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var searchCoordinator = new TemplateSearchCoordinator(engineEnvironmentSettings);
            async Task<IReadOnlyList<SearchResult>> Search()
            {
                var result = await searchCoordinator.SearchAsync(p => true, Filter, default);
                if (result != null && result.Count > 0 && !string.IsNullOrWhiteSpace(result[0].ErrorMessage))
                {
                    var errorMessage = result[0].ErrorMessage;
                    if (errorMessage!.Contains("The SSL connection could not be established"))
                    {
                        throw new HttpRequestException(errorMessage);
                    }
                }
                return result!;
            }
            var searchResult = await TestUtils.AttemptSearch<IReadOnlyList<SearchResult>, HttpRequestException>(3, TimeSpan.FromSeconds(10), Search);

            Assert.IsNotNull(searchResult);
            Assert.ContainsSingle(searchResult);
            Assert.IsFalse(searchResult[0].Success);
            Assert.IsFalse(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.IsEmpty(searchResult[0].SearchHits);
            Assert.AreEqual($"Local search cache '{Path.Combine(engineEnvironmentSettings.Paths.HostVersionSettingsDir, "nugetTemplateSearchInfo.json")}' does not exist.", searchResult[0].ErrorMessage);

            environment.SetEnvironmentVariable("DOTNET_NEW_LOCAL_SEARCH_FILE_ONLY", null);
            searchResult = await TestUtils.AttemptSearch<IReadOnlyList<SearchResult>, HttpRequestException>(3, TimeSpan.FromSeconds(10), Search);

            Assert.IsNotNull(searchResult);
            Assert.ContainsSingle(searchResult);
            Assert.IsTrue(searchResult[0].Success);
            Assert.IsTrue(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.IsNotEmpty(searchResult[0].SearchHits);

            environment.SetEnvironmentVariable("DOTNET_NEW_LOCAL_SEARCH_FILE_ONLY", "true");
            searchResult = await TestUtils.AttemptSearch<IReadOnlyList<SearchResult>, HttpRequestException>(3, TimeSpan.FromSeconds(10), Search);

            Assert.IsNotNull(searchResult);
            Assert.ContainsSingle(searchResult);
            Assert.IsTrue(searchResult[0].Success);
            Assert.IsTrue(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.IsNotEmpty(searchResult[0].SearchHits);
        }

        [TestMethod]
        public async Task SearchReturnsErrorOnIncorrectCache()
        {
            var jsonObject = JsonNode.Parse(JsonSerializer.Serialize(new { randomField = "smth" }))!;
            string searchFilePath = Path.Combine(TestUtils.CreateTemporaryFolder(), "searchCacheV2.json");
            File.WriteAllText(searchFilePath, jsonObject.ToString());

            var environment = new MockEnvironment();
            environment.SetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", searchFilePath);

            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true, environment: environment);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            const string templateName = "foo";

            static IReadOnlyList<ITemplateInfo> Filter(TemplatePackageSearchData templatePack) => templatePack.Templates
                    .Where(t => ((ITemplateInfo)t).Name.Contains(templateName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var searchCoordinator = new TemplateSearchCoordinator(engineEnvironmentSettings);
            var searchResult = await searchCoordinator.SearchAsync(p => true, Filter, TestContext.CancellationToken);

            Assert.IsNotNull(searchResult);
            Assert.ContainsSingle(searchResult);
            Assert.IsFalse(searchResult[0].Success);
            Assert.IsFalse(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.AreEqual("The template search cache data is not supported.", searchResult[0].ErrorMessage);
            Assert.IsEmpty(searchResult[0].SearchHits);

            //provider should not copy local file to settings
            Assert.IsFalse(engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(engineEnvironmentSettings.Paths.HostVersionSettingsDir, "nugetTemplateSearchInfo.json")));
        }

        [TestMethod]
        public async Task SearchReturnsErrorOnIncorrectV1Cache()
        {
            var jsonObject = JsonNode.Parse(JsonSerializer.Serialize(new { version = "1.0.0.0", randomField = "smth" }))!;
            string searchFilePath = Path.Combine(TestUtils.CreateTemporaryFolder(), "searchCache.json");
            File.WriteAllText(searchFilePath, jsonObject.ToString());

            var environment = new MockEnvironment();
            environment.SetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", searchFilePath);

            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true, environment: environment);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            const string templateName = "foo";

            static IReadOnlyList<ITemplateInfo> Filter(TemplatePackageSearchData templatePack) => templatePack.Templates
                    .Where(t => ((ITemplateInfo)t).Name.Contains(templateName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var searchCoordinator = new TemplateSearchCoordinator(engineEnvironmentSettings);
            var searchResult = await searchCoordinator.SearchAsync(p => true, Filter, TestContext.CancellationToken);

            Assert.IsNotNull(searchResult);
            Assert.ContainsSingle(searchResult);
            Assert.IsFalse(searchResult[0].Success);
            Assert.IsFalse(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.IsEmpty(searchResult[0].SearchHits);
            Assert.AreEqual("The template search cache data is not valid.", searchResult[0].ErrorMessage);

            //provider should not copy local file to settings
            Assert.IsFalse(engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(engineEnvironmentSettings.Paths.HostVersionSettingsDir, "nugetTemplateSearchInfo.json")));
        }

        [TestMethod]
        public async Task SearchReturnsErrorOnIncorrectV2Cache()
        {
            var jsonObject = JsonNode.Parse(JsonSerializer.Serialize(new { version = "2.0", randomField = "smth" }))!;
            string searchFilePath = Path.Combine(TestUtils.CreateTemporaryFolder(), "searchCache.json");
            File.WriteAllText(searchFilePath, jsonObject.ToString());

            var environment = new MockEnvironment();
            environment.SetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", searchFilePath);

            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true, environment: environment);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            const string templateName = "foo";

            static IReadOnlyList<ITemplateInfo> Filter(TemplatePackageSearchData templatePack) => templatePack.Templates
                    .Where(t => ((ITemplateInfo)t).Name.Contains(templateName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var searchCoordinator = new TemplateSearchCoordinator(engineEnvironmentSettings);
            var searchResult = await searchCoordinator.SearchAsync(p => true, Filter, TestContext.CancellationToken);

            Assert.IsNotNull(searchResult);
            Assert.ContainsSingle(searchResult);
            Assert.IsFalse(searchResult[0].Success);
            Assert.IsFalse(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.IsEmpty(searchResult[0].SearchHits);
            Assert.AreEqual("The template search cache data is not valid.", searchResult[0].ErrorMessage);

            //provider should not copy local file to settings
            Assert.IsFalse(engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(engineEnvironmentSettings.Paths.HostVersionSettingsDir, "nugetTemplateSearchInfo.json")));
        }

        [TestMethod]
        public async Task SearchReturnsErrorOnIncorrectVersionCache()
        {
            var jsonObject = JsonNode.Parse(JsonSerializer.Serialize(new { version = "3.0", TemplatePackages = Array.Empty<string>() }))!;
            string searchFilePath = Path.Combine(TestUtils.CreateTemporaryFolder(), "searchCache.json");
            File.WriteAllText(searchFilePath, jsonObject.ToString());

            var environment = new MockEnvironment();
            environment.SetEnvironmentVariable("DOTNET_NEW_SEARCH_FILE_OVERRIDE", searchFilePath);

            IEngineEnvironmentSettings engineEnvironmentSettings = _environmentSettingsHelper.CreateEnvironment(hostIdentifier: GetType().Name, virtualize: true, environment: environment);
            engineEnvironmentSettings.Components.AddComponent(typeof(ITemplateSearchProviderFactory), new NuGetMetadataSearchProviderFactory());

            const string templateName = "foo";

            static IReadOnlyList<ITemplateInfo> Filter(TemplatePackageSearchData templatePack) => templatePack.Templates
                    .Where(t => ((ITemplateInfo)t).Name.Contains(templateName, StringComparison.OrdinalIgnoreCase))
                    .ToList();

            var searchCoordinator = new TemplateSearchCoordinator(engineEnvironmentSettings);
            var searchResult = await searchCoordinator.SearchAsync(p => true, Filter, TestContext.CancellationToken);

            Assert.IsNotNull(searchResult);
            Assert.ContainsSingle(searchResult);
            Assert.IsFalse(searchResult[0].Success);
            Assert.IsFalse(string.IsNullOrWhiteSpace(searchResult[0].ErrorMessage));
            Assert.IsEmpty(searchResult[0].SearchHits);
            Assert.AreEqual("The template search cache data is not supported.", searchResult[0].ErrorMessage);

            //provider should not copy local file to settings
            Assert.IsFalse(engineEnvironmentSettings.Host.FileSystem.FileExists(Path.Combine(engineEnvironmentSettings.Paths.HostVersionSettingsDir, "nugetTemplateSearchInfo.json")));
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
