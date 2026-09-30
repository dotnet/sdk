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

        object? first = greeting.GetValue(null);
        object? second = greeting.GetValue(null);

        first.Should().Be("Runtime value");
        second.Should().BeSameAs(first);
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