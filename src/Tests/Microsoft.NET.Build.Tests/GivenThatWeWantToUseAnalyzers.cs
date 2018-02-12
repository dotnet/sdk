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
        [InlineData("F#", "AppWithLibraryFS")]
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
                })
                .Restore(Log, relativePath: "TestApp");

            var command = new GetValuesCommand(
                Log,
                Path.Combine(asset.Path, "TestApp"),
                "netcoreapp1.1",
                "Analyzer",
                GetValuesCommand.ValueType.Item);

            command.Execute().Should().Pass();

            var analyzers = command.GetValues();

            IEnumerable<string> NuGetCachePaths(params string[] relativePaths)
                => relativePaths.Select(
                    r => Path.Combine(TestContext.Current.NuGetCachePath, r.Replace('/', Path.DirectorySeparatorChar)));

            switch (language)
            {
                case "C#":
                    analyzers.Should().BeEquivalentTo(
                        NuGetCachePaths(
                            "microsoft.codeanalysis.analyzers/1.1.0/analyzers/dotnet/cs/Microsoft.CodeAnalysis.Analyzers.dll",
                            "microsoft.codeanalysis.analyzers/1.1.0/analyzers/dotnet/cs/Microsoft.CodeAnalysis.CSharp.Analyzers.dll",
                            "microsoft.codequality.analyzers/2.6.0/analyzers/dotnet/cs/Microsoft.CodeQuality.Analyzers.dll",
                            "microsoft.codequality.analyzers/2.6.0/analyzers/dotnet/cs/Microsoft.CodeQuality.CSharp.Analyzers.dll",
                            "microsoft.dependencyvalidation.analyzers/0.9.0/analyzers/dotnet/Microsoft.DependencyValidation.Analyzers.dll"
                            ));
                    break;

                case "VB":
                    analyzers.Should().BeEquivalentTo(
                        NuGetCachePaths(
                            "microsoft.codeanalysis.analyzers/1.1.0/analyzers/dotnet/vb/Microsoft.CodeAnalysis.Analyzers.dll",
                            "microsoft.codeanalysis.analyzers/1.1.0/analyzers/dotnet/vb/Microsoft.CodeAnalysis.VisualBasic.Analyzers.dll",
                            "microsoft.codequality.analyzers/2.6.0/analyzers/dotnet/vb/Microsoft.CodeQuality.Analyzers.dll",
                            "microsoft.codequality.analyzers/2.6.0/analyzers/dotnet/vb/Microsoft.CodeQuality.VisualBasic.Analyzers.dll",
                            "microsoft.dependencyvalidation.analyzers/0.9.0/analyzers/dotnet/Microsoft.DependencyValidation.Analyzers.dll"
                            ));
                    break;

                case "F#":
                    analyzers.Should().BeEmpty();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(language));
            }
        }
    }
}
