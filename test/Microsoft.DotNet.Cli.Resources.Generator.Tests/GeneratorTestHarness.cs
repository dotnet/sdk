// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Resources;
using System.Runtime.Loader;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Emit;
using Microsoft.CodeAnalysis.Text;

namespace Microsoft.DotNet.Cli.Resources.Generator.Tests;

internal static class GeneratorTestHarness
{
    private const string Source = "internal sealed class GeneratorTestAnchor { }";

    internal static GeneratorTestResult Run(params GeneratorTestResource[] resources)
        => RunCore(new ResxSourceGenerator(), GetReferences(), Source, resources);

    internal static GeneratorTestResult Run(
        IIncrementalGenerator generator,
        params GeneratorTestResource[] resources)
        => RunCore(generator, GetReferences(), Source, resources);

    internal static GeneratorTestResult RunNet472(params GeneratorTestResource[] resources)
        => RunCore(new ResxSourceGenerator(), GetNet472References(), Net472RuntimeStubs, resources);

    internal static ImmutableArray<IncrementalStepRunReason> RunAfterUnrelatedSourceChange(
        GeneratorTestResource resource)
    {
        CSharpParseOptions parseOptions = new(LanguageVersion.Preview);
        CSharpCompilation compilation = CreateCompilation(parseOptions, GetReferences(), Source);
        TestAdditionalText additionalText = new(resource.Path, resource.Content);
        TestAnalyzerConfigOptionsProvider optionsProvider = new([resource]);
        GeneratorDriverOptions driverOptions = new(
            IncrementalGeneratorOutputKind.None,
            trackIncrementalGeneratorSteps: true);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new ResxSourceGenerator().AsSourceGenerator()],
            additionalTexts: [additionalText],
            parseOptions: parseOptions,
            optionsProvider: optionsProvider,
            driverOptions: driverOptions);

        driver = driver.RunGenerators(compilation);
        compilation = compilation.AddSyntaxTrees(CSharpSyntaxTree.ParseText(
            "internal sealed class UnrelatedSource { }",
            parseOptions));
        driver = driver.RunGenerators(compilation);

        GeneratorRunResult result = driver.GetRunResult().Results.Single();
        return
        [
            .. result.TrackedSteps["ResourceGeneration"]
                .SelectMany(static step => step.Outputs)
                .Select(static output => output.Reason)
        ];
    }

    internal static ImmutableArray<IncrementalStepRunReason> RunAfterOneResourceChanges(
        GeneratorTestResource changedResource,
        string changedContent,
        GeneratorTestResource unchangedResource)
    {
        CSharpParseOptions parseOptions = new(LanguageVersion.Preview);
        CSharpCompilation compilation = CreateCompilation(parseOptions, GetReferences(), Source);
        TestAdditionalText changedText = new(changedResource.Path, changedResource.Content);
        TestAdditionalText unchangedText = new(unchangedResource.Path, unchangedResource.Content);
        TestAnalyzerConfigOptionsProvider optionsProvider = new([changedResource, unchangedResource]);
        GeneratorDriverOptions driverOptions = new(
            IncrementalGeneratorOutputKind.None,
            trackIncrementalGeneratorSteps: true);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [new ResxSourceGenerator().AsSourceGenerator()],
            additionalTexts: [changedText, unchangedText],
            parseOptions: parseOptions,
            optionsProvider: optionsProvider,
            driverOptions: driverOptions);

        driver = driver.RunGenerators(compilation);
        driver = driver.ReplaceAdditionalText(
            changedText,
            new TestAdditionalText(changedResource.Path, changedContent));
        driver = driver.RunGenerators(compilation);

        GeneratorRunResult result = driver.GetRunResult().Results.Single();
        return
        [
            .. result.TrackedSteps["ResourceGeneration"]
                .SelectMany(static step => step.Outputs)
                .Select(static output => output.Reason)
        ];
    }

    private static GeneratorTestResult RunCore(
        IIncrementalGenerator generator,
        ImmutableArray<MetadataReference> references,
        string source,
        params GeneratorTestResource[] resources)
    {
        CSharpParseOptions parseOptions = new(LanguageVersion.Preview);
        CSharpCompilation compilation = CreateCompilation(parseOptions, references, source);

        ImmutableArray<AdditionalText> additionalTexts =
            [.. resources.Select(static resource => (AdditionalText)new TestAdditionalText(resource.Path, resource.Content))];
        TestAnalyzerConfigOptionsProvider optionsProvider = new(resources);
        GeneratorDriver driver = CSharpGeneratorDriver.Create(
            generators: [generator.AsSourceGenerator()],
            additionalTexts: additionalTexts,
            parseOptions: parseOptions,
            optionsProvider: optionsProvider);

        driver = driver.RunGeneratorsAndUpdateCompilation(
            compilation,
            out Compilation outputCompilation,
            out _);

        GeneratorDriverRunResult runResult = driver.GetRunResult();
        GeneratorRunResult generatorResult = runResult.Results.Single();
        ImmutableArray<Diagnostic> compilerDiagnostics =
            [.. outputCompilation.GetDiagnostics()
                .Where(static diagnostic => diagnostic.Severity is DiagnosticSeverity.Warning or DiagnosticSeverity.Error)];
        ImmutableArray<Diagnostic> compilerErrors =
            [.. compilerDiagnostics.Where(static diagnostic => diagnostic.Severity == DiagnosticSeverity.Error)];

        return new(
            generatorResult.GeneratedSources,
            generatorResult.Diagnostics,
            compilerDiagnostics,
            compilerErrors,
            outputCompilation);
    }

    private static CSharpCompilation CreateCompilation(
        CSharpParseOptions parseOptions,
        ImmutableArray<MetadataReference> references,
        string source)
    {
        return CSharpCompilation.Create(
            assemblyName: "Microsoft.DotNet.Cli.Resources.Generator.TestCompilation",
            syntaxTrees: [CSharpSyntaxTree.ParseText(source, parseOptions)],
            references: references,
            options: new CSharpCompilationOptions(
                OutputKind.DynamicallyLinkedLibrary,
                optimizationLevel: OptimizationLevel.Release,
                nullableContextOptions: NullableContextOptions.Enable));
    }

    private static ImmutableArray<MetadataReference> GetReferences()
    {
        if (AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is not string trustedPlatformAssemblies)
        {
            throw new InvalidOperationException("Trusted platform assemblies are unavailable.");
        }

        HashSet<string> paths = [with(StringComparer.Ordinal)];
        ImmutableArray<MetadataReference>.Builder references = ImmutableArray.CreateBuilder<MetadataReference>();

        foreach (string path in trustedPlatformAssemblies.Split(Path.PathSeparator))
        {
            if (paths.Add(path))
            {
                references.Add(MetadataReference.CreateFromFile(path));
            }
        }

        string resourceRuntimeAssembly = typeof(StringResourceManager).Assembly.Location;
        if (paths.Add(resourceRuntimeAssembly))
        {
            references.Add(MetadataReference.CreateFromFile(resourceRuntimeAssembly));
        }

        return references.ToImmutable();
    }

    private static ImmutableArray<MetadataReference> GetNet472References()
    {
        string packages = GetAssemblyMetadata("NuGetPackageRoot");
        string framework = Path.Join(
            packages,
            "microsoft.netframework.referenceassemblies.net472",
            "1.0.3",
            "build",
            ".NETFramework",
            "v4.7.2");

        return
        [
            MetadataReference.CreateFromFile(Path.Join(framework, "mscorlib.dll")),
            MetadataReference.CreateFromFile(Path.Join(framework, "System.dll")),
            MetadataReference.CreateFromFile(Path.Join(framework, "System.Core.dll"))
        ];
    }

    private static string GetAssemblyMetadata(string key)
    {
        foreach (AssemblyMetadataAttribute attribute in typeof(GeneratorTestHarness).Assembly
            .GetCustomAttributes<AssemblyMetadataAttribute>())
        {
            if (attribute.Key == key && !string.IsNullOrEmpty(attribute.Value))
            {
                return attribute.Value;
            }
        }

        throw new InvalidOperationException($"Assembly metadata '{key}' is unavailable.");
    }

    private const string Net472RuntimeStubs = """
        #nullable enable

        namespace Microsoft.DotNet.Cli.Resources
        {
            public class StringResourceManager
            {
                public StringResourceManager(string baseName, System.Reflection.Assembly assembly)
                {
                }

                public virtual string? GetString(
                    string name,
                    System.Globalization.CultureInfo? culture) => null;
            }

            public sealed class SatelliteStringResourceManager : StringResourceManager
            {
                private SatelliteStringResourceManager(string baseName, System.Reflection.Assembly assembly)
                    : base(baseName, assembly)
                {
                }

                public static SatelliteStringResourceManager FromRuntimeSatellites(
                    string baseName,
                    System.Reflection.Assembly assembly) => new(baseName, assembly);
            }

            public static class StringResourceManagerProvider
            {
                public static StringResourceManager Create(
                    string baseName,
                    System.Reflection.Assembly assembly) =>
                    SatelliteStringResourceManager.FromRuntimeSatellites(baseName, assembly);
            }
        }
        """;
}

