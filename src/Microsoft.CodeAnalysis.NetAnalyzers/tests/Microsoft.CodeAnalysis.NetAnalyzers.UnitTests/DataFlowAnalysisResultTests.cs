// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests
{
    [TestClass]
    public class DataFlowAnalysisResultTests
    {
        public TestContext TestContext { get; set; }

        private const int Unknown = -1;
        private const string Source = """
            class C
            {
                void M(int value)
                {
                    object result = (System.IComparable)value;
                }
            }
            """;

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void SyntaxLookupPrefersExplicitOperation(bool indexed)
        {
            var (cfg, operations) = CreateOperations(CSharpSyntaxTree.ParseText(Source, cancellationToken: TestContext.CancellationToken));
            var conversions = operations.OfType<IConversionOperation>().ToArray();
            var explicitOperation = conversions.Single(operation => !operation.IsImplicit);
            var implicitOperation = conversions.Single(operation => operation.IsImplicit);
            Assert.AreSame(explicitOperation.Syntax, implicitOperation.Syntax);
            var values = ImmutableDictionary<IOperation, int>.Empty
                .Add(explicitOperation, 10)
                .Add(implicitOperation, 20);
            var result = CreateResult(cfg, values);
            if (indexed)
            {
                Assert.AreEqual(Unknown, result[OperationKind.Invalid, explicitOperation.Syntax]);
            }

            Assert.AreEqual(10, result[OperationKind.Conversion, explicitOperation.Syntax]);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void SyntaxLookupPreservesFirstExplicitAndLastImplicit(bool implicitOperations)
        {
            var tree = CSharpSyntaxTree.ParseText(Source, cancellationToken: TestContext.CancellationToken);
            var (cfg, firstOperations) = CreateOperations(tree);
            var (_, secondOperations) = CreateOperations(tree);
            var first = firstOperations.OfType<IConversionOperation>().Single(operation => operation.IsImplicit == implicitOperations);
            var second = secondOperations.OfType<IConversionOperation>().Single(operation => operation.IsImplicit == implicitOperations);
            Assert.AreSame(first.Syntax, second.Syntax);
            var values = ImmutableDictionary<IOperation, int>.Empty.Add(first, 10).Add(second, 20);
            var expected = implicitOperations ? values.Last().Value : values.First().Value;
            var result = CreateResult(cfg, values);
            Assert.AreEqual(expected, result[first.Kind, first.Syntax]);
            Assert.AreEqual(Unknown, result[OperationKind.Invalid, first.Syntax]);
            Assert.AreEqual(expected, result[first.Kind, first.Syntax]);
        }

        [TestMethod]
        public void SyntaxLookupMatchesScanForAllOperations()
        {
            var (cfg, operations) = CreateOperations(CSharpSyntaxTree.ParseText(Source, cancellationToken: TestContext.CancellationToken));
            var values = operations.Select((operation, index) => (operation, index))
                .ToImmutableDictionary(pair => pair.operation, pair => pair.index);
            var result = CreateResult(cfg, values);
            for (var iteration = 0; iteration < 3; iteration++)
            {
                foreach (var operation in operations)
                {
                    var expected = Unknown;
                    foreach (var pair in values)
                    {
                        if (pair.Key.Kind == operation.Kind && pair.Key.Syntax == operation.Syntax)
                        {
                            expected = pair.Value;
                            if (!pair.Key.IsImplicit)
                            {
                                break;
                            }
                        }
                    }

                    Assert.AreEqual(expected, result[operation.Kind, operation.Syntax]);
                }
            }
        }

        [TestMethod]
        public void SyntaxLookupDoesNotMatchAnotherSyntaxTree()
        {
            var (cfg, operations) = CreateOperations(CSharpSyntaxTree.ParseText(Source, cancellationToken: TestContext.CancellationToken));
            var values = operations.ToImmutableDictionary(operation => operation, _ => 1);
            var result = CreateResult(cfg, values);
            var (_, otherOperations) = CreateOperations(CSharpSyntaxTree.ParseText(Source, cancellationToken: TestContext.CancellationToken));
            foreach (var operation in otherOperations)
            {
                Assert.AreEqual(Unknown, result[operation.Kind, operation.Syntax]);
            }
        }

        [TestMethod]
        public void ReplacingOperationValuesDoesNotReuseOldValues()
        {
            var (cfg, operations) = CreateOperations(CSharpSyntaxTree.ParseText(Source, cancellationToken: TestContext.CancellationToken));
            var operation = operations.OfType<IParameterReferenceOperation>().Single();
            var result = CreateResult(cfg, ImmutableDictionary<IOperation, int>.Empty.Add(operation, 10));
            Assert.AreEqual(Unknown, result[OperationKind.Invalid, operation.Syntax]);
            Assert.AreEqual(10, result[operation.Kind, operation.Syntax]);
            var updated = result.With(ImmutableDictionary<IOperation, int>.Empty.Add(operation, 20));
            Assert.AreEqual(Unknown, updated[OperationKind.Invalid, operation.Syntax]);
            Assert.AreEqual(20, updated[operation.Kind, operation.Syntax]);
            Assert.AreEqual(10, result[operation.Kind, operation.Syntax]);
        }

        [TestMethod]
        public void EmptyResultReturnsUnknown()
        {
            var (cfg, operations) = CreateOperations(CSharpSyntaxTree.ParseText(Source, cancellationToken: TestContext.CancellationToken));
            var result = CreateResult(cfg, ImmutableDictionary<IOperation, int>.Empty);
            foreach (var operation in operations)
            {
                Assert.AreEqual(Unknown, result[operation.Kind, operation.Syntax]);
            }
        }

        [TestMethod]
        public Task ConcurrentSyntaxLookupsPreserveValues()
        {
            var (cfg, operations) = CreateOperations(CSharpSyntaxTree.ParseText(Source, cancellationToken: TestContext.CancellationToken));
            var operation = operations.OfType<IParameterReferenceOperation>().Single();
            var result = CreateResult(cfg, ImmutableDictionary<IOperation, int>.Empty.Add(operation, 10));
            return Task.WhenAll(Enumerable.Range(0, 16).Select(_ => Task.Run(() =>
            {
                for (var iteration = 0; iteration < 100; iteration++)
                {
                    Assert.AreEqual(Unknown, result[OperationKind.Invalid, operation.Syntax]);
                    Assert.AreEqual(10, result[operation.Kind, operation.Syntax]);
                }
            }, TestContext.CancellationToken)));
        }

        private (ControlFlowGraph Cfg, ImmutableArray<IOperation> Operations) CreateOperations(SyntaxTree tree)
        {
            var compilation = CSharpCompilation.Create("DataFlowResultTest", new[] { tree },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var method = tree.GetRoot(TestContext.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
            var operation = (IMethodBodyOperation)compilation.GetSemanticModel(tree).GetOperation(method, TestContext.CancellationToken);
            return (ControlFlowGraph.Create(operation, TestContext.CancellationToken), operation.DescendantsAndSelf().ToImmutableArray());
        }

        private static DataFlowAnalysisResult<TestBlockResult, int> CreateResult(
            ControlFlowGraph cfg, ImmutableDictionary<IOperation, int> values)
        {
            var lambdaInfo = new LambdaAndLocalFunctionAnalysisInfo(
                ImmutableHashSet.CreateBuilder<IMethodSymbol>(), ImmutableHashSet.CreateBuilder<IMethodSymbol>(),
                ImmutableHashSet.CreateBuilder<IFlowAnonymousFunctionOperation>(), ImmutableHashSet.CreateBuilder<IFlowAnonymousFunctionOperation>());
            return new DataFlowAnalysisResult<TestBlockResult, int>(
                ImmutableDictionary<BasicBlock, TestBlockResult>.Empty, values,
                ImmutableDictionary<IOperation, PredicateValueKind>.Empty, null,
                ImmutableDictionary<IOperation, IDataFlowAnalysisResult<int>>.Empty,
                ImmutableDictionary<IMethodSymbol, IDataFlowAnalysisResult<int>>.Empty,
                lambdaInfo, new TestBlockResult(cfg.Blocks[0]), new TestBlockResult(cfg.Blocks[cfg.Blocks.Length - 1]),
                null, null, null, null, cfg, Unknown);
        }

        private sealed class TestBlockResult : AbstractBlockAnalysisResult
        {
            public TestBlockResult(BasicBlock basicBlock) : base(basicBlock)
            {
            }
        }
    }
}
