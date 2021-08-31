// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.IO;
using Microsoft.NET.TestFramework;
using Microsoft.NET.TestFramework.Assertions;
using Microsoft.NET.TestFramework.Commands;
using Xunit;
using System.Linq;
using FluentAssertions;
using System.Xml.Linq;
using System.Collections.Generic;
using System;
using Xunit.Abstractions;
using System.Runtime.InteropServices;
using Microsoft.NET.TestFramework.ProjectConstruction;

namespace Microsoft.NET.Build.Tests
{
    public class GivenThatWeWantToUseAnalyzers : SdkTest
    {
        public GivenThatWeWantToUseAnalyzers(ITestOutputHelper log) : base(log)
        {
        }

        [Theory]
        [InlineData("C#", "AppWithLibrary")]
        [InlineData("VB", "AppWithLibraryVB")]
        [InlineData("F#", "AppWithLibraryFS", Skip = "https://github.com/dotnet/coreclr/issues/27275")]
        public void It_resolves_analyzers_correctly(string language, string testAssetName)
        {
            var asset = _testAssetsManager
                .CopyTestAsset(testAssetName, identifier: language)
                .WithSource()
                .WithProjectChanges(project =>
                {
                    var ns = project.Root.Name.Namespace;
                    project.Root.Add(
                        new XElement(ns + "ItemGroup",
                            new XElement(ns + "PackageReference",
                                new XAttribute("Include", "Microsoft.DependencyValidation.Analyzers"),
                                new XAttribute("Version", "0.9.0")),
                            new XElement(ns + "PackageReference",
                                new XAttribute("Include", "Microsoft.CodeQuality.Analyzers"),
                                new XAttribute("Version", "2.6.0"))));
                });

            var command = new GetValuesCommand(
                Log,
                Path.Combine(asset.Path, "TestApp"),
                "netcoreapp1.1",
                "Analyzer",
                GetValuesCommand.ValueType.Item);

            command.Execute().Should().Pass();

            var analyzers = command.GetValues();

            switch (language)
            {
                case "C#":
                    analyzers.Select(RelativeNuGetPath).Should().BeEquivalentTo(
                        "microsoft.codeanalysis.analyzers/1.1.0/analyzers/dotnet/cs/Microsoft.CodeAnalysis.Analyzers.dll",
                        "microsoft.codeanalysis.analyzers/1.1.0/analyzers/dotnet/cs/Microsoft.CodeAnalysis.CSharp.Analyzers.dll",
                        "microsoft.codequality.analyzers/2.6.0/analyzers/dotnet/cs/Microsoft.CodeQuality.Analyzers.dll",
                        "microsoft.codequality.analyzers/2.6.0/analyzers/dotnet/cs/Microsoft.CodeQuality.CSharp.Analyzers.dll",
                        "microsoft.dependencyvalidation.analyzers/0.9.0/analyzers/dotnet/Microsoft.DependencyValidation.Analyzers.dll"
                        );
                    break;

                case "VB":
                    analyzers.Select(RelativeNuGetPath).Should().BeEquivalentTo(
                        "microsoft.codeanalysis.analyzers/1.1.0/analyzers/dotnet/vb/Microsoft.CodeAnalysis.Analyzers.dll",
                        "microsoft.codeanalysis.analyzers/1.1.0/analyzers/dotnet/vb/Microsoft.CodeAnalysis.VisualBasic.Analyzers.dll",
                        "microsoft.codequality.analyzers/2.6.0/analyzers/dotnet/vb/Microsoft.CodeQuality.Analyzers.dll",
                        "microsoft.codequality.analyzers/2.6.0/analyzers/dotnet/vb/Microsoft.CodeQuality.VisualBasic.Analyzers.dll",
                        "microsoft.dependencyvalidation.analyzers/0.9.0/analyzers/dotnet/Microsoft.DependencyValidation.Analyzers.dll"
                        );
                    break;

                case "F#":
                    analyzers.Should().BeEmpty();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(language));
            }
        }

