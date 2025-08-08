// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Runtime.CompilerServices;

namespace Microsoft.NET.Sdk.Razor.Tests
{
    public class BuildWithComponentsIntegrationTest : AspNetSdkTest
    {
        public BuildWithComponentsIntegrationTest(ITestOutputHelper log) : base(log) { }

        [CoreMSBuildOnlyFact]
        public void Build_Components_WithDotNetCoreMSBuild_Works() => Build_ComponentsWorks();

        [RequiresMSBuildVersionFact("17.10.0.8101")]
        public void Build_Components_WithDesktopMSBuild_Works() => Build_ComponentsWorks();

        [Fact]
        public void Building_NetstandardComponentLibrary()
        {
            var testAsset = "RazorComponentLibrary";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            // Build
            var build = new BuildCommand(projectDirectory);
            build.Execute().Should().Pass();

            string outputPath = build.GetOutputDirectory("netstandard2.0").ToString();

            new FileInfo(Path.Combine(outputPath, "ComponentLibrary.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "ComponentLibrary.pdb")).Should().Exist();

            new FileInfo(Path.Combine(outputPath, "ComponentLibrary.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "ComponentLibrary.Views.pdb")).Should().NotExist();
        }

        private void Build_ComponentsWorks([CallerMemberName] string callerName = "")
        {
            var testAsset = "RazorMvcWithComponents";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset, callerName);

            var build = new BuildCommand(projectDirectory);
            build.Execute("/p:BuildWithNetFrameworkHostedCompiler=true").Should().Pass();

            string outputPath = build.GetOutputDirectory(DefaultTfm).ToString();

            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.dll")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.pdb")).Should().Exist();
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.Views.dll")).Should().NotExist();
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.Views.pdb")).Should().NotExist();

            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.dll")).AssemblyShould().ContainType("MvcWithComponents.TestComponent");
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.dll")).AssemblyShould().ContainType("MvcWithComponents.Views.Shared.NavMenu");

            // Components should appear in the app assembly.
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.dll")).AssemblyShould().ContainType("MvcWithComponents.Components.Counter");
            // Views should also appear in the app assembly.
            new FileInfo(Path.Combine(outputPath, "MvcWithComponents.dll")).AssemblyShould().ContainType("AspNetCoreGeneratedDocument.Views_Home_Index");
        }

        [Fact]
        public void Build_ComponentApp_IncludesEmbeddedValidatableTypeAttributeForNet100()
        {
            var testAsset = "RazorComponentApp";
            var projectDirectory = CreateAspNetSdkTestAsset(testAsset);

            // Add a target to introspect the properties and compile items
            var projectFile = Path.Combine(projectDirectory.Path, "ComponentApp.csproj");
            var originalContent = File.ReadAllText(projectFile);
            var modifiedContent = originalContent.Replace("</Project>", 
                @"  <Target Name=""_IntrospectValidatableTypeAttribute"" BeforeTargets=""Build"">
    <Message Text=""_HasRazorFiles: $(_HasRazorFiles)"" Importance=""High"" />
    <Message Text=""GenerateEmbeddedValidatableTypeAttribute: $(GenerateEmbeddedValidatableTypeAttribute)"" Importance=""High"" />
    <Message Text=""IncludeEmbeddedValidationGlobalUsing: $(IncludeEmbeddedValidationGlobalUsing)"" Importance=""High"" />
    <Message Text=""_TargetingNET100OrLater: $(_TargetingNET100OrLater)"" Importance=""High"" />
    <Message Text=""Compile items containing ValidatableTypeAttribute: @(Compile->WithMetadataValue('Filename', 'ValidatableTypeAttribute'))"" Importance=""High"" />
    <Message Text=""Compile items containing EmbeddedAttribute: @(Compile->WithMetadataValue('Filename', 'EmbeddedAttribute'))"" Importance=""High"" />
    <Message Text=""Using items: @(Using)"" Importance=""High"" />
  </Target>
</Project>");
            
            File.WriteAllText(projectFile, modifiedContent);

            // Build with .NET 10.0 target framework
            var build = new BuildCommand(projectDirectory);
            var result = build.Execute($"/p:TargetFramework=net10.0");
            
            result.Should().Pass();

            // Check that the properties were set correctly
            result.Should().HaveStdOutContaining("_HasRazorFiles: true");
            result.Should().HaveStdOutContaining("GenerateEmbeddedValidatableTypeAttribute: true");
            result.Should().HaveStdOutContaining("IncludeEmbeddedValidationGlobalUsing: true");
            result.Should().HaveStdOutContaining("_TargetingNET100OrLater: true");
            
            // Check that the source files were included
            result.Should().HaveStdOutContaining("ValidatableTypeAttribute");
            result.Should().HaveStdOutContaining("EmbeddedAttribute");
            
            // Check that the global using was included
            result.Should().HaveStdOutContaining("Microsoft.Extensions.Validation.Embedded");
        }
    }
}
