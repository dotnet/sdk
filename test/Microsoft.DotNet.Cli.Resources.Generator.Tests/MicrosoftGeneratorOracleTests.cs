// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Cli.Resources.Generator.Tests;

[TestClass]
public class MicrosoftGeneratorOracleTests
{
    private static readonly SymbolDisplayFormat s_typeDisplayFormat = SymbolDisplayFormat.FullyQualifiedFormat
        .WithMiscellaneousOptions(SymbolDisplayMiscellaneousOptions.IncludeNullableReferenceTypeModifier);

    [TestMethod]
    public void Generate_RetainedOptions_MatchesOracleSurface()
    {
        const string resource = """
            <root>
              <data name="Greeting" xml:space="preserve">
                <value>Hello {name}</value>
              </data>
            </root>
            """;
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["GenerateSource"] = "true",
            ["IncludeDefaultValues"] = "true",
            ["EmitFormatMethods"] = "true",
            ["Public"] = "true"
        };
        GeneratorTestResource testResource = GeneratorTestResource.Selected(resource, metadata: metadata);
        GeneratorTestResult adaptedResult = GeneratorTestHarness.Run(testResource);
        using OracleGenerator oracle = OracleGenerator.Load();
        GeneratorTestResult oracleResult = GeneratorTestHarness.Run(oracle.Generator, testResource);

        adaptedResult.CompilerErrors.Should().BeEmpty();
        oracleResult.CompilerErrors.Should().BeEmpty();
        INamedTypeSymbol adaptedType = GetGeneratedType(adaptedResult);
        INamedTypeSymbol oracleType = GetGeneratedType(oracleResult);