internal sealed class GeneratorTestResult(
    ImmutableArray<GeneratedSourceResult> generatedSources,
    ImmutableArray<Diagnostic> generatorDiagnostics,
    ImmutableArray<Diagnostic> compilerDiagnostics,
    ImmutableArray<Diagnostic> compilerErrors,
    Compilation outputCompilation)
{
    internal ImmutableArray<GeneratedSourceResult> GeneratedSources { get; } = generatedSources;

    internal ImmutableArray<Diagnostic> GeneratorDiagnostics { get; } = generatorDiagnostics;

    internal ImmutableArray<Diagnostic> CompilerDiagnostics { get; } = compilerDiagnostics;

    internal ImmutableArray<Diagnostic> CompilerErrors { get; } = compilerErrors;

    internal Compilation OutputCompilation { get; } = outputCompilation;

    internal string SingleSource => GeneratedSources.Single().SourceText.ToString();

    internal GeneratedAssembly Emit(IReadOnlyDictionary<string, string> resources)
    {
        byte[] resourceData;
        using (MemoryStream resourceStream = new())
        {
            using ResourceWriter writer = new(resourceStream);
            foreach (KeyValuePair<string, string> resource in resources)
            {
                writer.AddResource(resource.Key, resource.Value);
            }

            writer.Generate();
            resourceData = resourceStream.ToArray();
        }

        ResourceDescription description = new(
            "Test.Resources.Strings.resources",
            () => new MemoryStream(resourceData, writable: false),
            isPublic: true);
        using MemoryStream assemblyStream = new();
        EmitResult emitResult = OutputCompilation.Emit(
            assemblyStream,
            manifestResources: [description]);

        if (!emitResult.Success)
        {
            throw new InvalidOperationException(string.Join(Environment.NewLine, emitResult.Diagnostics));
        }

        assemblyStream.Position = 0;
        return GeneratedAssembly.Load(assemblyStream);
    }
}

