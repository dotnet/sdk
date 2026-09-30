// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;
using System.Text;

namespace Microsoft.DotNet.Cli.Resources.Generator.Tests;

[TestClass]
public class ResxSourceGeneratorTests
{
    private const string SimpleResource = """
        <root>
          <data name="Greeting" xml:space="preserve">
            <value>Hello</value>
          </data>
        </root>
        """;

    [TestMethod]
    public void Generate_SelectedString_EmitsCompilingCachedProperty()
    {
        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource));

        result.GeneratorDiagnostics.Should().BeEmpty();
        result.CompilerErrors.Should().BeEmpty();
        result.GeneratedSources.Should().ContainSingle();
        result.SingleSource.Should().Contain("private sealed class __ResourceCache");
        result.SingleSource.Should().Contain("private static class __ResourceManagerCache");
        result.SingleSource.Should().Contain(
            "private static string GetCachedResourceString(ref string? value, string resourceKey)");
        result.SingleSource.Should().Contain("get => GetCachedResourceString(");
        result.SingleSource.Should().Contain("ref s_cache._value0");
        result.SingleSource.Should().Contain("nameof(@Greeting)");
        result.SingleSource.Should().Contain("throw new global::System.Resources.MissingManifestResourceException()");
    }

    [TestMethod]
    public void Generate_SelectedString_MarksGetterAndCacheHelperAggressiveInlining()
    {
        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource));
        INamedTypeSymbol type = result.OutputCompilation.GetTypeByMetadataName("Test.Resources.Strings")
            ?? throw new InvalidOperationException("The generated resource type was not found.");
        IPropertySymbol property = type.GetMembers("Greeting").OfType<IPropertySymbol>().Single();
        IMethodSymbol getter = property.GetMethod
            ?? throw new InvalidOperationException("The generated property getter was not found.");
        IMethodSymbol helper = type.GetMembers("GetCachedResourceString")
            .OfType<IMethodSymbol>()
            .Single(method => method.Parameters.Length == 2);

        AssertAggressiveInlining(getter);
        AssertAggressiveInlining(helper);
    }

    [TestMethod]
    public void Generate_DefaultValueAndFormatMethod_UsesCachedProperty()
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
            ["IncludeDefaultValues"] = "true",
            ["EmitFormatMethods"] = "true"
        };

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(resource, metadata: metadata));

        result.GeneratorDiagnostics.Should().BeEmpty();
        result.CompilerErrors.Should().BeEmpty();
        result.SingleSource.Should().Contain(
            "private static string GetCachedResourceString(ref string? value, string resourceKey, string defaultValue)");
        result.SingleSource.Should().Contain("ref s_cache._value0");
        result.SingleSource.Should().Contain("@\"Hello {name}\"");
        result.SingleSource.Should().Contain(
            "global::System.String.Format(global::@Test.@Resources.@Strings.Culture, "
            + "global::@Test.@Resources.@Strings.@Greeting.Replace(@\"{name}\", @\"{0}\"), @name)");
    }

    [TestMethod]
    public void Generate_MixedNamedAndNumericFormatArguments_OmitsFormatMethod()
    {
        const string resource = """
            <root>
              <data name="Greeting"><value>Hello {name} {0}</value></data>
            </root>
            """;
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["EmitFormatMethods"] = "true"
        };

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(resource, metadata: metadata));

        result.GeneratorDiagnostics.Should().BeEmpty();
        result.CompilerErrors.Should().BeEmpty();
        result.SingleSource.Should().Contain("string @Greeting");
        result.SingleSource.Should().NotContain("FormatGreeting(");
    }

    [TestMethod]
    public void Generate_EscapedFormatBraces_OmitsFormatMethods()
    {
        const string resource = """
            <root>
              <data name="Named"><value>Hello {{name}}</value></data>
              <data name="Numeric"><value>Hello {{0}}</value></data>
            </root>
            """;
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["EmitFormatMethods"] = "true"
        };

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(resource, metadata: metadata));

        result.GeneratorDiagnostics.Should().BeEmpty();
        result.CompilerErrors.Should().BeEmpty();
        result.SingleSource.Should().Contain("string @Named");
        result.SingleSource.Should().Contain("string @Numeric");
        result.SingleSource.Should().NotContain("FormatNamed(");
        result.SingleSource.Should().NotContain("FormatNumeric(");
    }

    [TestMethod]
    public void Generate_LocalizedSibling_UsesManagerProvider()
    {
        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource),
            GeneratorTestResource.Sibling(SimpleResource, "fr"));

        result.GeneratorDiagnostics.Should().BeEmpty();
        result.CompilerErrors.Should().BeEmpty();
        result.SingleSource.Should().Contain("StringResourceManagerProvider.Create");
        result.SingleSource.Should().NotContain("SatelliteStringResourceManager.FromRuntimeSatellites");
    }

    [TestMethod]
    public void Generate_FinalResourceFamilyEndingInCulture_DetectsLocalizedSibling()
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["ManifestResourceName"] = "Company.Messages.fr",
            ["CliResourceFamily"] = "Company.Messages.fr"
        };
        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource, metadata: metadata),
            GeneratorTestResource.Sibling(
                SimpleResource,
                "fr",
                path: "Resources/Messages.fr.resx",
                resourceFamily: "Company.Messages.fr"));

        result.GeneratorDiagnostics.Should().BeEmpty();
        result.CompilerErrors.Should().BeEmpty();
        result.SingleSource.Should().Contain("StringResourceManagerProvider.Create");
    }

    [TestMethod]
    public void Generate_SanitizedIdentifier_UsesLiteralResourceKey()
    {
        const string resource = """
            <root>
              <data name="Greeting-Text" xml:space="preserve">
                <value>Hello</value>
              </data>
            </root>
            """;

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(resource));

        result.GeneratorDiagnostics.Should().BeEmpty();
        result.CompilerErrors.Should().BeEmpty();
        result.SingleSource.Should().Contain("public static string @Greeting_Text");
        result.SingleSource.Should().Contain("get => GetCachedResourceString(");
        result.SingleSource.Should().Contain("@\"Greeting-Text\"");
    }

    [TestMethod]
    public void Generate_NonStringEntry_ReportsDiagnosticAndContinues()
    {
        const string resource = """
            <root>
              <data name="Blob" type="System.Byte[], mscorlib">
                <value>AA==</value>
              </data>
              <data name="Greeting">
                <value>Hello</value>
              </data>
            </root>
            """;

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(resource));

        result.GeneratorDiagnostics.Should().ContainSingle()
            .Which.Id.Should().Be("CLIRESX0001");
        result.CompilerErrors.Should().BeEmpty();
        result.SingleSource.Should().Contain("public static string @Greeting");
        result.SingleSource.Should().NotContain("public static string @Blob");
    }

    [TestMethod]
    [DataRow("OmitGetResourceString", "true")]
    [DataRow("AsConstants", "true")]
    [DataRow("NoWarn", "CS1591")]
    public void Generate_UnsupportedOption_ReportsDiagnostic(string optionName, string optionValue)
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            [optionName] = optionValue
        };

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource, metadata: metadata));

        result.GeneratorDiagnostics.Should().ContainSingle()
            .Which.Id.Should().Be("CLIRESX0002");
        result.GeneratedSources.Should().BeEmpty();
    }

    [TestMethod]
    public void Generate_DocumentType_ReportsDiagnosticWithoutResolvingEntity()
    {
        const string resource = """
            <!DOCTYPE root [<!ENTITY external SYSTEM "file:///not-accessed">]>
            <root>
              <data name="Greeting">
                <value>&external;</value>
              </data>
            </root>
            """;

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(resource));

        result.GeneratorDiagnostics.Should().ContainSingle()
            .Which.Id.Should().Be("CLIRESX0003");
        result.GeneratedSources.Should().BeEmpty();
    }

    [TestMethod]
    public void Generate_SourceAtCharacterLimit_Generates()
    {
        int limit = GetGeneratorConstant("ResourceInput", "MaxSourceCharacters");
        string resource = CreateSizedResource(limit);

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(resource));

        result.GeneratorDiagnostics.Should().BeEmpty();
        result.CompilerErrors.Should().BeEmpty();
        resource.Length.Should().Be(limit);
    }

    [TestMethod]
    public void Generate_SourceOverCharacterLimit_ReportsDiagnostic()
    {
        int limit = GetGeneratorConstant("ResourceInput", "MaxSourceCharacters");
        string resource = CreateSizedResource(limit + 1);

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(resource));

        result.GeneratorDiagnostics.Should().ContainSingle()
            .Which.Id.Should().Be("CLIRESX0003");
        result.GeneratedSources.Should().BeEmpty();
    }

    [TestMethod]
    public void Generate_AtEntryLimit_Generates()
    {
        int limit = GetGeneratorConstant("ResxParser", "MaxResourceEntries");

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(CreateResourceWithEntries(limit)));

        result.GeneratorDiagnostics.Should().BeEmpty();
        result.CompilerErrors.Should().BeEmpty();
    }

    [TestMethod]
    public void Generate_OverEntryLimit_ReportsDiagnostic()
    {
        int limit = GetGeneratorConstant("ResxParser", "MaxResourceEntries");

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(CreateResourceWithEntries(limit + 1)));

        result.GeneratorDiagnostics.Should().ContainSingle()
            .Which.Id.Should().Be("CLIRESX0003");
        result.GeneratedSources.Should().BeEmpty();
    }

    [TestMethod]
    public void Generate_NamedFormatArgumentsAtLimit_Generates()
    {
        int limit = GetGeneratorConstant("ResxParser", "MaxFormatArguments");

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(
                CreateResourceWithFormatArguments(limit, named: true),
                metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["EmitFormatMethods"] = "true"
                }));

        result.GeneratorDiagnostics.Should().BeEmpty();
        result.CompilerErrors.Should().BeEmpty();
    }

    [TestMethod]
    public void Generate_NamedFormatArgumentsOverLimit_ReportsDiagnostic()
    {
        int limit = GetGeneratorConstant("ResxParser", "MaxFormatArguments");

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(
                CreateResourceWithFormatArguments(limit + 1, named: true),
                metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["EmitFormatMethods"] = "true"
                }));

        result.GeneratorDiagnostics.Should().ContainSingle()
            .Which.Id.Should().Be("CLIRESX0003");
        result.GeneratedSources.Should().BeEmpty();
    }

    [TestMethod]
    public void Generate_NumericFormatIndexOverLimit_ReportsDiagnostic()
    {
        int limit = GetGeneratorConstant("ResxParser", "MaxFormatArguments");
        string resource = $"<root><data name=\"Value\"><value>{{{limit}}}</value></data></root>";

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(
                resource,
                metadata: new Dictionary<string, string>(StringComparer.Ordinal)
                {
                    ["EmitFormatMethods"] = "true"
                }));

        result.GeneratorDiagnostics.Should().ContainSingle()
            .Which.Id.Should().Be("CLIRESX0003");
        result.GeneratedSources.Should().BeEmpty();
    }

    [TestMethod]
    public void Generate_Net472ReferenceSurface_Compiles()
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["IncludeDefaultValues"] = "true"
        };
        GeneratorTestResult result = GeneratorTestHarness.RunNet472(
            GeneratorTestResource.Selected(SimpleResource, metadata: metadata));

        result.GeneratorDiagnostics.Should().BeEmpty();
        result.CompilerDiagnostics.Should().BeEmpty();
    }

    [TestMethod]
    public void Generate_LinkedCustomResource_UsesConfiguredIdentityAndVisibility()
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["Link"] = "Features/Messages.resx",
            ["ManifestResourceName"] = "Company.Manifest.Messages",
            ["ClassName"] = "Company.Linked.Messages",
            ["Public"] = "true"
        };

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(
                SimpleResource,
                path: "../shared/Messages.resx",
                metadata: metadata));

        result.GeneratorDiagnostics.Should().BeEmpty();
        result.CompilerErrors.Should().BeEmpty();
        result.SingleSource.Should().Contain("namespace @Company.@Linked");
        result.SingleSource.Should().Contain("public static partial class @Messages");
        result.SingleSource.Should().Contain("@\"Company.Manifest.Messages\"");
    }

    [TestMethod]
    [DataRow("/Rooted/")]
    [DataRow("\\Rooted\\")]
    [DataRow("C:DriveRelative\\")]
    [DataRow("C:\\Rooted\\")]
    public void Generate_RootedRelativeDirectory_IgnoresDirectory(string relativeDirectory)
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["ClassName"] = string.Empty,
            ["Link"] = string.Empty,
            ["ManifestResourceName"] = string.Empty,
            ["RelativeDir"] = relativeDirectory,
            ["CliResourceFamily"] = string.Empty
        };

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(
                SimpleResource,
                path: "Strings.resx",
                metadata: metadata));

        result.GeneratorDiagnostics.Should().BeEmpty();
        result.CompilerErrors.Should().BeEmpty();
        result.OutputCompilation.GetTypeByMetadataName("Test.Strings").Should().NotBeNull();
    }

    [TestMethod]
    public void Generate_DisabledResource_EmitsNothing()
    {
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["CliResourceGenerateSource"] = "false"
        };

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource, metadata: metadata));

        result.GeneratorDiagnostics.Should().BeEmpty();
        result.GeneratedSources.Should().BeEmpty();
    }

    [TestMethod]
    public void Generate_CollidingMemberNames_ReportsDiagnosticAndEmitsValidEntry()
    {
        const string resource = """
            <root>
              <data name="Greeting-Text"><value>First</value></data>
              <data name="Greeting_Text"><value>Second</value></data>
            </root>
            """;

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(resource));

        result.GeneratorDiagnostics.Should().ContainSingle()
            .Which.Id.Should().Be("CLIRESX0004");
        result.CompilerErrors.Should().BeEmpty();
        result.SingleSource.Should().Contain("public static string @Greeting_Text", Exactly.Once());
    }

    [TestMethod]
    [DataRow("Strings")]
    [DataRow("__ResourceCache")]
    [DataRow("__ResourceManagerCache")]
    [DataRow("CreateResourceManager")]
    [DataRow("Culture")]
    [DataRow("GetCachedResourceString")]
    [DataRow("GetResourceString")]
    [DataRow("ResourceManager")]
    [DataRow("s_cache")]
    [DataRow("s_resourceManager")]
    public void Generate_InfrastructureMemberName_ReportsDiagnostic(string resourceName)
    {
        string resource = $"<root><data name=\"{resourceName}\"><value>Value</value></data></root>";

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(resource));

        result.GeneratorDiagnostics.Should().ContainSingle()
            .Which.Id.Should().Be("CLIRESX0004");
        result.CompilerErrors.Should().BeEmpty();
    }

    [TestMethod]
    public void Generate_FormatArgumentsMatchingMembers_QualifiesGeneratedAccesses()
    {
        const string resource = """
            <root>
              <data name="Greeting"><value>Hello {Greeting} from {Culture}</value></data>
            </root>
            """;
        Dictionary<string, string> metadata = new(StringComparer.Ordinal)
        {
            ["EmitFormatMethods"] = "true"
        };

        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(resource, metadata: metadata));

        result.GeneratorDiagnostics.Should().BeEmpty();
        result.CompilerErrors.Should().BeEmpty();
        result.SingleSource.Should().Contain("global::@Test.@Resources.@Strings.Culture");
        result.SingleSource.Should().Contain("global::@Test.@Resources.@Strings.@Greeting.Replace");
    }

    [TestMethod]
    public void Generate_DuplicateClass_ReportsDiagnosticAndEmitsNothing()
    {
        GeneratorTestResult result = GeneratorTestHarness.Run(
            GeneratorTestResource.Selected(SimpleResource, path: "First/Strings.resx"),
            GeneratorTestResource.Selected(SimpleResource, path: "Second/Strings.resx"));

        result.GeneratorDiagnostics.Should().ContainSingle()
            .Which.Id.Should().Be("CLIRESX0005");
        result.GeneratedSources.Should().BeEmpty();
    }

    [TestMethod]
    public void Generate_UnrelatedSourceChange_KeepsResourceOutputCached()
    {
        ImmutableArray<IncrementalStepRunReason> reasons =
            GeneratorTestHarness.RunAfterUnrelatedSourceChange(
                GeneratorTestResource.Selected(SimpleResource));

        reasons.Should().NotBeEmpty();
        reasons.Should().OnlyContain(static reason => reason == IncrementalStepRunReason.Cached);
    }

    [TestMethod]
    public void Generate_OneResourceChanges_KeepsOtherResourceCached()
    {
        Dictionary<string, string> otherMetadata = new(StringComparer.Ordinal)
        {
            ["ClassName"] = "Test.Resources.Other",
            ["ManifestResourceName"] = "Test.Resources.Other",
            ["CliResourceFamily"] = "Test.Resources.Other"
        };
        GeneratorTestResource changedResource = GeneratorTestResource.Selected(SimpleResource);
        GeneratorTestResource unchangedResource = GeneratorTestResource.Selected(
            SimpleResource,
            path: "Resources/Other.resx",
            metadata: otherMetadata);
        const string changedContent = """
            <root>
              <data name="Greeting"><value>Changed</value></data>
            </root>
            """;

        ImmutableArray<IncrementalStepRunReason> reasons = GeneratorTestHarness.RunAfterOneResourceChanges(
            changedResource,
            changedContent,
            unchangedResource);

        reasons.Should().ContainSingle(static reason => reason == IncrementalStepRunReason.Modified);
        reasons.Should().ContainSingle(static reason => reason == IncrementalStepRunReason.Cached);
    }

    private static string CreateSizedResource(int length)
    {
        const string Prefix = "<root><!--";
        const string Suffix = "--><data name=\"Greeting\"><value>Hello</value></data></root>";
        int paddingLength = length - Prefix.Length - Suffix.Length;
        if (paddingLength < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(length));
        }

        return Prefix + new string('x', paddingLength) + Suffix;
    }

    private static string CreateResourceWithEntries(int count)
    {
        StringBuilder builder = new("<root>");
        for (int index = 0; index < count; index++)
        {
            builder.Append("<data name=\"Key")
                .Append(index)
                .Append("\"><value>Value</value></data>");
        }

        return builder.Append("</root>").ToString();
    }

    private static string CreateResourceWithFormatArguments(int count, bool named)
    {
        StringBuilder builder = new("<root><data name=\"Value\"><value>");
        for (int index = 0; index < count; index++)
        {
            builder.Append('{');
            if (named)
            {
                builder.Append('p');
            }

            builder.Append(index).Append('}');
        }

        return builder.Append("</value></data></root>").ToString();
    }

    private static int GetGeneratorConstant(string typeName, string fieldName)
    {
        Type type = typeof(ResxSourceGenerator).Assembly.GetType(
            "Microsoft.DotNet.Cli.Resources.Generator." + typeName,
            throwOnError: false)
            ?? throw new InvalidOperationException($"Generator type '{typeName}' was not found.");
        System.Reflection.FieldInfo field = type.GetField(
            fieldName,
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Static)
            ?? throw new InvalidOperationException($"Generator field '{fieldName}' was not found.");
        object? value = field.GetRawConstantValue();
        return value is int result
            ? result
            : throw new InvalidOperationException($"Generator field '{fieldName}' is not an int constant.");
    }

    private static void AssertAggressiveInlining(IMethodSymbol method)
    {
        AttributeData attribute = method.GetAttributes().Single(attribute =>
            attribute.AttributeClass?.ToDisplayString()
                == "System.Runtime.CompilerServices.MethodImplAttribute");
        object? value = attribute.ConstructorArguments.Single().Value;

        Convert.ToInt32(value, System.Globalization.CultureInfo.InvariantCulture)
            .Should().Be((int)MethodImplOptions.AggressiveInlining);
    }
}