        [Fact]
        public void It_resolves_multitargeted_analyzers()
        {
            var testProject = new TestProject()
            {
                TargetFrameworks = "net6.0;net472"
            };

            //  Disable analyzers built in to the SDK so we can more easily test the ones coming from NuGet packages
            testProject.AdditionalProperties["EnableNETAnalyzers"] = "false";

            testProject.ProjectChanges.Add(project =>
            {
                var ns = project.Root.Name.Namespace;

                var itemGroup = XElement.Parse(@"
  <ItemGroup>
    <PackageReference Include=""System.Text.Json"" Version=""4.7.0"" Condition="" '$(TargetFramework)' == 'net472' "" />
    <PackageReference Include=""System.Text.Json"" Version=""6.0.0-preview.4.21253.7"" Condition="" '$(TargetFramework)' == 'net6.0' "" />
  </ItemGroup>");

                project.Root.Add(itemGroup);
            });

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            List<string> GetAnalyzersForTargetFramework(string targetFramework)
            {
                var getValuesCommand = new GetValuesCommand(testAsset,
                    valueName: "Analyzer",
                    GetValuesCommand.ValueType.Item,
                    targetFramework);

                getValuesCommand.DependsOnTargets = "ResolveLockFileAnalyzers";

                getValuesCommand.Execute("-p:TargetFramework=" + targetFramework).Should().Pass();

                return getValuesCommand.GetValues().Select(RelativeNuGetPath).ToList();
            }
            
            GetAnalyzersForTargetFramework("net6.0").Should().BeEquivalentTo("system.text.json/6.0.0-preview.4.21253.7/analyzers/dotnet/cs/System.Text.Json.SourceGeneration.dll");
            GetAnalyzersForTargetFramework("net472").Should().BeEmpty();
        }

        [Theory]
        [InlineData("roslyn3.9")]
        [InlineData("roslyn4.0")]
        public void It_resolves_analyzers_tareting_mulitple_roslyn_versions(string compilerApiVersion)
        {
            var testProject = new TestProject()
            {
                TargetFrameworks = "netstandard2.0"
            };

            //  Disable analyzers built in to the SDK so we can more easily test the ones coming from NuGet packages
            testProject.AdditionalProperties["EnableNETAnalyzers"] = "false";

            testProject.ProjectChanges.Add(project =>
            {
                var propertyGroup = XElement.Parse($@"
  <PropertyGroup>
    <CompilerApiVersion>{compilerApiVersion}</CompilerApiVersion>
  </PropertyGroup>");

                project.Root.Add(propertyGroup);

                var itemGroup = XElement.Parse(@"
  <ItemGroup>
    <PackageReference Include=""Library.ContainsAnalyzer"" Version=""1.0.0"" />
    <PackageReference Include=""Library.ContainsAnalyzer2"" Version=""1.0.0"" />
  </ItemGroup>");

                project.Root.Add(itemGroup);
            });

            var testAsset = _testAssetsManager.CreateTestProject(testProject);

            NuGetConfigWriter.Write(testAsset.Path, TestContext.Current.TestPackages);

            var command = new GetValuesCommand(testAsset,
                "Analyzer",
                GetValuesCommand.ValueType.Item);

            command.Execute().Should().Pass();

            var analyzers = command.GetValues();

            switch (compilerApiVersion)
            {
                case "roslyn3.9":
                    analyzers.Select(RelativeNuGetPath).Should().BeEquivalentTo(
                        "library.containsanalyzer/1.0.0/analyzers/dotnet/roslyn3.9/cs/Library.ContainsAnalyzer.dll",
                        "library.containsanalyzer2/1.0.0/analyzers/dotnet/roslyn3.8/cs/Library.ContainsAnalyzer2.dll"
                        );
                    break;

                case "roslyn4.0":
                    analyzers.Select(RelativeNuGetPath).Should().BeEquivalentTo(
                        "library.containsanalyzer/1.0.0/analyzers/dotnet/roslyn4.0/cs/Library.ContainsAnalyzer.dll",
                        "library.containsanalyzer2/1.0.0/analyzers/dotnet/roslyn3.10/cs/Library.ContainsAnalyzer2.dll"
                        );
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(compilerApiVersion));
            }
        }

        static readonly List<string> nugetRoots = new List<string>()
            {
                TestContext.Current.NuGetCachePath,
                Path.Combine(FileConstants.UserProfileFolder, ".dotnet", "NuGetFallbackFolder")
            };

        static string RelativeNuGetPath(string absoluteNuGetPath)
        {
            foreach (var nugetRoot in nugetRoots)
            {
                if (absoluteNuGetPath.StartsWith(nugetRoot + Path.DirectorySeparatorChar))
                {
                    return absoluteNuGetPath.Substring(nugetRoot.Length + 1)
                                .Replace(Path.DirectorySeparatorChar, '/');
                }
            }
            throw new InvalidDataException("Expected path to be under a NuGet root: " + absoluteNuGetPath);
        }
    }
}
