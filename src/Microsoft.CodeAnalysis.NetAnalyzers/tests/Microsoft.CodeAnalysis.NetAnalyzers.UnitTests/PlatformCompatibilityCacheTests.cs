// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.NetCore.Analyzers.InteropServices;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests
{
    [TestClass]
    public class PlatformCompatibilityCacheTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DoNotParallelize]
        public Task CachedMapperDoesNotCaptureBlockStateCSharp()
        {
            var tree = CSharpSyntaxTree.ParseText("""
                using System.Runtime.InteropServices;
                using System.Runtime.Versioning;
                class C
                {
                    [SupportedOSPlatform("windows")] static void WindowsOnly() { }
                    static void M()
                    {
                        var platform = OSPlatform.Create("windows");
                        if (RuntimeInformation.IsOSPlatform(platform)) WindowsOnly();
                        WindowsOnly();
                    }
                }
                """, cancellationToken: TestContext.CancellationToken);
            return VerifyCachedMapperAsync(CSharpCompilation.Create("PlatformMapperCapture", new[] { tree }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)), tree);
        }

        [TestMethod]
        [DoNotParallelize]
        public Task CachedMapperDoesNotCaptureBlockStateVisualBasic()
        {
            var tree = VisualBasicSyntaxTree.ParseText("""
                Imports System.Runtime.InteropServices
                Imports System.Runtime.Versioning
                Class C
                    <SupportedOSPlatform("windows")>
                    Shared Sub WindowsOnly()
                    End Sub
                    Shared Sub M()
                        Dim platform = OSPlatform.Create("windows")
                        If RuntimeInformation.IsOSPlatform(platform) Then WindowsOnly()
                        WindowsOnly()
                    End Sub
                End Class
                """, cancellationToken: TestContext.CancellationToken);
            return VerifyCachedMapperAsync(VisualBasicCompilation.Create("PlatformMapperCapture", new[] { tree }, References,
                new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary)), tree);
        }

        private async Task VerifyCachedMapperAsync(Compilation compilation, SyntaxTree tree)
        {
            Assert.IsEmpty(compilation.GetDiagnostics(TestContext.CancellationToken)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray());
            compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
                ImmutableDictionary<string, ReportDiagnostic>.Empty.Add("CA1416", ReportDiagnostic.Warn)));
            var options = new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty, new PlatformOptionsProvider());
            var driver = compilation.WithAnalyzers(ImmutableArray.Create<DiagnosticAnalyzer>(new PlatformCompatibilityAnalyzer()),
                new CompilationWithAnalyzersOptions(options, null, concurrentAnalysis: false,
                    logAnalyzerExecutionTime: false, reportSuppressedDiagnostics: false));
            var diagnostics = await driver.GetAnalyzerDiagnosticsAsync(TestContext.CancellationToken);
            Assert.HasCount(1, diagnostics, string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.ToString())));
            Assert.AreEqual("CA1416", diagnostics[0].Id);

            var cacheField = typeof(ValueContentAnalysis).BaseType.BaseType.GetField(
                "s_resultCachesByCompilation", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(cacheField);
            var mappers = ((IEnumerable)cacheField.GetValue(null)).Cast<object>()
                .SelectMany(association =>
                {
                    var cache = association.GetType().GetProperty("Value").GetValue(association);
                    var entriesField = cache.GetType().BaseType.GetField("_weakReferencedEntries",
                        BindingFlags.Instance | BindingFlags.NonPublic);
                    return ((IEnumerable)entriesField.GetValue(cache)).Cast<object>();
                })
                .Select(weak =>
                {
                    object[] arguments = { null };
                    return (bool)weak.GetType().GetMethod("TryGetTarget").Invoke(weak, arguments) ? arguments[0] : null;
                })
                .Where(entry => entry != null && ((IOperation)entry.GetType().GetProperty("Key").GetValue(entry)).Syntax.SyntaxTree == tree)
                .Select(entry => entry.GetType().GetProperty("Value").GetValue(entry))
                .SelectMany(map =>
                {
                    var dictionary = map.GetType().GetField("BackingDictionary", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(map);
                    return (System.Collections.Generic.IEnumerable<ValueContentAnalysisContext>)dictionary.GetType().GetProperty("Keys").GetValue(dictionary);
                })
                .Select(context => context.GetValueForAdditionalSupportedValueTypeOperation)
                .Where(mapper => mapper != null)
                .ToArray();
            Assert.HasCount(1, mappers);
            Assert.IsNotNull(mappers[0].Target);
            var captures = mappers[0].Target.GetType()
                .GetFields(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.IsNotEmpty(captures);
            foreach (var capture in captures)
            {
                var value = capture.GetValue(mappers[0].Target);
                Assert.IsTrue(value == null || value is ISymbol, $"Cached mapper retained {capture.FieldType}.");
            }
        }

        private static MetadataReference[] References => new[]
        {
            typeof(object).Assembly.Location,
            typeof(OSPlatform).Assembly.Location,
            typeof(SupportedOSPlatformAttribute).Assembly.Location,
            Assembly.Load("System.Runtime").Location
        }.Distinct().Select(path => MetadataReference.CreateFromFile(path)).ToArray();

        private sealed class PlatformOptionsProvider : AnalyzerConfigOptionsProvider
        {
            private readonly AnalyzerConfigOptions _options = new PlatformOptions();
            public override AnalyzerConfigOptions GlobalOptions => _options;
            public override AnalyzerConfigOptions GetOptions(SyntaxTree tree) => _options;
            public override AnalyzerConfigOptions GetOptions(AdditionalText textFile) => _options;
        }

        private sealed class PlatformOptions : AnalyzerConfigOptions
        {
            public override bool TryGetValue(string key, out string value)
            {
                value = key switch
                {
                    "build_property.TargetFrameworkIdentifier" => ".NETCoreApp",
                    "build_property.TargetFrameworkVersion" => "v5.0",
                    _ => null
                };
                return value != null;
            }
        }
    }
}