internal sealed class GeneratedAssembly : IDisposable
{
    private readonly AssemblyLoadContext _loadContext;

    private GeneratedAssembly(AssemblyLoadContext loadContext, Assembly assembly)
    {
        _loadContext = loadContext;
        Assembly = assembly;
    }

    internal Assembly Assembly { get; }

    internal static GeneratedAssembly Load(Stream assemblyStream)
    {
        AssemblyLoadContext loadContext = new(
            "Microsoft.DotNet.Cli.Resources.Generator.Tests." + Guid.NewGuid(),
            isCollectible: true);
        loadContext.Resolving += ResolveResourceRuntime;
        Assembly assembly = loadContext.LoadFromStream(assemblyStream);
        return new(loadContext, assembly);
    }

    internal Type GetGeneratedType()
    {
        return Assembly.GetType("Test.Resources.Strings", throwOnError: false)
            ?? throw new InvalidOperationException("The generated resource type was not emitted.");
    }

    public void Dispose()
    {
        _loadContext.Resolving -= ResolveResourceRuntime;
        _loadContext.Unload();
    }

    private static Assembly? ResolveResourceRuntime(AssemblyLoadContext context, AssemblyName name)
    {
        Assembly resourceRuntimeAssembly = typeof(StringResourceManager).Assembly;
        return name.Name == resourceRuntimeAssembly.GetName().Name ? resourceRuntimeAssembly : null;
    }
}

