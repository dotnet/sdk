// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Microsoft.DotNet.Cli.Resources.Generator.Tests;

[TestClass]
[DoNotParallelize]
public class GeneratedResourceRuntimeTests
{
    private const string SimpleResource = """
        <root>
          <data name="Greeting" xml:space="preserve">
            <value>Source fallback</value>
          </data>
        </root>
        """;

    [TestInitialize]
    public void ResetProviderBeforeTest() => ResetProvider();

    [TestCleanup]
    public void ResetProviderAfterTest() => ResetProvider();

    [TestMethod]
    public void ResourceManager_LocalizedResourceWithoutRegistration_UsesRuntimeSatellites()
    {
        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource),
            GeneratorTestResource.Sibling(SimpleResource, "fr"));
        using GeneratedAssembly generatedAssembly = result.Emit(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Greeting"] = "Runtime value"
            });

        object? manager = GetRequiredProperty(
            generatedAssembly.GetGeneratedType(),
            "ResourceManager").GetValue(null);

        manager.Should().BeOfType<SatelliteStringResourceManager>();
    }

    [TestMethod]
    public async Task ResourceManager_RegisteredProvider_InvokesProviderOnce()
    {
        int invocationCount = 0;
        string? receivedBaseName = null;
        Assembly? receivedAssembly = null;
        StringResourceManagerProvider.Register(
            (baseName, ownerAssembly) =>
            {
                Interlocked.Increment(ref invocationCount);
                receivedBaseName = baseName;
                receivedAssembly = ownerAssembly;
                return new StringResourceManager(baseName, ownerAssembly);
            });
        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource),
            GeneratorTestResource.Sibling(SimpleResource, "fr"));
        using GeneratedAssembly generatedAssembly = result.Emit(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Greeting"] = "Runtime value"
            });
        PropertyInfo resourceManager = GetRequiredProperty(
            generatedAssembly.GetGeneratedType(),
            "ResourceManager");

        Task<object?>[] reads =
        [
            .. Enumerable.Range(0, 16)
                .Select(_ => Task.Run(() => resourceManager.GetValue(null)))
        ];
        object?[] managers = await Task.WhenAll(reads).ConfigureAwait(false);

        invocationCount.Should().Be(1);
        managers.Should().OnlyContain(manager => ReferenceEquals(manager, managers[0]));
        managers[0].Should().BeOfType<StringResourceManager>();
        receivedBaseName.Should().Be("Test.Resources.Strings");
        receivedAssembly.Should().BeSameAs(generatedAssembly.Assembly);
    }

    [TestMethod]
    public void GeneratedProperty_RepeatRead_CachesValue()
    {
        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource));
        using GeneratedAssembly generatedAssembly = result.Emit(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Greeting"] = "Runtime value"
            });
        Type resourceType = generatedAssembly.GetGeneratedType();
        PropertyInfo greeting = GetRequiredProperty(resourceType, "Greeting");
        StringResourceManager manager = (StringResourceManager)GetRequiredProperty(
            resourceType,
            "ResourceManager").GetValue(null)!;

        string? firstUncached = manager.GetString("Greeting");
        string? secondUncached = manager.GetString("Greeting");

        firstUncached.Should().Be("Runtime value");
        secondUncached.Should().Be(firstUncached).And.NotBeSameAs(firstUncached);

        object? first = greeting.GetValue(null);
        object? second = greeting.GetValue(null);

        first.Should().Be("Runtime value");
        second.Should().BeSameAs(first);
    }

    [TestMethod]
    public void GeneratedProperty_FailedTableLoad_RetriesWithoutRecreatingManager()
    {
        int managerCount = 0;
        int loadCount = 0;
        StringResourceManagerProvider.Register(
            (baseName, ownerAssembly) =>
            {
                managerCount++;
                return new StringResourceManager(
                    baseName,
                    () =>
                    {
                        if (++loadCount == 1)
                        {
                            throw new IOException("Transient resource read failure.");
                        }

                        return ownerAssembly.GetManifestResourceStream(baseName + ".resources")
                            ?? throw new MissingManifestResourceException(baseName);
                    });
            });
        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource),
            GeneratorTestResource.Sibling(SimpleResource, "fr"));
        using GeneratedAssembly generatedAssembly = result.Emit(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Greeting"] = "Runtime value"
            });
        PropertyInfo greeting = GetRequiredProperty(generatedAssembly.GetGeneratedType(), "Greeting");

        Action firstRead = () => greeting.GetValue(null);
        firstRead.Should().Throw<TargetInvocationException>()
            .Which.InnerException.Should().BeOfType<IOException>();

        object? value = greeting.GetValue(null);

        value.Should().Be("Runtime value");
        greeting.GetValue(null).Should().BeSameAs(value);
        managerCount.Should().Be(1);
        loadCount.Should().Be(2);
    }

    [TestMethod]
    public void Culture_Set_ReplacesWholeCache()
    {
        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource));
        using GeneratedAssembly generatedAssembly = result.Emit(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Greeting"] = "Runtime value"
            });
        Type resourceType = generatedAssembly.GetGeneratedType();
        FieldInfo cacheField = GetRequiredField(resourceType, "s_cache");
        PropertyInfo cultureProperty = GetRequiredProperty(resourceType, "Culture");
        object? originalCache = cacheField.GetValue(null);
        CultureInfo culture = new("fr-FR");

        cultureProperty.SetValue(null, culture);

        object? replacementCache = cacheField.GetValue(null);
        replacementCache.Should().NotBeSameAs(originalCache);
        cultureProperty.GetValue(null).Should().BeSameAs(culture);
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GeneratedProperty_AmbientCultureChanges_RefreshesCachedValue(bool includeDefaults)
    {
        using GeneratedAssembly generatedAssembly = CreateCultureAwareAssembly(includeDefaults);
        Type resourceType = generatedAssembly.GetGeneratedType();
        PropertyInfo greeting = GetRequiredProperty(resourceType, "Greeting");
        PropertyInfo culture = GetRequiredProperty(resourceType, "Culture");
        CultureInfo originalCulture = CultureInfo.CurrentUICulture;

        try
        {
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            object? english = greeting.GetValue(null);
            english.Should().Be("en-US");
            greeting.GetValue(null).Should().BeSameAs(english);

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("fr");
            object? french = greeting.GetValue(null);
            french.Should().Be("fr");
            greeting.GetValue(null).Should().BeSameAs(french);
            culture.GetValue(null).Should().BeNull();
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public async Task GeneratedProperty_ConcurrentAmbientCultures_StayIsolated(bool includeDefaults)
    {
        using GeneratedAssembly generatedAssembly = CreateCultureAwareAssembly(includeDefaults);
        PropertyInfo greeting = GetRequiredProperty(generatedAssembly.GetGeneratedType(), "Greeting");
        using Barrier barrier = new(participantCount: 2);

        await Task.WhenAll(
            Task.Run(() => ReadCulture("en-US"), TestContext.CancellationToken),
            Task.Run(() => ReadCulture("fr"), TestContext.CancellationToken));

        void ReadCulture(string name)
        {
            CultureInfo originalCulture = CultureInfo.CurrentUICulture;
            try
            {
                CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo(name);
                for (int iteration = 0; iteration < 100; iteration++)
                {
                    barrier.SignalAndWait(TimeSpan.FromSeconds(10), TestContext.CancellationToken).Should().BeTrue();
                    greeting.GetValue(null).Should().Be(name);
                }
            }
            finally
            {
                CultureInfo.CurrentUICulture = originalCulture;
            }
        }
    }

    [TestMethod]
    [DataRow(false)]
    [DataRow(true)]
    public void GeneratedProperty_ExplicitCultureOverridesAmbient_UntilReset(bool includeDefaults)
    {
        using GeneratedAssembly generatedAssembly = CreateCultureAwareAssembly(includeDefaults);
        Type resourceType = generatedAssembly.GetGeneratedType();
        PropertyInfo greeting = GetRequiredProperty(resourceType, "Greeting");
        PropertyInfo culture = GetRequiredProperty(resourceType, "Culture");
        CultureInfo originalCulture = CultureInfo.CurrentUICulture;

        try
        {
            culture.SetValue(null, CultureInfo.GetCultureInfo("fr"));
            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("en-US");
            object? french = greeting.GetValue(null);
            french.Should().Be("fr");

            CultureInfo.CurrentUICulture = CultureInfo.GetCultureInfo("ja");
            greeting.GetValue(null).Should().BeSameAs(french);

            culture.SetValue(null, null);
            greeting.GetValue(null).Should().Be("ja");
        }
        finally
        {
            CultureInfo.CurrentUICulture = originalCulture;
        }
    }

    [TestMethod]
    public void GeneratedProperty_MissingRequiredResource_Throws()
    {
        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource));
        using GeneratedAssembly generatedAssembly = result.Emit(
            new Dictionary<string, string>(StringComparer.Ordinal));
        PropertyInfo greeting = GetRequiredProperty(generatedAssembly.GetGeneratedType(), "Greeting");

        Action action = () => greeting.GetValue(null);

        action.Should().Throw<TargetInvocationException>()
            .Which.InnerException.Should().BeOfType<MissingManifestResourceException>();
    }

    [TestMethod]
    public void GeneratedProperty_MissingResourceWithDefault_ReturnsAndCachesDefault()
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["IncludeDefaultValues"] = "true"
        };
        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource, metadata: metadata));
        using GeneratedAssembly generatedAssembly = result.Emit(
            new Dictionary<string, string>(StringComparer.Ordinal));
        PropertyInfo greeting = GetRequiredProperty(generatedAssembly.GetGeneratedType(), "Greeting");

        object? first = greeting.GetValue(null);
        object? second = greeting.GetValue(null);

        first.Should().Be("Source fallback");
        second.Should().BeSameAs(first);
    }

    [TestMethod]
    public void FormatMethod_NamedPlaceholder_FormatsCachedProperty()
    {
        const string resource = """
            <root>
              <data name="Greeting"><value>Hello {name}</value></data>
            </root>
            """;
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["EmitFormatMethods"] = "true"
        };
        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(resource, metadata: metadata));
        using GeneratedAssembly generatedAssembly = result.Emit(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Greeting"] = "Hello {name}"
            });
        Type resourceType = generatedAssembly.GetGeneratedType();
        MethodInfo formatGreeting = resourceType.GetMethod(
            "FormatGreeting",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("FormatGreeting was not found.");

        object? formatted = formatGreeting.Invoke(null, ["Ada"]);

        formatted.Should().Be("Hello Ada");
        object? cachedValue = GetRequiredProperty(resourceType, "Greeting").GetValue(null);
        cachedValue.Should().Be("Hello {name}");
    }

    [TestMethod]
    public void FormatMethod_SparseNumericPlaceholder_PadsParameters()
    {
        const string resource = """
            <root>
              <data name="Greeting"><value>Hello {1}</value></data>
            </root>
            """;
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["EmitFormatMethods"] = "true"
        };
        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(resource, metadata: metadata));
        using GeneratedAssembly generatedAssembly = result.Emit(
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                ["Greeting"] = "Hello {1}"
            });
        Type resourceType = generatedAssembly.GetGeneratedType();
        MethodInfo formatGreeting = resourceType.GetMethod(
            "FormatGreeting",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("FormatGreeting was not found.");

        object? formatted = formatGreeting.Invoke(null, ["unused", "Ada"]);

        formatted.Should().Be("Hello Ada");
    }

    public TestContext TestContext { get; set; } = null!;

    private static GeneratedAssembly CreateCultureAwareAssembly(bool includeDefaults)
    {
        StringResourceManagerProvider.Register(
            static (baseName, assembly) => new CultureAwareResourceManager(baseName, assembly));
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["IncludeDefaultValues"] = includeDefaults ? "true" : "false"
        };
        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource, metadata: metadata),
            GeneratorTestResource.Sibling(SimpleResource, "fr"));
        return result.Emit(new Dictionary<string, string>(StringComparer.Ordinal));
    }

    private sealed class CultureAwareResourceManager(string baseName, Assembly assembly)
        : StringResourceManager(baseName, assembly)
    {
        public override string? GetString(string name, CultureInfo? culture)
            => new string((culture ?? CultureInfo.CurrentUICulture).Name.AsSpan());
    }

    private static PropertyInfo GetRequiredProperty(Type type, string name)
    {
        return type.GetProperty(name, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Property '{name}' was not found.");
    }

    private static FieldInfo GetRequiredField(Type type, string name)
    {
        return type.GetField(name, BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException($"Field '{name}' was not found.");
    }

    private static void ResetProvider()
    {
        MethodInfo reset = typeof(StringResourceManagerProvider).GetMethod(
            "ResetForTests",
            BindingFlags.NonPublic | BindingFlags.Static)
            ?? throw new InvalidOperationException("The provider reset hook was not found.");

        reset.Invoke(obj: null, parameters: null);
    }
}