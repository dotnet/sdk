// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

#nullable enable

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using Microsoft.Extensions.DependencyModel;
using Microsoft.Extensions.DependencyModel.Resolution;
using Xunit;

namespace Microsoft.NET.Sdk.Razor.SourceGenerators
{
    public class RazorSourceGeneratorTests
    {
        private static readonly Project _baseProject = CreateBaseProject();

        [Fact]
        public async Task SourceGenerator_RazorFiles_Works()
        {
            // Arrange
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
            });

            var compilation = await project.GetCompilationAsync();
            var driver = await GetDriverAsync(project);

            var result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Single(result.GeneratedSources);
        }

        [Fact]
        public async Task SourceGenerator_DoesNotAddAnyGeneratedSources_WhenSourceGeneratorIsSuppressed()
        {
            // Regression test for https://github.com/dotnet/aspnetcore/issues/36227
            // Arrange
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
                ["Pages/Counter.razor"] = "<h1>Counter</h1>",
            });

            var compilation = await project.GetCompilationAsync();
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project, optionsProvider =>
            {
                optionsProvider.TestGlobalOptions["build_property.SuppressRazorSourceGenerator"] = "true";
            });

            var result = RunGenerator(compilation!, ref driver);
            Assert.Empty(result.Diagnostics);
            Assert.Empty(result.GeneratedSources);

            var updatedText = new TestAdditionalText("Pages/Index.razor", SourceText.From(@"<h1>Hello world 1</h1>", Encoding.UTF8));
            driver = driver.ReplaceAdditionalText(additionalTexts.First(f => f.Path == updatedText.Path), updatedText);

            // Now run the source generator again with updated text that should result in a cache miss
            // and exercise comparers
            result = RunGenerator(compilation!, ref driver);

            Assert.Empty(result.Diagnostics);
            Assert.Empty(result.GeneratedSources);
        }

        [Fact]
        public async Task SourceGenerator_CorrectlyGeneratesSourcesOnceSuppressRazorSourceGeneratorIsUnset()
        {
            // Regression test for https://github.com/dotnet/aspnetcore/issues/36227
            // Arrange
            var project = CreateTestProject(new()
            {
                ["Pages/Index.razor"] = "<h1>Hello world</h1>",
                ["Pages/Counter.razor"] =
@"
@using Microsoft.AspNetCore.Components
@using Microsoft.AspNetCore.Components.Web
<h1>Counter</h1>
<button @onclick=""IncrementCount"">Click me</button>",
            });

            var compilation = await project.GetCompilationAsync();
            TestAnalyzerConfigOptionsProvider? testOptionsProvider = null;
            var (driver, additionalTexts) = await GetDriverWithAdditionalTextAsync(project, optionsProvider =>
            {
                testOptionsProvider = optionsProvider;
                optionsProvider.TestGlobalOptions["build_property.SuppressRazorSourceGenerator"] = "true";
            });

            var result = RunGenerator(compilation!, ref driver);
            Assert.Empty(result.Diagnostics);
            Assert.Empty(result.GeneratedSources);


            var updatedOptionsProvider = new TestAnalyzerConfigOptionsProvider();
            foreach (var option in testOptionsProvider!.AdditionalTextOptions)
            {
                updatedOptionsProvider.AdditionalTextOptions[option.Key] = option.Value;
            }

            foreach (var option in testOptionsProvider!.TestGlobalOptions.Options)
            {
                updatedOptionsProvider.TestGlobalOptions[option.Key] = option.Value;
            }

            updatedOptionsProvider.TestGlobalOptions["build_property.SuppressRazorSourceGenerator"] = "false";

            driver = driver.WithUpdatedAnalyzerConfigOptions(updatedOptionsProvider);
            result = RunGenerator(compilation!, ref driver);

            Assert.Collection(
                result.GeneratedSources,
                sourceResult =>
                {
                    Assert.Contains("public partial class Index", sourceResult.SourceText.ToString());
                },
                sourceResult =>
                {
                    var sourceText = sourceResult.SourceText.ToString();
                    Assert.Contains("public partial class Counter", sourceText);
                    // Regression test for https://github.com/dotnet/aspnetcore/issues/36116. Verify that @onclick is resolved as a component, and not as a regular attribute
                    Assert.Contains("__builder.AddAttribute(2, \"onclick\", Microsoft.AspNetCore.Components.EventCallback.Factory.Create<Microsoft.AspNetCore.Components.Web.MouseEventArgs>(this,", sourceText);
                });
        }

        private static async ValueTask<GeneratorDriver> GetDriverAsync(Project project)
        {
            var (driver, _) = await GetDriverWithAdditionalTextAsync(project);
            return driver;
        }

        private static async ValueTask<(GeneratorDriver, ImmutableArray<AdditionalText>)> GetDriverWithAdditionalTextAsync(Project project, Action<TestAnalyzerConfigOptionsProvider>? configureGlobalOptions = null)
        {
            var razorSourceGenerator = new RazorSourceGenerator().AsSourceGenerator();
            var driver = (GeneratorDriver)CSharpGeneratorDriver.Create(new[] { razorSourceGenerator }, parseOptions: (CSharpParseOptions)project.ParseOptions!);

            var optionsProvider = GetDefaultOptionsProvider();

            configureGlobalOptions?.Invoke(optionsProvider);

            var additionalTexts = ImmutableArray<AdditionalText>.Empty;

            foreach (var document in project.AdditionalDocuments)
            {
                var additionalText = new TestAdditionalText(document.Name, await document.GetTextAsync());
                additionalTexts = additionalTexts.Add(additionalText);

                var additionalTextOptions = new TestAnalyzerConfigOptions
                {
                    ["build_metadata.AdditionalFiles.TargetPath"] = Convert.ToBase64String(Encoding.UTF8.GetBytes(additionalText.Path)),
                };

                optionsProvider.AdditionalTextOptions[additionalText.Path] = additionalTextOptions;
            }

            driver = driver
                .AddAdditionalTexts(additionalTexts)
                .WithUpdatedAnalyzerConfigOptions(optionsProvider);

            return (driver, additionalTexts);
        }

        private static TestAnalyzerConfigOptionsProvider GetDefaultOptionsProvider()
        {
            var optionsProvider = new TestAnalyzerConfigOptionsProvider();
            optionsProvider.TestGlobalOptions["build_property.RazorConfiguration"] = "Default";
            optionsProvider.TestGlobalOptions["build_property.RootNamespace"] = "MyApp";
            optionsProvider.TestGlobalOptions["build_property.RazorLangVersion"] = "Latest";
            return optionsProvider;
        }

        private static GeneratorRunResult RunGenerator(Compilation compilation, ref GeneratorDriver driver)
        {
            driver = driver.RunGenerators(compilation);

            var result = driver.RunGenerators(compilation).GetRunResult();
            return result.Results[0];
        }

        private static Project CreateTestProject(
            Dictionary<string, string> additonalSources,
            Dictionary<string, string>? sources = null)
        {
            var project = _baseProject;

            if (sources is not null)
            {
                foreach (var (name, source) in sources)
                {
                    project = project.AddDocument(name, source).Project;
                }
            }

            foreach (var (name, source) in additonalSources)
            {
                project = project.AddAdditionalDocument(name, source).Project;
            }

            return project;
        }

        private class AppLocalResolver : ICompilationAssemblyResolver
        {
            public bool TryResolveAssemblyPaths(CompilationLibrary library, List<string> assemblies)
            {
                foreach (var assembly in library.Assemblies)
                {
                    var dll = Path.Combine(Directory.GetCurrentDirectory(), "refs", Path.GetFileName(assembly));
                    if (File.Exists(dll))
                    {
                        assemblies.Add(dll);
                        return true;
                    }

                    dll = Path.Combine(Directory.GetCurrentDirectory(), Path.GetFileName(assembly));
                    if (File.Exists(dll))
                    {
                        assemblies.Add(dll);
                        return true;
                    }
                }

                return false;
            }
        }

        private static Project CreateBaseProject()
        {
            var projectId = ProjectId.CreateNewId(debugName: "TestProject");

            var solution = new AdhocWorkspace()
               .CurrentSolution
               .AddProject(projectId, "TestProject", "TestProject", LanguageNames.CSharp);

            var project = solution.Projects.Single()
                .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                .WithNullableContextOptions(NullableContextOptions.Enable));

            project = project.WithParseOptions(((CSharpParseOptions)project.ParseOptions!).WithLanguageVersion(LanguageVersion.Preview));


            foreach (var defaultCompileLibrary in DependencyContext.Load(typeof(RazorSourceGeneratorTests).Assembly).CompileLibraries)
            {
                foreach (var resolveReferencePath in defaultCompileLibrary.ResolveReferencePaths(new AppLocalResolver()))
                {
                    project = project.AddMetadataReference(MetadataReference.CreateFromFile(resolveReferencePath));
                }
            }

            // The deps file in the project is incorrect and does not contain "compile" nodes for some references.
            // However these binaries are always present in the bin output. As a "temporary" workaround, we'll add
            // every dll file that's present in the test's build output as a metadatareference.
            foreach (var assembly in Directory.EnumerateFiles(AppContext.BaseDirectory, "*.dll"))
            {
                if (!project.MetadataReferences.Any(c => string.Equals(Path.GetFileNameWithoutExtension(c.Display), Path.GetFileNameWithoutExtension(assembly), StringComparison.OrdinalIgnoreCase)))
                {
                    project = project.AddMetadataReference(MetadataReference.CreateFromFile(assembly));
                }
            }

            return project;
        }

        private class TestAnalyzerConfigOptionsProvider : AnalyzerConfigOptionsProvider
        {
            public override AnalyzerConfigOptions GlobalOptions => TestGlobalOptions;

            public TestAnalyzerConfigOptions TestGlobalOptions { get; } = new TestAnalyzerConfigOptions();

            public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => throw new NotImplementedException();

            public Dictionary<string, TestAnalyzerConfigOptions> AdditionalTextOptions { get; } = new();

            public override AnalyzerConfigOptions GetOptions(AdditionalText textFile)
            {
                return AdditionalTextOptions.TryGetValue(textFile.Path, out var options) ? options : new TestAnalyzerConfigOptions();
            }
        }

        private class TestAnalyzerConfigOptions : AnalyzerConfigOptions
        {
            public Dictionary<string, string> Options { get; } = new();

            public string this[string name]
            {
                get => Options[name];
                set => Options[name] = value;
            }

            public override bool TryGetValue(string key, [NotNullWhen(true)] out string? value)
                => Options.TryGetValue(key, out value);
        }
    }
}
