// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias NetAnalyzers;

using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeAnalysis.VisualBasic;
using Microsoft.CodeAnalysis.VisualBasic.Syntax;
using WellKnownTypeProvider = NetAnalyzers::Analyzer.Utilities.WellKnownTypeProvider;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests
{
    [TestClass]
    public class DataFlowArgumentTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DataRow("""Capture("first", Identity("second"), Identity(Identity("third")));""", "first", "second", "third")]
        [DataRow("""string value = "first"; Capture(value, Mutate(ref value, "second"), value);""", "first", "second", "second")]
        [DataRow("""string value; Capture("first", Initialize(out value), value);""", "first", "second", "second")]
        [DataRow("""var outer = new Holder { Value = "first" }; { var inner = outer; inner.Value = "discard"; } Capture(outer.Value, "second", "third");""", "first", "second", "third")]
        [DataRow("""var outer = (Value: "first", Other: "value"); { var inner = outer; inner.Value = "discard"; } Capture(outer.Value, "second", "third");""", "first", "second", "third")]
        [DataRow("""var outer = new Holder { Value = "first" }; if (flag) { var inner = outer; outer = inner; } else { outer = new Holder { Value = "first" }; } Capture(outer.Value, "second", "third");""", "first", "second", "third")]
        [DataRow("""var outer = new Holder { Value = "first" }; Capture(ReadValue(outer), "second", "third");""", "first", "second", "third")]
        public void NestedAndEscapedArgumentsCSharp(string body, string first, string second, string third)
        {
            var tree = CSharpSyntaxTree.ParseText($$"""
                class C
                {
                    struct Holder { public string Value; }
                    static string ReadValue(Holder value) => value.Value;
                    static string Identity(string value) => value;
                    static string Mutate(ref string value, string replacement) { value = replacement; return replacement; }
                    static string Initialize(out string value) { value = "second"; return value; }
                    static void Capture(string first, string second, string third) {}
                    static void M(bool flag) { {{body}} }
                }
                """, cancellationToken: TestContext.CancellationToken);
            var compilation = CSharpCompilation.Create("ArgumentsTest", new[] { tree }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var method = tree.GetRoot(TestContext.CancellationToken).DescendantNodes()
                .OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == "M");
            var model = compilation.GetSemanticModel(tree);
            var cfg = ControlFlowGraph.Create((IMethodBodyOperation)model.GetOperation(method, TestContext.CancellationToken), TestContext.CancellationToken);
            VerifyArguments(compilation, cfg, model.GetDeclaredSymbol(method, TestContext.CancellationToken), first, second, third);
        }

        [TestMethod]
        [DataRow("""Capture("first", Identity("second"), Identity(Identity("third")))""", "first", "second", "third")]
        [DataRow("""Dim value = "first" : Capture(value, Mutate(value, "second"), value)""", "first", "second", "second")]
        [DataRow("""
            Dim outer As New Holder With {.Value = "first"}
            If True Then
                Dim inner = outer
                inner.Value = "discard"
            End If
            Capture(outer.Value, "second", "third")
            """, "first", "second", "third")]
        [DataRow("""
            Dim outer As New Holder With {.Value = "first"}
            If flag Then
                Dim inner = outer
                outer = inner
            Else
                outer = New Holder With {.Value = "first"}
            End If
            Capture(outer.Value, "second", "third")
            """, "first", "second", "third")]
        [DataRow("""
            Dim outer As New Holder With {.Value = "first"}
            Capture(ReadValue(outer), "second", "third")
            """, "first", "second", "third")]
        public void NestedAndEscapedArgumentsVisualBasic(string body, string first, string second, string third)
        {
            var tree = VisualBasicSyntaxTree.ParseText($$"""
                Class C
                    Structure Holder
                        Public Value As String
                    End Structure
                    Shared Function ReadValue(value As Holder) As String
                        Return value.Value
                    End Function
                    Shared Function Identity(value As String) As String
                        Return value
                    End Function
                    Shared Function Mutate(ByRef value As String, replacement As String) As String
                        value = replacement
                        Return replacement
                    End Function
                    Shared Sub Capture(first As String, second As String, third As String)
                    End Sub
                    Shared Sub M(flag As Boolean)
                        {{body}}
                    End Sub
                End Class
                """, cancellationToken: TestContext.CancellationToken);
            var compilation = VisualBasicCompilation.Create("ArgumentsTest", new[] { tree }, References,
                new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var method = tree.GetRoot(TestContext.CancellationToken).DescendantNodes()
                .OfType<MethodBlockSyntax>().Single(method => method.SubOrFunctionStatement.Identifier.ValueText == "M");
            var model = compilation.GetSemanticModel(tree);
            var cfg = ControlFlowGraph.Create((IBlockOperation)model.GetOperation(method, TestContext.CancellationToken), TestContext.CancellationToken);
            VerifyArguments(compilation, cfg, compilation.GetTypeByMetadataName("C").GetMembers("M").Single(), first, second, third);
        }

        private void VerifyArguments(Compilation compilation, ControlFlowGraph cfg, ISymbol owner, params string[] expected)
        {
            var result = ValueContentAnalysis.TryGetOrComputeResult(cfg, owner, WellKnownTypeProvider.GetOrCreate(compilation),
                new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty), global::Microsoft.NetCore.Analyzers.Security.DoNotHardCodeEncryptionKey.Rule,
                PointsToAnalysisKind.Complete, interproceduralAnalysisKind: InterproceduralAnalysisKind.ContextSensitive);
            Assert.IsNotNull(result);
            var invocation = cfg.Blocks.SelectMany(block => block.Operations)
                .SelectMany(operation => operation.DescendantsAndSelf()).OfType<IInvocationOperation>()
                .Single(operation => operation.TargetMethod.Name == "Capture");
            Assert.HasCount(expected.Length, invocation.Arguments);
            for (var index = 0; index < expected.Length; index++)
            {
                Assert.IsTrue(result[invocation.Arguments[index]].TryGetSingleNonNullLiteral<string>(out var value));
                Assert.AreEqual(expected[index], value);
            }
        }

        private static MetadataReference[] References => new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
    }
}
