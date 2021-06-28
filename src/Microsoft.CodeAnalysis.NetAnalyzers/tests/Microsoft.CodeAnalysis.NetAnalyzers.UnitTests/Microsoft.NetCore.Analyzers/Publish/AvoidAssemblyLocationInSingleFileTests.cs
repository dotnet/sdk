// Copyright (c) Microsoft.  All Rights Reserved.  Licensed under the Apache License, Version 2.0.  See License.txt in the project root for license information.

using System;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.Testing;
using Xunit;
using VerifyCS = Test.Utilities.CSharpCodeFixVerifier<
    Microsoft.NetCore.Analyzers.Publish.AvoidAssemblyLocationInSingleFile,
    Microsoft.CodeAnalysis.Testing.EmptyCodeFixProvider>;
using static Analyzer.Utilities.MSBuildPropertyOptionNames;

namespace Microsoft.NetCore.Analyzers.Publish.UnitTests
{
    public class AvoidAssemblyLocationInSingleFileTests
    {
        [Theory]
        [CombinatorialData]
        public Task GetExecutingAssemblyLocation(
            [CombinatorialValues(true, false, null)] bool? publish,
            [CombinatorialValues(true, false, null)] bool? includeContent)
        {
            const string source = @"
using System.Reflection;
class C
{
    public string M() => Assembly.GetExecutingAssembly().Location;
}";
            string analyzerConfig = "";
            if (publish is not null)
            {
                analyzerConfig += $"build_property.{PublishSingleFile} = {publish}" + Environment.NewLine;
            }
            if (includeContent is not null)
            {
                analyzerConfig += $"build_property.{IncludeAllContentForSelfExtract} = {includeContent}";
            }

            var test = new VerifyCS.Test
            {
                TestCode = source,
                AnalyzerConfigDocument = analyzerConfig
            };

            DiagnosticResult[] diagnostics;
            if (publish is true && includeContent is not true)
            {
                diagnostics = new DiagnosticResult[] {
                    // /0/Test0.cs(5,26): warning IL3000: 'System.Reflection.Assembly.Location' always returns an empty string for assemblies embedded in a single-file app. If the path to the app directory is needed, consider calling 'System.AppContext.BaseDirectory'.
                    VerifyCS.Diagnostic(AvoidAssemblyLocationInSingleFile.IL3000).WithSpan(5, 26, 5, 66).WithArguments("System.Reflection.Assembly.Location"),
                };
            }
            else
            {
                diagnostics = Array.Empty<DiagnosticResult>();
            }

            test.ExpectedDiagnostics.AddRange(diagnostics);
            return test.RunAsync();
        }

        [Fact]
        public Task AssemblyProperties()
        {
            var src = @"
using System.Reflection;
class C
{
    public void M()
    {
        var a = Assembly.GetExecutingAssembly();
        _ = a.Location;
        // below will be obsolete in 5.0
        _ = a.CodeBase;
        _ = a.EscapedCodeBase;
    }
}";
            return VerifyDiagnosticsAsync(src,
                // /0/Test0.cs(8,13): warning IL3000: 'System.Reflection.Assembly.Location' always returns an empty string for assemblies embedded in a single-file app. If the path to the app directory is needed, consider calling 'System.AppContext.BaseDirectory'.
                VerifyCS.Diagnostic(AvoidAssemblyLocationInSingleFile.IL3000).WithSpan(8, 13, 8, 23).WithArguments("System.Reflection.Assembly.Location")
            );
        }

        [Fact]
        public Task AssemblyMethods()
        {
            var src = @"
using System.Reflection;
class C
{
    public void M()
    {
        var a = Assembly.GetExecutingAssembly();
        _ = a.GetFile(""/some/file/path"");
        _ = a.GetFiles();
    }
}";
            return VerifyDiagnosticsAsync(src,
                // /0/Test0.cs(8,13): warning IL3001: Assemblies embedded in a single-file app cannot have additional files in the manifest.
                VerifyCS.Diagnostic(AvoidAssemblyLocationInSingleFile.IL3001).WithSpan(8, 13, 8, 41).WithArguments("System.Reflection.Assembly.GetFile(string)"),
                // /0/Test0.cs(9,13): warning IL3001: Assemblies embedded in a single-file app cannot have additional files in the manifest.
                VerifyCS.Diagnostic(AvoidAssemblyLocationInSingleFile.IL3001).WithSpan(9, 13, 9, 25).WithArguments("System.Reflection.Assembly.GetFiles()")
                );
        }

        [Fact]
        public Task AssemblyNameAttributes()
        {
            var src = @"
using System.Reflection;
class C
{
    public void M()
    {
        var a = Assembly.GetExecutingAssembly().GetName();
        _ = a.CodeBase;
        _ = a.EscapedCodeBase;
    }
}";
            return VerifyDiagnosticsAsync(src,
                // /0/Test0.cs(8,13): warning IL3000: 'System.Reflection.AssemblyName.CodeBase' always returns an empty string for assemblies embedded in a single-file app. If the path to the app directory is needed, consider calling 'System.AppContext.BaseDirectory'.
                VerifyCS.Diagnostic(AvoidAssemblyLocationInSingleFile.IL3000).WithSpan(8, 13, 8, 23).WithArguments("System.Reflection.AssemblyName.CodeBase"),
                // /0/Test0.cs(9,13): warning IL3000: 'System.Reflection.AssemblyName.EscapedCodeBase' always returns an empty string for assemblies embedded in a single-file app. If the path to the app directory is needed, consider calling 'System.AppContext.BaseDirectory'.
                VerifyCS.Diagnostic(AvoidAssemblyLocationInSingleFile.IL3000).WithSpan(9, 13, 9, 30).WithArguments("System.Reflection.AssemblyName.EscapedCodeBase")
                );
        }

        [Fact]
        public Task FalsePositive()
        {
            // This is an OK use of Location and GetFile since these assemblies were loaded from
            // a file, but the analyzer is conservative
            var src = @"
using System.Reflection;
class C
{
    public void M()
    {
        var a = Assembly.LoadFrom(""/some/path/not/in/bundle"");
        _ = a.Location;
        _ = a.GetFiles();
    }
}";
            return VerifyDiagnosticsAsync(src,
                // /0/Test0.cs(8,13): warning IL3000: 'System.Reflection.Assembly.Location' always returns an empty string for assemblies embedded in a single-file app. If the path to the app directory is needed, consider calling 'System.AppContext.BaseDirectory'.
                VerifyCS.Diagnostic(AvoidAssemblyLocationInSingleFile.IL3000).WithSpan(8, 13, 8, 23).WithArguments("System.Reflection.Assembly.Location"),
                // /0/Test0.cs(9,13): warning IL3001: Assemblies embedded in a single-file app cannot have additional files in the manifest.
                VerifyCS.Diagnostic(AvoidAssemblyLocationInSingleFile.IL3001).WithSpan(9, 13, 9, 25).WithArguments("System.Reflection.Assembly.GetFiles()")
                );
        }

        private Task VerifyDiagnosticsAsync(string source, params DiagnosticResult[] expected)
        {
            const string singleFilePublishConfig = @"
build_property." + PublishSingleFile + " = true";

            var test = new VerifyCS.Test
            {
                TestCode = source,
                AnalyzerConfigDocument = singleFilePublishConfig
            };

            test.ExpectedDiagnostics.AddRange(expected);
            return test.RunAsync();
        }
    }
}