internal sealed class GeneratorTestResource
{
    private GeneratorTestResource(
        string path,
        string content,
        IReadOnlyDictionary<string, string> metadata)
    {
        Path = path;
        Content = content;
        Metadata = metadata;
    }

    internal string Path { get; }

    internal string Content { get; }

    internal IReadOnlyDictionary<string, string> Metadata { get; }

    internal static GeneratorTestResource Selected(
        string content,
        string path = "Resources/Strings.resx",
        IReadOnlyDictionary<string, string>? metadata = null)
    {
        Dictionary<string, string> values = new(StringComparer.Ordinal)
        {
            ["Generator"] = "Microsoft.DotNet.Cli.Resources",
            ["CliResourceGenerateSource"] = "true",
            ["CliResourceFamily"] = "Test.Resources.Strings",
            ["ManifestResourceName"] = "Test.Resources.Strings",
            ["ClassName"] = "Test.Resources.Strings"
        };

        if (metadata is not null)
        {
            foreach (KeyValuePair<string, string> pair in metadata)
            {
                values[pair.Key] = pair.Value;
            }
        }

        return new(path, content, values);
    }

    internal static GeneratorTestResource Sibling(
        string content,
        string culture,
        string path = "Resources/Strings.fr.resx",
        string resourceFamily = "Test.Resources.Strings")
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["Culture"] = culture,
            ["ManifestResourceName"] = resourceFamily + "." + culture,
            ["CliResourceFamily"] = resourceFamily
        };

        return new(path, content, metadata);
    }
}

internal sealed class TestAdditionalText(string path, string content) : AdditionalText
{
    public override string Path { get; } = path;

    public override SourceText GetText(CancellationToken cancellationToken = default) =>
        SourceText.From(content);
}

internal sealed class TestAnalyzerConfigOptions(
    IReadOnlyDictionary<string, string> options) : AnalyzerConfigOptions
{
    internal static TestAnalyzerConfigOptions Empty { get; } = new(new Dictionary<string, string>());

    public override IEnumerable<string> Keys => options.Keys;

    public override bool TryGetValue(string key, [MaybeNullWhen(false)] out string value) =>
        options.TryGetValue(key, out value);
}

internal sealed class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
{
    private readonly Dictionary<string, TestAnalyzerConfigOptions> _optionsByPath;

    internal TestAnalyzerConfigOptionsProvider(IEnumerable<GeneratorTestResource> resources)
    {
        _optionsByPath = [with(StringComparer.Ordinal)];

        foreach (GeneratorTestResource resource in resources)
        {
            Dictionary<string, string> options = [with(StringComparer.Ordinal)];
            foreach (KeyValuePair<string, string> pair in resource.Metadata)
            {
                options["build_metadata.AdditionalFiles." + pair.Key] = pair.Value;
            }

            _optionsByPath.Add(resource.Path, new(options));
        }
    }

    public override AnalyzerConfigOptions GlobalOptions { get; } = new TestAnalyzerConfigOptions(
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["build_property.RootNamespace"] = "Test"
        });

    public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => TestAnalyzerConfigOptions.Empty;

    public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) =>
        _optionsByPath.TryGetValue(textFile.Path, out TestAnalyzerConfigOptions? options)
            ? options
            : TestAnalyzerConfigOptions.Empty;
}