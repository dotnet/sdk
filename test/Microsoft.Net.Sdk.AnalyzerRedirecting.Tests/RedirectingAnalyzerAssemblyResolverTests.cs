// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.Net.Sdk.AnalyzerRedirecting.Tests;

public class RedirectingAnalyzerAssemblyResolverTests(ITestOutputHelper log) : SdkTest(log)
{
    [Fact]
    public void Assembly_SameMajorVersion()
    {
        TestDirectory testDir = _testAssetsManager.CreateTestDirectory(identifier: "RuntimeAnalyzers");

        var vsDir = Path.Combine(testDir.Path, "vs");
        var vsAnalyzerPath = CompileDll(vsDir, @"AspNetCoreAnalyzers\9.0.0-preview.5.24306.11\analyzers\dotnet\cs", "Microsoft.AspNetCore.App.Analyzers", "9.1.2");

        var sdkDir = Path.Combine(testDir.Path, "sdk");
        var sdkAnalyzerPath = CompileDll(sdkDir, @"packs\Microsoft.AspNetCore.App.Ref\9.0.0-preview.7.24406.2\analyzers\dotnet\cs", "Microsoft.AspNetCore.App.Analyzers", "9.3.4");
        var sdkAnalyzerName = AssemblyName.GetAssemblyName(sdkAnalyzerPath);

        var resolver = new RedirectingAnalyzerAssemblyResolver(vsDir);
        var resolved = resolver.ResolveAssembly(sdkAnalyzerName, Path.GetDirectoryName(sdkAnalyzerPath));
        resolved.Location.Should().Be(vsAnalyzerPath);
    }

    [Fact]
    public void Assembly_DifferentMajorVersion()
    {
        TestDirectory testDir = _testAssetsManager.CreateTestDirectory(identifier: "RuntimeAnalyzers");

        var vsDir = Path.Combine(testDir.Path, "vs");
        CompileDll(vsDir, @"AspNetCoreAnalyzers\8.0.100\analyzers\dotnet\cs", "Microsoft.AspNetCore.App.Analyzers", "8.0.1");

        var sdkDir = Path.Combine(testDir.Path, "sdk");
        var sdkAnalyzerPath = CompileDll(sdkDir, @"packs\Microsoft.AspNetCore.App.Ref\9.0.0-preview.7.24406.2\analyzers\dotnet\cs", "Microsoft.AspNetCore.App.Analyzers", "9.3.4");
        var sdkAnalyzerName = AssemblyName.GetAssemblyName(sdkAnalyzerPath);

        var resolver = new RedirectingAnalyzerAssemblyResolver(vsDir);
        var resolved = resolver.ResolveAssembly(sdkAnalyzerName, Path.GetDirectoryName(sdkAnalyzerPath));
        resolved.Should().BeNull();
    }

    [Fact]
    public void Path_SameMajorVersion()
    {
        TestDirectory testDir = _testAssetsManager.CreateTestDirectory(identifier: "RuntimeAnalyzers");

        var vsDir = Path.Combine(testDir.Path, "vs");
        var vsAnalyzerPath = CompileDll(vsDir, @"AspNetCoreAnalyzers\9.0.0-preview.5.24306.11\analyzers\dotnet\cs", "Microsoft.AspNetCore.App.Analyzers", "9.1.2");
        var sdkAnalyzerPath = CompileDll(testDir.Path, @"sdk\packs\Microsoft.AspNetCore.App.Ref\9.0.0-preview.7.24406.2\analyzers\dotnet\cs", "Microsoft.AspNetCore.App.Analyzers", "9.3.4");

        var resolver = new RedirectingAnalyzerAssemblyResolver(vsDir);
        var redirected = resolver.RedirectPath(sdkAnalyzerPath);
        redirected.Should().Be(vsAnalyzerPath);
    }

    [Fact]
    public void Path_DifferentMajorVersion()
    {
        TestDirectory testDir = _testAssetsManager.CreateTestDirectory(identifier: "RuntimeAnalyzers");

        var vsDir = Path.Combine(testDir.Path, "vs");
        CompileDll(vsDir, @"AspNetCoreAnalyzers\8.0.100\analyzers\dotnet\cs", "Microsoft.AspNetCore.App.Analyzers", "8.0.1");
        var sdkAnalyzerPath = CompileDll(testDir.Path, @"sdk\packs\Microsoft.AspNetCore.App.Ref\9.0.0-preview.7.24406.2\analyzers\dotnet\cs", "Microsoft.AspNetCore.App.Analyzers", "9.3.4");

        var resolver = new RedirectingAnalyzerAssemblyResolver(vsDir);
        var redirected = resolver.RedirectPath(sdkAnalyzerPath);
        redirected.Should().BeNull();
    }

    private static string CompileDll(string root, string subdir, string name, string version)
    {
        var dllPath = Path.Combine(root, subdir, $"{name}.dll");
        Directory.CreateDirectory(Path.GetDirectoryName(dllPath));

        var comp = CSharpCompilation.Create(
            name,
            [
                CSharpSyntaxTree.ParseText($$"""
                    using System.Reflection;
                    [assembly: AssemblyTitle("{{name}}")]
                    [assembly: AssemblyVersion("{{version}}")]
                    """),
            ],
            [
                MetadataReference.CreateFromFile(typeof(AssemblyTitleAttribute).Assembly.Location),
            ],
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        var result = comp.Emit(dllPath);
        result.Diagnostics.Should().BeEmpty();
        result.Success.Should().BeTrue();

        return dllPath;
    }
}
