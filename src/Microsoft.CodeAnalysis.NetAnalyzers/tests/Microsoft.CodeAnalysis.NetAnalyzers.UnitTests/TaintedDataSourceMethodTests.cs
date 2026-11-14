// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias NetAnalyzers;

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities.FlowAnalysis.Analysis.TaintedDataAnalysis;
using Analyzer.Utilities.PooledObjects;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.FlowAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.Operations;
using WellKnownTypeProvider = NetAnalyzers::Analyzer.Utilities.WellKnownTypeProvider;
using ValueContentAnalysisResult = Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.DataFlowAnalysisResult<Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis.ValueContentBlockAnalysisResult, Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis.ValueContentAbstractValue>;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests
{
    [TestClass]
    public class TaintedDataSourceMethodTests
    {
        private static readonly DiagnosticDescriptor s_rule = global::Microsoft.NetCore.Analyzers.Security.DoNotHardCodeEncryptionKey.Rule;

        public TestContext TestContext { get; set; }

        [TestMethod]
        public void CfgChecksEvaluateOnceAndShareArgumentValues()
        {
            var (compilation, cfg, original, lowered, owner) = CreateOperations();
            var provider = WellKnownTypeProvider.GetOrCreate(compilation);
            var values = ValueContentAnalysis.TryGetOrComputeResult(
                cfg, owner, provider, new AnalyzerOptions(ImmutableArray<AdditionalText>.Empty),
                s_rule, PointsToAnalysisKind.Complete, out _, out var pointsTo);
            Assert.IsNotNull(values);
            Assert.IsNotNull(pointsTo);
            var pointArrays = new List<ImmutableArray<PointsToAbstractValue>>();
            var valueArrays = new List<ImmutableArray<ValueContentAbstractValue>>();
            ValueContentCheck check = (points, contents) =>
            {
                pointArrays.Add(points);
                valueArrays.Add(contents);
                Assert.HasCount(2, points);
                Assert.HasCount(2, contents);
                Assert.IsTrue(contents.All(value => value.IsLiteralState));
                return true;
            };
            using var sources = PooledHashSet<SourceInfo>.GetInstance();
            sources.AddSourceInfoSpecifyingTaintedTargets(
                "Source", isInterface: false, taintedProperties: null,
                taintedMethodsNeedsPointsToAnalysis: null,
                taintedMethodsNeedsValueContentAnalysis: new[]
                {
                    ((MethodMatcher)((name, _) => name == "Read"),
                        new[] { (check, "first"), (check, TaintedTargetValue.Return) })
                },
                transferMethods: null);
            var map = new TaintedDataSymbolMap<SourceInfo>(provider, sources);
            Assert.IsTrue(map.IsSourceMethod(lowered.TargetMethod, lowered.Arguments, pointsTo, values, out var targets));
            using (targets)
            {
                Assert.HasCount(2, targets);
                Assert.Contains("first", targets);
                Assert.Contains(TaintedTargetValue.Return, targets);
            }

            Assert.HasCount(2, pointArrays);
            Assert.AreEqual(pointArrays[0], pointArrays[1]);
            Assert.AreEqual(valueArrays[0], valueArrays[1]);

            pointArrays.Clear();
            valueArrays.Clear();
            var pointsFactory = new Lazy<PointsToAnalysisResult>(() => pointsTo);
            var valueFactory = new Lazy<(PointsToAnalysisResult, ValueContentAnalysisResult)>(
                () => (pointsTo, values));
            Assert.IsTrue(map.IsSourceMethod(original.TargetMethod, original.Arguments, pointsFactory, valueFactory));
            Assert.HasCount(1, pointArrays);
            Assert.IsFalse(pointsFactory.IsValueCreated);
            Assert.IsTrue(valueFactory.IsValueCreated);
        }

        [TestMethod]
        public void OrdinarySourcePrepassDoesNotRequestFlowResults()
        {
            var (compilation, _, original, _, _) = CreateOperations();
            using var sources = PooledHashSet<SourceInfo>.GetInstance();
            sources.AddSourceInfo("Source", isInterface: false, taintedProperties: null, taintedMethods: new[] { "Read" });
            var map = new TaintedDataSymbolMap<SourceInfo>(WellKnownTypeProvider.GetOrCreate(compilation), sources);
            var pointsFactory = new Lazy<PointsToAnalysisResult>(() => throw new InvalidOperationException("Unexpected points-to analysis"));
            var valueFactory = new Lazy<(PointsToAnalysisResult, ValueContentAnalysisResult)>(
                () => throw new InvalidOperationException("Unexpected value-content analysis"));
            Assert.IsTrue(map.IsSourceMethod(original.TargetMethod, original.Arguments, pointsFactory, valueFactory));
            Assert.IsFalse(pointsFactory.IsValueCreated);
            Assert.IsFalse(valueFactory.IsValueCreated);
        }

        [TestMethod]
        public void MissingAnalysisDoesNotInvokeSourceChecks()
        {
            var (compilation, _, _, lowered, _) = CreateOperations();
            using var sources = PooledHashSet<SourceInfo>.GetInstance();
            sources.AddSourceInfoSpecifyingTaintedTargets(
                "Source", isInterface: false, taintedProperties: null,
                taintedMethodsNeedsPointsToAnalysis: null,
                taintedMethodsNeedsValueContentAnalysis: new[]
                {
                    ((MethodMatcher)((name, _) => name == "Read"),
                        new[] { ((ValueContentCheck)((_, _) => throw new InvalidOperationException("Unexpected source check")), TaintedTargetValue.Return) })
                },
                transferMethods: null);
            var map = new TaintedDataSymbolMap<SourceInfo>(WellKnownTypeProvider.GetOrCreate(compilation), sources);
            Assert.IsFalse(map.IsSourceMethod(lowered.TargetMethod, lowered.Arguments, pointsToAnalysisResult: null, valueContentAnalysisResult: null, out var targets));
            Assert.IsNull(targets);
        }

        private (Compilation Compilation, ControlFlowGraph Cfg, IInvocationOperation Original, IInvocationOperation Lowered, ISymbol Owner) CreateOperations()
        {
            var tree = CSharpSyntaxTree.ParseText("""
                class Source
                {
                    public static string Read(string first, string second) => first;
                }
                class C
                {
                    void M() { Source.Read("one", "two"); }
                }
                """, cancellationToken: TestContext.CancellationToken);
            var compilation = CSharpCompilation.Create("SourceMethodTest", new[] { tree },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var model = compilation.GetSemanticModel(tree);
            var method = tree.GetRoot(TestContext.CancellationToken).DescendantNodes()
                .OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == "M");
            var operation = (IMethodBodyOperation)model.GetOperation(method, TestContext.CancellationToken);
            var cfg = ControlFlowGraph.Create(operation, TestContext.CancellationToken);
            var original = operation.DescendantsAndSelf().OfType<IInvocationOperation>().Single();
            var lowered = cfg.Blocks.SelectMany(block => block.Operations)
                .SelectMany(operation => operation.DescendantsAndSelf()).OfType<IInvocationOperation>().Single();
            return (compilation, cfg, original, lowered, model.GetDeclaredSymbol(method, TestContext.CancellationToken));
        }
    }
}
