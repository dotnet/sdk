// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias NetAnalyzers;

using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
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
using IndexBuilder = NetAnalyzers::Analyzer.Utilities.PooledObjects.ArrayBuilder<Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.AbstractIndex>;
using WellKnownTypeProvider = NetAnalyzers::Analyzer.Utilities.WellKnownTypeProvider;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests
{
    [TestClass]
    public class AnalysisEntityFactoryTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DataRow("matrix[left: 0, right: 1]", 17)]
        [DataRow("matrix[right: 1, left: 0]", 17)]
        [DataRow("matrix[0, right: 1]", 17)]
        [DataRow("matrix[right: 1]", 17)]
        [DataRow("matrix[left: 1, right: 0]", 23)]
        public void IndexerKeysUseParameterOrderCSharp(string read, int expected)
        {
            var tree = CSharpSyntaxTree.ParseText($$"""
                class Matrix
                {
                    private readonly int[,] values = new int[2, 2];
                    public int this[int left = 0, int right = 0]
                    {
                        get => values[left, right];
                        set => values[left, right] = value;
                    }
                }
                class C
                {
                    static void M(Matrix matrix)
                    {
                        matrix[left: 0, right: 1] = 17;
                        matrix[left: 1, right: 0] = 23;
                        var observed = {{read}};
                    }
                }
                """, cancellationToken: TestContext.CancellationToken);
            var compilation = CSharpCompilation.Create("IndexerKeyTest", new[] { tree }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var method = tree.GetRoot(TestContext.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
            var model = compilation.GetSemanticModel(tree);
            var cfg = ControlFlowGraph.Create((IMethodBodyOperation)model.GetOperation(method, TestContext.CancellationToken),
                TestContext.CancellationToken);
            VerifyIndexerRead(compilation, cfg, model.GetDeclaredSymbol(method, TestContext.CancellationToken), expected);
        }

        [TestMethod]
        [DataRow("matrix(left:=0, right:=1)", 17)]
        [DataRow("matrix(right:=1, left:=0)", 17)]
        [DataRow("matrix(1, left:=0)", 17)]
        [DataRow("matrix(right:=1)", 17)]
        [DataRow("matrix(left:=1, right:=0)", 23)]
        public void IndexerKeysUseParameterOrderVisualBasic(string read, int expected)
        {
            var tree = VisualBasicSyntaxTree.ParseText($$"""
                Class Matrix
                    Private ReadOnly values(1, 1) As Integer
                    Default Public Property Item(right As Integer, Optional left As Integer = 0) As Integer
                        Get
                            Return values(left, right)
                        End Get
                        Set(value As Integer)
                            values(left, right) = value
                        End Set
                    End Property
                End Class
                Class C
                    Shared Sub M(matrix As Matrix)
                        matrix(left:=0, right:=1) = 17
                        matrix(left:=1, right:=0) = 23
                        Dim observed = {{read}}
                    End Sub
                End Class
                """, cancellationToken: TestContext.CancellationToken);
            var compilation = VisualBasicCompilation.Create("IndexerKeyTest", new[] { tree }, References,
                new VisualBasicCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var method = tree.GetRoot(TestContext.CancellationToken).DescendantNodes().OfType<MethodBlockSyntax>()
                .Single(method => method.SubOrFunctionStatement.Identifier.ValueText == "M");
            var model = compilation.GetSemanticModel(tree);
            var cfg = ControlFlowGraph.Create((IBlockOperation)model.GetOperation(method, TestContext.CancellationToken),
                TestContext.CancellationToken);
            VerifyIndexerRead(compilation, cfg, compilation.GetTypeByMetadataName("C").GetMembers("M").Single(), expected);
        }

        private void VerifyIndexerRead(Compilation compilation, ControlFlowGraph cfg, ISymbol owner, int expected)
        {
            Assert.IsEmpty(compilation.GetDiagnostics(TestContext.CancellationToken)
                .Where(diagnostic => diagnostic.Severity == DiagnosticSeverity.Error).ToArray());
            var result = ValueContentAnalysis.TryGetOrComputeResult(cfg, owner, WellKnownTypeProvider.GetOrCreate(compilation),
                new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty), global::Microsoft.NetCore.Analyzers.Security.DoNotHardCodeEncryptionKey.Rule,
                PointsToAnalysisKind.Complete, pessimisticAnalysis: false);
            Assert.IsNotNull(result);
            var read = cfg.Blocks.SelectMany(block => block.Operations)
                .SelectMany(operation => operation.DescendantsAndSelf()).OfType<IPropertyReferenceOperation>().Last();
            Assert.IsTrue(result[read].TryGetSingleNonNullLiteral<int>(out var literal));
            Assert.AreEqual(expected, literal);
        }

        [TestMethod]
        public void NamedIndexerKeysDoNotReorderArgumentEvaluation()
        {
            var tree = CSharpSyntaxTree.ParseText("""
                class Matrix { public int this[int left, int right] { get => 0; set {} } }
                class C
                {
                    static void M(Matrix matrix)
                    {
                        int index = 0;
                        matrix[right: index = 1, left: index = 2] = 17;
                    }
                }
                """, cancellationToken: TestContext.CancellationToken);
            var compilation = CSharpCompilation.Create("IndexerEvaluation", new[] { tree }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var model = compilation.GetSemanticModel(tree);
            var method = tree.GetRoot(TestContext.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
            var cfg = ControlFlowGraph.Create((IMethodBodyOperation)model.GetOperation(method, TestContext.CancellationToken),
                TestContext.CancellationToken);
            var result = ValueContentAnalysis.TryGetOrComputeResult(cfg, model.GetDeclaredSymbol(method, TestContext.CancellationToken),
                WellKnownTypeProvider.GetOrCreate(compilation), new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
                global::Microsoft.NetCore.Analyzers.Security.DoNotHardCodeEncryptionKey.Rule,
                PointsToAnalysisKind.Complete, pessimisticAnalysis: false);
            Assert.IsNotNull(result);
            var property = cfg.Blocks.SelectMany(block => block.Operations)
                .SelectMany(operation => operation.DescendantsAndSelf()).OfType<IPropertyReferenceOperation>().Single();
            Assert.AreEqual(1, property.Arguments[0].Parameter.Ordinal);
            Assert.AreEqual(0, property.Arguments[1].Parameter.Ordinal);
            Assert.IsTrue(result[property.Arguments[0]].TryGetSingleNonNullLiteral<int>(out var first));
            Assert.IsTrue(result[property.Arguments[1]].TryGetSingleNonNullLiteral<int>(out var second));
            Assert.AreEqual(1, first);
            Assert.AreEqual(2, second);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        [DoNotParallelize]
        public void IndexerProjectionReturnsBuilderWhenArgumentAccessFails(bool constantValueFails)
        {
            var property = CreateIndexerReference();
            var original = property.Arguments[0];
            var argument = HashCollisionProxy.Create(original, (method, args) =>
            {
                if (method.Name == "get_Value")
                {
                    if (!constantValueFails)
                    {
                        throw new OperationCanceledException();
                    }

                    return HashCollisionProxy.Create(original.Value, static (_, _) => throw new OperationCanceledException());
                }

                return method.Invoke(original, args);
            });
            var reference = HashCollisionProxy.Create(property, (method, args) =>
                method.Name == "get_Arguments" ? ImmutableArray.Create(argument) : method.Invoke(property, args));
            var seed = IndexBuilder.GetInstance();
            seed.Dispose();
            var exception = Assert.ThrowsExactly<TargetInvocationException>(() => Project(reference, out _));
            Assert.IsInstanceOfType<OperationCanceledException>(exception.InnerException);
            using var reused = IndexBuilder.GetInstance();
            try
            {
                Assert.AreSame(seed, reused);
            }
            finally
            {
                if (!ReferenceEquals(seed, reused))
                {
                    seed.Dispose();
                }
            }
        }

        [TestMethod]
        [DoNotParallelize]
        public void ArrayIndexProjectionReturnsBuilderWhenIndexAccessFails()
        {
            var original = CreateIndexerReference().Arguments[0].Value;
            var operation = HashCollisionProxy.Create(original, static (_, _) => throw new OperationCanceledException());
            var method = typeof(AnalysisEntityFactory).GetMethods(BindingFlags.NonPublic | BindingFlags.Static)
                .Single(candidate => candidate.Name == "CreateAbstractIndices" && candidate.IsGenericMethodDefinition)
                .MakeGenericMethod(typeof(IOperation));
            var seed = IndexBuilder.GetInstance();
            seed.Dispose();
            var exception = Assert.ThrowsExactly<TargetInvocationException>(() =>
                method.Invoke(null, new object[] { ImmutableArray.Create(operation) }));
            Assert.IsInstanceOfType<OperationCanceledException>(exception.InnerException);
            using var reused = IndexBuilder.GetInstance();
            try
            {
                Assert.AreSame(seed, reused);
            }
            finally
            {
                if (!ReferenceEquals(seed, reused))
                {
                    seed.Dispose();
                }
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void UnboundOrDuplicateIndexerParametersAreNotTracked(bool duplicate)
        {
            var property = CreateIndexerReference();
            var original = property.Arguments[0];
            var argument = HashCollisionProxy.Create(original, (method, args) =>
                method.Name == "get_Parameter" && !duplicate ? null : method.Invoke(original, args));
            var arguments = duplicate ? ImmutableArray.Create(argument, argument) : ImmutableArray.Create(argument);
            var reference = HashCollisionProxy.Create(property, (method, args) =>
                method.Name == "get_Arguments" ? arguments : method.Invoke(property, args));
            var indices = Project(reference, out var symbol);
            Assert.IsNull(symbol);
            Assert.IsTrue(indices.IsEmpty);
        }

        private IPropertyReferenceOperation CreateIndexerReference()
        {
            var tree = CSharpSyntaxTree.ParseText("""
                class Matrix { public int this[int index] => index; }
                class C { static int M(Matrix matrix) => matrix[0]; }
                """, cancellationToken: TestContext.CancellationToken);
            var compilation = CSharpCompilation.Create("IndexerProjection", new[] { tree }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var method = tree.GetRoot(TestContext.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
            return compilation.GetSemanticModel(tree).GetOperation(method, TestContext.CancellationToken)
                .DescendantsAndSelf().OfType<IPropertyReferenceOperation>().Single();
        }

        private static ImmutableArray<AbstractIndex> Project(IPropertyReferenceOperation property, out ISymbol symbol)
        {
            var method = typeof(AnalysisEntityFactory).GetMethod("GetSymbolAndIndicesForMemberReference",
                BindingFlags.NonPublic | BindingFlags.Static);
            object[] arguments = { property, null, ImmutableArray<AbstractIndex>.Empty };
            method.Invoke(null, arguments);
            symbol = (ISymbol)arguments[1];
            return (ImmutableArray<AbstractIndex>)arguments[2];
        }

        private static MetadataReference[] References => new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
    }
}