        adaptedType.Name.Should().Be(oracleType.Name);
        adaptedType.ContainingNamespace.ToDisplayString().Should().Be(oracleType.ContainingNamespace.ToDisplayString());
        adaptedType.DeclaredAccessibility.Should().Be(oracleType.DeclaredAccessibility);
        adaptedType.IsStatic.Should().Be(oracleType.IsStatic);
        GetPublicPropertyNames(adaptedType).Should().BeEquivalentTo(GetPublicPropertyNames(oracleType));
        GetPropertySurface(adaptedType, "Greeting").Should().Be(GetPropertySurface(oracleType, "Greeting"));
        GetPropertySurface(adaptedType, "Culture").Should().Be(GetPropertySurface(oracleType, "Culture"));
        GetFormatMethods(adaptedType).Should().BeEquivalentTo(
            GetFormatMethods(oracleType),
            "the pinned oracle generated:{0}{1}",
            Environment.NewLine,
            oracleResult.SingleSource);
        GetRequiredProperty(adaptedType, "ResourceManager").Type.ToDisplayString(s_typeDisplayFormat)
            .Should().Be("global::Microsoft.DotNet.Cli.Resources.StringResourceManager");
        GetRequiredProperty(oracleType, "ResourceManager").Type.ToDisplayString(s_typeDisplayFormat)
            .Should().Be("global::System.Resources.ResourceManager");
    }

    [TestMethod]
    public void Generate_RelativeDirectoryDefaultNaming_MatchesOracleTypeIdentity()
    {
        const string resource = "<root><data name=\"Greeting\"><value>Hello</value></data></root>";
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["ClassName"] = string.Empty,
            ["Link"] = string.Empty,
            ["ManifestResourceName"] = string.Empty,
            ["RelativeDir"] = "Features/",
            ["CliResourceFamily"] = string.Empty
        };
        GeneratorTestResource testResource = GeneratorTestResource.Selected(
            resource,
            path: "../shared/Messages.resx",
            metadata: metadata);
        GeneratorTestResult adaptedResult = GeneratorTestHarness.Run(testResource);
        using OracleGenerator oracle = OracleGenerator.Load();
        GeneratorTestResult oracleResult = GeneratorTestHarness.Run(oracle.Generator, testResource);

        adaptedResult.CompilerErrors.Should().BeEmpty();
        oracleResult.CompilerErrors.Should().BeEmpty();
        GetGeneratedResourceType(adaptedResult).ToDisplayString()
            .Should().Be(GetGeneratedResourceType(oracleResult).ToDisplayString());
    }

    [TestMethod]
    public void Generate_ImplicitCulturedResource_MatchesOracleNoOutput()
    {
        const string resource = "<root><data name=\"Greeting\"><value>Bonjour</value></data></root>";
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["GenerateSource"] = string.Empty,
            ["CliResourceGenerateSource"] = "false",
            ["WithCulture"] = "true"
        };
        GeneratorTestResource testResource = GeneratorTestResource.Selected(resource, metadata: metadata);
        GeneratorTestResult adaptedResult = GeneratorTestHarness.Run(testResource);
        using OracleGenerator oracle = OracleGenerator.Load();
        GeneratorTestResult oracleResult = GeneratorTestHarness.Run(oracle.Generator, testResource);

        adaptedResult.GeneratedSources.Should().BeEmpty();
        oracleResult.GeneratedSources.Should().BeEmpty();
    }

    [TestMethod]
    public void Generate_ExplicitCulturedResource_MatchesOracleGeneratedSurface()
    {
        const string resource = "<root><data name=\"Greeting\"><value>Bonjour</value></data></root>";
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["GenerateSource"] = "true",
            ["CliResourceGenerateSource"] = "true",
            ["WithCulture"] = "true"
        };
        GeneratorTestResource testResource = GeneratorTestResource.Selected(resource, metadata: metadata);
        GeneratorTestResult adaptedResult = GeneratorTestHarness.Run(testResource);
        using OracleGenerator oracle = OracleGenerator.Load();
        GeneratorTestResult oracleResult = GeneratorTestHarness.Run(oracle.Generator, testResource);

        adaptedResult.CompilerErrors.Should().BeEmpty();
        oracleResult.CompilerErrors.Should().BeEmpty();
        GetPropertySurface(GetGeneratedType(adaptedResult), "Greeting")
            .Should().Be(GetPropertySurface(GetGeneratedType(oracleResult), "Greeting"));
    }

    [TestMethod]
    public void Generate_CustomManifestWithoutClassName_MatchesOracleTypeIdentity()
    {
        const string resource = "<root><data name=\"Greeting\"><value>Hello</value></data></root>";
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["ClassName"] = string.Empty,
            ["ManifestResourceName"] = "Company.Custom.Manifest",
            ["RelativeDir"] = "Resources/",
            ["CliResourceFamily"] = "Company.Custom.Manifest"
        };
        GeneratorTestResource testResource = GeneratorTestResource.Selected(resource, metadata: metadata);
        GeneratorTestResult adaptedResult = GeneratorTestHarness.Run(testResource);
        using OracleGenerator oracle = OracleGenerator.Load();
        GeneratorTestResult oracleResult = GeneratorTestHarness.Run(oracle.Generator, testResource);

        adaptedResult.CompilerErrors.Should().BeEmpty();
        oracleResult.CompilerErrors.Should().BeEmpty();
        GetGeneratedResourceType(adaptedResult).ToDisplayString()
            .Should().Be(GetGeneratedResourceType(oracleResult).ToDisplayString());
        adaptedResult.SingleSource.Should().Contain("@\"Company.Custom.Manifest\"");
    }

    private static INamedTypeSymbol GetGeneratedType(GeneratorTestResult result)
        => GetGeneratedType(result, "Test.Resources.Strings");

    private static INamedTypeSymbol GetGeneratedType(GeneratorTestResult result, string metadataName)
    {
        return result.OutputCompilation.GetTypeByMetadataName(metadataName)
            ?? throw new InvalidOperationException("The generated resource type was not found.");
    }

    private static INamedTypeSymbol GetGeneratedResourceType(GeneratorTestResult result)
    {
        SyntaxTree generatedTree = result.GeneratedSources.Single().SyntaxTree;
        Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax declaration = generatedTree
            .GetRoot()
            .DescendantNodes()
            .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.ClassDeclarationSyntax>()
            .Single(type => type.Members
                .OfType<Microsoft.CodeAnalysis.CSharp.Syntax.PropertyDeclarationSyntax>()
                .Any(static property => property.Identifier.ValueText == "Greeting"));

        ISymbol? symbol = result.OutputCompilation.GetSemanticModel(generatedTree).GetDeclaredSymbol(declaration);
        return symbol as INamedTypeSymbol
            ?? throw new InvalidOperationException("The generated resource type symbol was not found.");
    }

    private static IPropertySymbol GetRequiredProperty(INamedTypeSymbol type, string name)
    {
        return type.GetMembers(name).OfType<IPropertySymbol>().Single();
    }

    private static string GetPropertySurface(INamedTypeSymbol type, string name)
    {
        IPropertySymbol property = GetRequiredProperty(type, name);
        return string.Join(
            "|",
            property.Name,
            property.DeclaredAccessibility,
            property.IsStatic,
            property.Type.ToDisplayString(s_typeDisplayFormat),
            property.GetMethod is not null,
            property.SetMethod is not null);
    }

    private static string[] GetPublicPropertyNames(INamedTypeSymbol type) =>
        [.. type.GetMembers()
            .OfType<IPropertySymbol>()
            .Where(static property => property.DeclaredAccessibility == Accessibility.Public)
            .Select(static property => property.Name)
            .OrderBy(static name => name, StringComparer.Ordinal)];

    private static string[] GetFormatMethods(INamedTypeSymbol type) =>
        [.. type.GetMembers()
            .OfType<IMethodSymbol>()
            .Where(static method => method.Name.StartsWith("Format", StringComparison.Ordinal))
            .Select(method => string.Join(
                "|",
                method.Name,
                method.DeclaredAccessibility,
                method.IsStatic,
                method.ReturnType.ToDisplayString(s_typeDisplayFormat),
                string.Join(",", method.Parameters.Select(parameter => string.Join(
                    ":",
                    parameter.Name,
                    parameter.Type.ToDisplayString(s_typeDisplayFormat))))))
            .OrderBy(static method => method, StringComparer.Ordinal)];

        [TestMethod]
        public void Generate_DefaultValueWhitespace_MatchesOracleFallback()
        {
                const string resource = """
                        <root>
                            <data name="Greeting" xml:space="preserve">
                                <value>  Hello  </value>
                            </data>
                        </root>
                        """;
                Dictionary<string, string> metadata = new(StringComparer.Ordinal)
                {
                        ["GenerateSource"] = "true",
                        ["IncludeDefaultValues"] = "true"
                };
                GeneratorTestResource testResource = GeneratorTestResource.Selected(resource, metadata: metadata);
                GeneratorTestResult adaptedResult = GeneratorTestHarness.Run(testResource);
                using OracleGenerator oracle = OracleGenerator.Load();
                GeneratorTestResult oracleResult = GeneratorTestHarness.Run(oracle.Generator, testResource);

                adaptedResult.CompilerErrors.Should().BeEmpty();
                oracleResult.CompilerErrors.Should().BeEmpty();
                adaptedResult.SingleSource.Should().Contain("@\"Hello\"");
                oracleResult.SingleSource.Should().Contain("@\"Hello\"");
                adaptedResult.SingleSource.Should().NotContain("@\"  Hello  \"");
                oracleResult.SingleSource.Should().NotContain("@\"  Hello  \"");
        }
}