// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias NetAnalyzers;

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.VisualBasic;
using MethodBlockSyntax = Microsoft.CodeAnalysis.VisualBasic.Syntax.MethodBlockSyntax;
using OperationBlocksExtensions = NetAnalyzers::Analyzer.Utilities.Extensions.OperationBlocksExtensions;
using OperationExtensions = NetAnalyzers::Analyzer.Utilities.Extensions.IOperationExtensions;
using WellKnownTypeProvider = NetAnalyzers::Analyzer.Utilities.WellKnownTypeProvider;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests
{
    [TestClass]
    public class ControlFlowGraphCacheTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public Task ContextAndHelperShareCanonicalGraphCSharp(bool helperFirst)
        {
            var tree = CSharpSyntaxTree.ParseText("""
                class C
                {
                    static int M(int value)
                    {
                        if (value > 0) return value;
                        return -value;
                    }
                }
                """, cancellationToken: TestContext.CancellationToken);
            return VerifyAsync(CSharpCompilation.Create("CfgCacheTest", new[] { tree }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)), helperFirst);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public Task ContextAndHelperShareCanonicalGraphVisualBasic(bool helperFirst)
        {
            var tree = VisualBasicSyntaxTree.ParseText("""
                Class C
                    Shared Function M(value As Integer) As Integer
                        If value > 0 Then Return value
                        Return -value
                    End Function
                End Class
                """, cancellationToken: TestContext.CancellationToken);
            return VerifyAsync(VisualBasicCompilation.Create("CfgCacheTest", new[] { tree }, References,
                new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary)), helperFirst);
        }

        [TestMethod]
        public async Task CancellationDoesNotPoisonGraphCache()
        {
            var tree = CSharpSyntaxTree.ParseText("class C { static int M(int value) => value; }",
                cancellationToken: TestContext.CancellationToken);
            var compilation = CSharpCompilation.Create("CfgCancellationTest", new[] { tree }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var calls = 0;
            var analyzer = new CallbackAnalyzer(context =>
            {
                var root = context.OperationBlocks.Single();
                Assert.ThrowsExactly<OperationCanceledException>(() =>
                {
                    _ = OperationExtensions.TryGetEnclosingControlFlowGraph(root, out _, () => throw new OperationCanceledException());
                });
                Assert.ThrowsExactly<OperationCanceledException>(() =>
                {
                    _ = OperationExtensions.TryGetEnclosingControlFlowGraph(root, out _, 0,
                        static _ => throw new OperationCanceledException());
                });
                Assert.IsTrue(OperationExtensions.TryGetEnclosingControlFlowGraph(root, out var graph,
                    () => context.GetControlFlowGraph(root)));
                Assert.AreSame(context.GetControlFlowGraph(root), graph);
                calls++;
            });
            await RunAsync(compilation, analyzer);
            Assert.AreEqual(1, calls);
        }

        [TestMethod]
        public void AttributeRootDoesNotInvokeHostFactory()
        {
            var tree = CSharpSyntaxTree.ParseText("""[System.Obsolete("value")] class C {}""",
                cancellationToken: TestContext.CancellationToken);
            var compilation = CSharpCompilation.Create("CfgAttributeTest", new[] { tree }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var syntax = tree.GetRoot(TestContext.CancellationToken).DescendantNodes()
                .OfType<AttributeSyntax>().Single();
            var operation = compilation.GetSemanticModel(tree).GetOperation(syntax, TestContext.CancellationToken);
            Assert.IsNotNull(operation);
            Assert.IsFalse(OperationExtensions.TryGetEnclosingControlFlowGraph(operation, out var graph,
                () => throw new InvalidOperationException("Unsupported root requested a host CFG.")));
            Assert.IsNull(graph);
            Assert.IsFalse(OperationExtensions.TryGetEnclosingControlFlowGraph(operation, out graph, 0,
                static _ => throw new InvalidOperationException("Unsupported root requested a stateful host CFG.")));
            Assert.IsNull(graph);
        }

        [TestMethod]
        public Task CachedContextLookupsDoNotAllocateCSharp()
        {
            var tree = CSharpSyntaxTree.ParseText("class C { static int M(int value) => value; }",
                cancellationToken: TestContext.CancellationToken);
            return VerifyCacheHitAllocationsAsync(CSharpCompilation.Create("CfgAllocationTest", new[] { tree }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));
        }

        [TestMethod]
        public Task CachedContextLookupsDoNotAllocateVisualBasic()
        {
            var tree = VisualBasicSyntaxTree.ParseText("""
                Class C
                    Shared Function M(value As Integer) As Integer
                        Return value
                    End Function
                End Class
                """, cancellationToken: TestContext.CancellationToken);
            return VerifyCacheHitAllocationsAsync(VisualBasicCompilation.Create("CfgAllocationTest", new[] { tree }, References,
                new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary)));
        }

        private async Task VerifyCacheHitAllocationsAsync(Compilation compilation)
        {
            var calls = 0;
            var analyzer = new CallbackAnalyzer(
                context =>
                {
                    Assert.AreEqual(0L, GetCacheHitAllocatedBytes(() => OperationBlocksExtensions.GetControlFlowGraph(context)));
                    var root = context.OperationBlocks.Single();
                    Assert.AreEqual(0L, GetCacheHitAllocatedBytes(() =>
                        OperationExtensions.TryGetEnclosingControlFlowGraph(root, out var graph,
                            (Context: context, Root: root), static state => state.Context.GetControlFlowGraph(state.Root))
                            ? graph : throw new InvalidOperationException("Operation root has no CFG.")));
                    calls++;
                },
                operationCallback: context =>
                {
                    Assert.AreEqual(0L, GetCacheHitAllocatedBytes(() =>
                        OperationExtensions.TryGetEnclosingControlFlowGraph(context, out var graph) ? graph :
                            throw new InvalidOperationException("Operation context has no CFG.")));
                    calls++;
                },
                startCallback: context =>
                {
                    Assert.AreEqual(0L, GetCacheHitAllocatedBytes(() => OperationBlocksExtensions.GetControlFlowGraph(context)));
                    calls++;
                });
            await RunAsync(compilation, analyzer);
            Assert.AreEqual(3, calls);
        }

        private static long GetCacheHitAllocatedBytes(Func<ControlFlowGraph> lookup)
        {
            var graph = lookup();
            Assert.IsNotNull(graph);
            for (var index = 0; index < 128; index++)
            {
                _ = lookup();
            }

            var before = GC.GetAllocatedBytesForCurrentThread();
            for (var index = 0; index < 1000; index++)
            {
                _ = lookup();
            }

            var allocatedBytes = GC.GetAllocatedBytesForCurrentThread() - before;
            GC.KeepAlive(graph);
            return allocatedBytes;
        }

        private async Task VerifyAsync(Compilation compilation, bool helperFirst)
        {
            var calls = 0;
            var analyzer = new CallbackAnalyzer(context =>
            {
                var root = context.OperationBlocks.Single();
                ControlFlowGraph expected;
                if (helperFirst)
                {
                    Assert.IsTrue(OperationExtensions.TryGetEnclosingControlFlowGraph(root, out expected));
                }
                else
                {
                    expected = context.GetControlFlowGraph(root);
                }

                var factoryCalls = 0;
                ControlFlowGraph GetHostGraph()
                {
                    factoryCalls++;
                    return context.GetControlFlowGraph(root);
                }

                Assert.IsTrue(OperationExtensions.TryGetEnclosingControlFlowGraph(root, out var graph, GetHostGraph));
                Assert.AreSame(expected, graph);
                Assert.IsTrue(OperationExtensions.TryGetEnclosingControlFlowGraph(root, out var repeated, GetHostGraph));
                Assert.AreSame(graph, repeated);
                Assert.AreSame(graph, OperationBlocksExtensions.GetControlFlowGraph(context));
                Assert.AreEqual(helperFirst ? 0 : 1, factoryCalls);

                var provider = WellKnownTypeProvider.GetOrCreate(context.Compilation);
                var first = ValueContentAnalysis.TryGetOrComputeResult(graph, context.OwningSymbol, provider,
                    context.Options, Rule, PointsToAnalysisKind.Complete);
                var second = ValueContentAnalysis.TryGetOrComputeResult(repeated, context.OwningSymbol, provider,
                    context.Options, Rule, PointsToAnalysisKind.Complete);
                Assert.IsNotNull(first);
                Assert.IsNotNull(second);
                // Cache capacity can evict results; live CFG identity remains canonical.
                Assert.AreSame(graph, first.ControlFlowGraph);
                Assert.AreSame(graph, second.ControlFlowGraph);
                calls++;
            });
            await RunAsync(compilation, analyzer);
            Assert.AreEqual(1, calls);
        }

        private async Task RunAsync(Compilation compilation, DiagnosticAnalyzer analyzer)
        {
            compilation = compilation.WithOptions(compilation.Options.WithSpecificDiagnosticOptions(
                ImmutableDictionary<string, ReportDiagnostic>.Empty.Add(Rule.Id, ReportDiagnostic.Warn)));
            var driver = compilation.WithAnalyzers(ImmutableArray.Create(analyzer),
                new CompilationWithAnalyzersOptions(new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty), null,
                    concurrentAnalysis: false, logAnalyzerExecutionTime: false, reportSuppressedDiagnostics: false));
            var diagnostics = await driver.GetAnalyzerDiagnosticsAsync(TestContext.CancellationToken);
            Assert.IsEmpty(diagnostics, string.Join(Environment.NewLine, diagnostics.Select(diagnostic => diagnostic.ToString())));
        }

        private static DiagnosticDescriptor Rule => global::Microsoft.NetCore.Analyzers.Security.DoNotHardCodeEncryptionKey.Rule;
        private static MetadataReference[] References => new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };

        [TestMethod]
        [DoNotParallelize]
        public async Task LiveCanonicalGraphSurvivesCollection()
        {
            var tree = CSharpSyntaxTree.ParseText("class C { static int M(int value) => value; }",
                cancellationToken: TestContext.CancellationToken);
            var compilation = CSharpCompilation.Create("CfgLiveGraphTest", new[] { tree }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var calls = 0;
            var analyzer = new CallbackAnalyzer(context =>
            {
                var root = context.OperationBlocks.Single();
                Assert.IsTrue(OperationExtensions.TryGetEnclosingControlFlowGraph(root, out var first));
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
                Assert.IsTrue(OperationExtensions.TryGetEnclosingControlFlowGraph(root, out var second,
                    () => throw new InvalidOperationException("A live canonical graph was lost.")));
                Assert.AreSame(first, second);
                calls++;
            });
            await RunAsync(compilation, analyzer);
            Assert.AreEqual(1, calls);
        }

        [TestMethod]
        [DoNotParallelize]
        public void CacheDoesNotKeepUnusedGraphsAlive()
        {
            var (root, graph) = CreateUnretainedGraph();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.IsFalse(graph.TryGetTarget(out _));
            GC.KeepAlive(root);
        }

        [TestMethod]
        public void SharedSyntaxCompilationsHaveDistinctGraphsCSharp()
        {
            var tree = CSharpSyntaxTree.ParseText("class C { static int M(int value) => value; }",
                cancellationToken: TestContext.CancellationToken);
            var compilation = CSharpCompilation.Create("CfgIsolationTest", new[] { tree }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var declaration = tree.GetRoot(TestContext.CancellationToken).DescendantNodes()
                .OfType<MethodDeclarationSyntax>().Single();
            VerifyCompilationIsolation(compilation, declaration);
        }

        [TestMethod]
        public void SharedSyntaxCompilationsHaveDistinctGraphsVisualBasic()
        {
            var tree = VisualBasicSyntaxTree.ParseText("""
                Class C
                    Shared Function M(value As Integer) As Integer
                        Return value
                    End Function
                End Class
                """, cancellationToken: TestContext.CancellationToken);
            var compilation = VisualBasicCompilation.Create("CfgIsolationTest", new[] { tree }, References,
                new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var declaration = tree.GetRoot(TestContext.CancellationToken).DescendantNodes()
                .OfType<MethodBlockSyntax>().Single();
            VerifyCompilationIsolation(compilation, declaration);
        }

        private void VerifyCompilationIsolation(Compilation compilation, SyntaxNode declaration)
        {
            var other = compilation.WithAssemblyName("OtherCfgIsolationTest");
            var firstRoot = compilation.GetSemanticModel(declaration.SyntaxTree).GetOperation(declaration, TestContext.CancellationToken);
            var secondRoot = other.GetSemanticModel(declaration.SyntaxTree).GetOperation(declaration, TestContext.CancellationToken);
            Assert.IsNotNull(firstRoot);
            Assert.IsNotNull(secondRoot);
            Assert.IsTrue(OperationExtensions.TryGetEnclosingControlFlowGraph(firstRoot, out var first));
            Assert.IsTrue(OperationExtensions.TryGetEnclosingControlFlowGraph(secondRoot, out var second));
            Assert.AreNotSame(first, second);
            Assert.AreSame(compilation, first.OriginalOperation.SemanticModel.Compilation);
            Assert.AreSame(other, second.OriginalOperation.SemanticModel.Compilation);
            Assert.IsTrue(OperationExtensions.TryGetEnclosingControlFlowGraph(firstRoot, out var repeated));
            Assert.AreSame(first, repeated);
        }

        [TestMethod]
        [DoNotParallelize]
        public void CacheDoesNotKeepCompilationsAlive()
        {
            var (compilation, root, graph) = CreateUnretainedCompilation();
            GC.Collect();
            GC.WaitForPendingFinalizers();
            GC.Collect();
            Assert.IsFalse(compilation.TryGetTarget(out _));
            Assert.IsFalse(root.TryGetTarget(out _));
            Assert.IsFalse(graph.TryGetTarget(out _));
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private (WeakReference<Compilation>, WeakReference<IOperation>, WeakReference<ControlFlowGraph>) CreateUnretainedCompilation()
        {
            var (root, graph) = CreateUnretainedGraph();
            return (new(root.SemanticModel.Compilation), new(root), graph);
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private (IOperation Root, WeakReference<ControlFlowGraph> Graph) CreateUnretainedGraph()
        {
            var tree = CSharpSyntaxTree.ParseText("class C { static int M(int value) => value; }",
                cancellationToken: TestContext.CancellationToken);
            var compilation = CSharpCompilation.Create("CfgUnusedGraphTest", new[] { tree }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var declaration = tree.GetRoot(TestContext.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
            var root = compilation.GetSemanticModel(tree).GetOperation(declaration, TestContext.CancellationToken);
            Assert.IsNotNull(root);
            Assert.IsTrue(OperationExtensions.TryGetEnclosingControlFlowGraph(root, out var graph));
            return (root, new WeakReference<ControlFlowGraph>(graph));
        }

#pragma warning disable RS1001 // Test-only analyzer instantiated directly, not loaded by the compiler.
        private sealed class CallbackAnalyzer(
            Action<OperationBlockAnalysisContext> callback,
            Action<OperationAnalysisContext> operationCallback = null,
            Action<OperationBlockStartAnalysisContext> startCallback = null) : DiagnosticAnalyzer
#pragma warning restore RS1001
        {
            public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

            public override void Initialize(AnalysisContext context)
            {
                context.EnableConcurrentExecution();
                context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
                context.RegisterOperationBlockAction(callback);
                if (operationCallback != null)
                {
                    context.RegisterOperationAction(operationCallback, OperationKind.ParameterReference);
                }

                if (startCallback != null)
                {
                    context.RegisterOperationBlockStartAction(startContext =>
                    {
                        startContext.RegisterOperationBlockEndAction(static _ => { });
                        startCallback(startContext);
                    });
                }
            }
        }
    }
}
