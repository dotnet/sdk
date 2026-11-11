// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias NetAnalyzers;

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
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
using OperationExtensions = NetAnalyzers::Analyzer.Utilities.Extensions.IOperationExtensions;
using WellKnownTypeProvider = NetAnalyzers::Analyzer.Utilities.WellKnownTypeProvider;
using ValueContentAnalysisResult = Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.DataFlowAnalysisResult<
    Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis.ValueContentBlockAnalysisResult,
    Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis.ValueContentAbstractValue>;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests
{
    [TestClass]
    public class ValueContentAnalysisCacheTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void CustomMappingPoliciesRemainDistinctCSharp()
            => VerifyMappingPolicies(CreateCSharpInput());

        private AnalysisInput CreateCSharpInput()
        {
            var tree = CSharpSyntaxTree.ParseText("""
                struct Token { }
                class C
                {
                    static Token Make() => default;
                    static void M() { var token = Make(); }
                }
                """, cancellationToken: TestContext.CancellationToken);
            var compilation = CSharpCompilation.Create("MappingCacheTest", new[] { tree }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var syntax = tree.GetRoot(TestContext.CancellationToken).DescendantNodes()
                .OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == "M");
            return CreateInput(compilation, syntax);
        }

        [TestMethod]
        public void CustomMappingPoliciesRemainDistinctVisualBasic()
        {
            var tree = VisualBasicSyntaxTree.ParseText("""
                Structure Token
                End Structure
                Class C
                    Shared Function Make() As Token
                        Return Nothing
                    End Function
                    Shared Sub M()
                        Dim token = Make()
                    End Sub
                End Class
                """, cancellationToken: TestContext.CancellationToken);
            var compilation = VisualBasicCompilation.Create("MappingCacheTest", new[] { tree }, References,
                new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var syntax = tree.GetRoot(TestContext.CancellationToken).DescendantNodes()
                .OfType<MethodBlockSyntax>().Single(method => method.SubOrFunctionStatement.Identifier.ValueText == "M");
            VerifyMappingPolicies(CreateInput(compilation, syntax));
        }

        private AnalysisInput CreateInput(Compilation compilation, SyntaxNode syntax)
        {
            Assert.IsEmpty(compilation.GetDiagnostics(TestContext.CancellationToken)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray());
            var model = compilation.GetSemanticModel(syntax.SyntaxTree);
            var operation = model.GetOperation(syntax, TestContext.CancellationToken);
            Assert.IsNotNull(operation);
            Assert.IsTrue(OperationExtensions.TryGetEnclosingControlFlowGraph(operation, out var graph));
            var invocation = graph.Blocks.SelectMany(block => block.Operations)
                .SelectMany(root => root.DescendantsAndSelf()).OfType<IInvocationOperation>().Single();
            var owner = model.GetEnclosingSymbol(invocation.Syntax.SpanStart, TestContext.CancellationToken);
            Assert.IsNotNull(owner);
            return new AnalysisInput(compilation, graph, invocation, owner, WellKnownTypeProvider.GetOrCreate(compilation));
        }

        private static void VerifyMappingPolicies(AnalysisInput input)
        {
            var type = input.Compilation.GetSpecialType(SpecialType.System_String);
            var first = new MappingPolicy(ValueContentAbstractValue.Create("first-policy", type));
            var second = new MappingPolicy(ValueContentAbstractValue.Create("second-policy", type));

            Assert.IsEmpty(input.Analyze()[input.Invocation].LiteralValues);
            var firstResult = input.Analyze(first.Map);
            Assert.IsTrue(firstResult[input.Invocation].LiteralValues.SetEquals(new object[] { "first-policy" }));
            var secondResult = input.Analyze(second.Map);
            Assert.IsTrue(secondResult[input.Invocation].LiteralValues.SetEquals(new object[] { "second-policy" }));
            Assert.IsEmpty(input.Analyze()[input.Invocation].LiteralValues);
            Assert.AreNotSame(firstResult, secondResult);
            var calls = first.Calls;
            Assert.IsGreaterThan(0, calls);
            Assert.AreSame(firstResult, input.Analyze(new Func<IOperation, ValueContentAbstractValue>(first.Map)));
            Assert.AreEqual(calls, first.Calls);
        }

        [TestMethod]
        public void UnrelatedCompilationsDoNotEvictResults()
        {
            var input = CreateCSharpInput();
            var expected = input.Analyze();
            var others = Enumerable.Range(0, 12).Select(_ => CreateCSharpInput()).ToArray();
            foreach (var other in others)
            {
                Assert.AreNotSame(expected, other.Analyze());
            }

            Assert.AreSame(expected, input.Analyze());
            GC.KeepAlive(others);
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        public void SupportedTypeHashCollisionsDoNotShareResults(int commonPrefixLength)
        {
            var input = CreateCSharpInput();
            var token = input.Compilation.GetTypeByMetadataName("Token");
            var other = input.Compilation.GetTypeByMetadataName("C");
            var firstType = HashCollisionProxy.Create(token);
            var secondType = HashCollisionProxy.Create(other);
            Assert.AreEqual(firstType.GetHashCode(), secondType.GetHashCode());
            Assert.IsFalse(SymbolEqualityComparer.Default.Equals(firstType, secondType));
            var prefix = Enumerable.Repeat(token, commonPrefixLength).ToImmutableArray();
            var policy = new MappingPolicy(ValueContentAbstractValue.MayBeContainsNonLiteralState);
            var first = input.Analyze(policy.Map, prefix.Add(firstType));
            var second = input.Analyze(policy.Map, prefix.Add(secondType));
            Assert.AreNotSame(first, second);
            Assert.AreSame(second, input.Analyze(policy.Map, prefix.Add(secondType)));
            Assert.AreSame(first, input.Analyze(policy.Map, prefix.Add(HashCollisionProxy.Create(token))));
        }

        [TestMethod]
        public void SupportedTypeEqualityUsesDefaultSymbolComparer()
        {
            var input = CreateCSharpInput();
            var token = input.Compilation.GetTypeByMetadataName("C");
            var policy = new MappingPolicy(ValueContentAbstractValue.MayBeContainsNonLiteralState);
            var first = input.Analyze(policy.Map, ImmutableArray.Create((INamedTypeSymbol)token.WithNullableAnnotation(NullableAnnotation.Annotated)));
            var second = input.Analyze(policy.Map, ImmutableArray.Create((INamedTypeSymbol)token.WithNullableAnnotation(NullableAnnotation.NotAnnotated)));
            Assert.AreSame(first, second);
        }

        [TestMethod]
        [DoNotParallelize]
        public void LiveCompilationResultsSurviveCollection()
        {
            var input = CreateCSharpInput();
            var expected = input.Analyze();
            Collect();
            Assert.AreSame(expected, input.Analyze());
        }

        [TestMethod]
        [DoNotParallelize]
        public void ResultCacheDoesNotKeepCompilationsAlive()
        {
            var (compilation, graph, result) = CreateUnretainedAnalysis();
            Collect();
            Assert.IsFalse(compilation.TryGetTarget(out _));
            Assert.IsFalse(graph.TryGetTarget(out _));
            Assert.IsFalse(result.TryGetTarget(out _));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private (WeakReference<Compilation>, WeakReference<ControlFlowGraph>, WeakReference<ValueContentAnalysisResult>) CreateUnretainedAnalysis()
        {
            var input = CreateCSharpInput();
            var result = input.Analyze();
            return (new(input.Compilation), new(input.Graph), new(result));
        }

        [TestMethod]
        [DoNotParallelize]
        public void ResultMapRetentionIsBoundedWithinCompilation()
        {
            var methods = string.Join("\n", Enumerable.Range(0, 12).Select(index =>
                $"static void M{index}() {{ var token = Make(); }}"));
            var tree = CSharpSyntaxTree.ParseText($$"""
                struct Token { }
                class C
                {
                    static Token Make() => default;
                    {{methods}}
                }
                """, cancellationToken: TestContext.CancellationToken);
            var compilation = CSharpCompilation.Create("MappingCacheBoundTest", new[] { tree }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var inputs = tree.GetRoot(TestContext.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>()
                .Where(method => method.Identifier.ValueText != "Make").Select(method => CreateInput(compilation, method)).ToArray();
            var results = CreateWeakResults(inputs);
            Collect();
            Assert.AreEqual(5, results.Count(result => result.TryGetTarget(out _)));
            GC.KeepAlive(inputs);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static WeakReference<ValueContentAnalysisResult>[] CreateWeakResults(AnalysisInput[] inputs)
            => inputs.Select(input => new WeakReference<ValueContentAnalysisResult>(input.Analyze())).ToArray();

        private static void Collect()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
        }

        private sealed class AnalysisInput(
            Compilation compilation, ControlFlowGraph graph, IInvocationOperation invocation, ISymbol owner, WellKnownTypeProvider provider)
        {
            private readonly AnalyzerOptions _options = new(ImmutableArray<AdditionalText>.Empty);
            private readonly ImmutableArray<INamedTypeSymbol> _supportedTypes = ImmutableArray.Create(compilation.GetTypeByMetadataName("Token"));
            public Compilation Compilation { get; } = compilation;
            public ControlFlowGraph Graph { get; } = graph;
            public IInvocationOperation Invocation { get; } = invocation;

            public ValueContentAnalysisResult Analyze(
                Func<IOperation, ValueContentAbstractValue> mapper = null, ImmutableArray<INamedTypeSymbol> supportedTypes = default)
            {
                var rule = global::Microsoft.NetCore.Analyzers.Security.DoNotHardCodeEncryptionKey.Rule;
                var result = ValueContentAnalysis.TryGetOrComputeResult(Graph, owner, provider, _options, rule,
                    PointsToAnalysisKind.None, out _, out _, additionalSupportedValueTypes: supportedTypes.IsDefault ? _supportedTypes : supportedTypes,
                    getValueContentValueForAdditionalSupportedValueTypeOperation: mapper);
                Assert.IsNotNull(result);
                return result;
            }
        }

        private sealed class MappingPolicy(ValueContentAbstractValue value)
        {
            public int Calls { get; private set; }

            public ValueContentAbstractValue Map(IOperation operation)
            {
                Calls++;
                return value;
            }
        }

        private static MetadataReference[] References => new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
    }
}
