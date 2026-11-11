// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

extern alias NetAnalyzers;

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.CodeQuality.Analyzers.QualityGuidelines.AvoidMultipleEnumerations.FlowAnalysis;
using RoslynHashCode = NetAnalyzers::Analyzer.Utilities.RoslynHashCode;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests
{
    [TestClass]
    public class MultipleEnumerationStateTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void AdditiveUpdatesShareReportableContributions()
        {
            var (entities, operations) = CreateInputs();
            var first = CreateState((entities[0], EnumerationCount.TwoOrMoreTime, new[] { operations[0] }));
            var second = GlobalFlowStateDictionaryAnalysisValue.Merge(first,
                CreateState((entities[1], EnumerationCount.One, new[] { operations[1] })), false);
            Assert.AreSame(first.ReportableEnumerationSets, second.ReportableEnumerationSets);
            var third = GlobalFlowStateDictionaryAnalysisValue.Merge(second,
                CreateState((entities[0], EnumerationCount.One, new[] { operations[2] })), false);
            Assert.AreSame(second.ReportableEnumerationSets, third.ReportableEnumerationSets.Pop());
            VerifyProjection(first);
            VerifyProjection(second);
            VerifyProjection(third);
            Assert.IsTrue(GetReportableOperations(third).SetEquals(new[] { operations[0], operations[2] }));
        }

        [TestMethod]
        public void SaturatedUpdatesReuseReportableContributions()
        {
            var (entities, operations) = CreateInputs();
            var value = CreateState((entities[0], EnumerationCount.TwoOrMoreTime, new[] { operations[0] }));
            var merged = GlobalFlowStateDictionaryAnalysisValue.Merge(value, value, false);
            Assert.AreSame(value.ReportableEnumerationSets, merged.ReportableEnumerationSets);
            VerifyProjection(merged);
        }

        [TestMethod]
        public void LostReportabilityRebuildsContributions()
        {
            var (entities, operations) = CreateInputs();
            var first = CreateState(
                (entities[0], EnumerationCount.TwoOrMoreTime, new[] { operations[0] }),
                (entities[1], EnumerationCount.TwoOrMoreTime, new[] { operations[1] }));
            var second = CreateState((entities[0], EnumerationCount.One, new[] { operations[0] }));
            var merged = GlobalFlowStateDictionaryAnalysisValue.Merge(first, second, true);
            Assert.IsTrue(GetReportableOperations(merged).SetEquals(new[] { operations[1] }));
            VerifyProjection(first);
            VerifyProjection(merged);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void BranchIntersectionsPreserveDistinctSites(bool sameSite)
        {
            var (entities, operations) = CreateInputs();
            var first = CreateState((entities[0], EnumerationCount.TwoOrMoreTime, new[] { operations[0] }));
            var second = CreateState((entities[0], EnumerationCount.One, new[] { operations[sameSite ? 0 : 1] }));
            var intersected = GlobalFlowStateDictionaryAnalysisValue.Intersect(first, second);
            Assert.AreEqual(EnumerationCount.One, intersected.TrackedEntities[entities[0]].EnumerationCount);
            Assert.AreEqual(!sameSite, intersected.TrackedEntities[entities[0]].IsMultipleEnumeration);
            VerifyProjection(intersected);
        }

        [TestMethod]
        public void RemovingEntitiesPreservesOnlyRemainingContributions()
        {
            var (entities, operations) = CreateInputs();
            var value = CreateState(
                (entities[0], EnumerationCount.TwoOrMoreTime, new[] { operations[0] }),
                (entities[1], EnumerationCount.One, new[] { operations[1] }),
                (entities[2], EnumerationCount.TwoOrMoreTime, new[] { operations[2] }));
            var withoutSingle = value.RemoveTrackedDeferredTypeEntity(entities[1]);
            Assert.AreSame(value.ReportableEnumerationSets, withoutSingle.ReportableEnumerationSets);
            var withoutMultiple = withoutSingle.RemoveTrackedDeferredTypeEntity(entities[0]);
            Assert.IsTrue(GetReportableOperations(withoutMultiple).SetEquals(new[] { operations[2] }));
            Assert.AreSame(GlobalFlowStateDictionaryAnalysisValue.Empty,
                withoutMultiple.RemoveTrackedDeferredTypeEntity(entities[2]));
            VerifyProjection(value);
            VerifyProjection(withoutSingle);
            VerifyProjection(withoutMultiple);
        }

        [TestMethod]
        public void RemovingOneEntityRetainsSharedOperationSites()
        {
            var (entities, operations) = CreateInputs();
            var value = CreateState(
                (entities[0], EnumerationCount.TwoOrMoreTime, new[] { operations[0] }),
                (entities[1], EnumerationCount.TwoOrMoreTime, new[] { operations[0] }));
            var removed = value.RemoveTrackedDeferredTypeEntity(entities[0]);
            Assert.IsTrue(GetReportableOperations(removed).SetEquals(new[] { operations[0] }));
            VerifyProjection(removed);
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void AllCountTransitionsPreserveReportProjection(bool intersect)
        {
            var (entities, operations) = CreateInputs();
            var counts = new[] { EnumerationCount.None, EnumerationCount.Zero, EnumerationCount.One, EnumerationCount.TwoOrMoreTime };
            foreach (var firstCount in counts)
            {
                foreach (var secondCount in counts)
                {
                    foreach (var sameSite in new[] { false, true })
                    {
                        var first = CreateState(
                            (entities[0], firstCount, new[] { operations[0] }),
                            (entities[1], EnumerationCount.TwoOrMoreTime, new[] { operations[2] }));
                        var second = CreateState((entities[0], secondCount, new[] { operations[sameSite ? 0 : 1] }));
                        VerifyProjection(GlobalFlowStateDictionaryAnalysisValue.Merge(first, second, intersect));
                        VerifyProjection(GlobalFlowStateDictionaryAnalysisValue.Merge(second, first, intersect));
                        VerifyProjection(GlobalFlowStateDictionaryAnalysisValue.Intersect(first, second));
                        VerifyProjection(GlobalFlowStateDictionaryAnalysisValue.Intersect(second, first));
                    }
                }
            }
        }

        [TestMethod]
        public void ChangedSiteCardinalityDoesNotHashHistoricalSites()
        {
            var (entities, operations) = CreateInputs();
            var first = new ThrowingHashEnumerationSet(ImmutableHashSet.Create(operations[0]), EnumerationCount.TwoOrMoreTime);
            var second = new ThrowingHashEnumerationSet(ImmutableHashSet.Create(operations[0], operations[1]), EnumerationCount.TwoOrMoreTime);
            var value = new GlobalFlowStateDictionaryAnalysisValue(
                ImmutableDictionary<IDeferredTypeEntity, TrackingEnumerationSet>.Empty.Add(entities[0], first),
                GlobalFlowStateDictionaryAnalysisValueKind.Known);
            var updated = value.TrackedEntities.SetItem(entities[0], second);
            Assert.AreSame(second, updated[entities[0]]);
            Assert.AreSame(TrackingEnumerationSet.ValueComparer, updated.ValueComparer);
        }

        [TestMethod]
        public void ChangedCountDoesNotHashHistoricalSites()
        {
            var (_, operations) = CreateInputs();
            var first = new ThrowingHashEnumerationSet(ImmutableHashSet.Create(operations[0]), EnumerationCount.One);
            var second = new ThrowingHashEnumerationSet(ImmutableHashSet.Create(operations[0]), EnumerationCount.TwoOrMoreTime);
            Assert.IsFalse(TrackingEnumerationSet.ValueComparer.Equals(first, second));
            Assert.IsTrue(TrackingEnumerationSet.ValueComparer.Equals(first, first));
        }

        private static GlobalFlowStateDictionaryAnalysisValue CreateState(
            params (IDeferredTypeEntity Entity, EnumerationCount Count, IOperation[] Operations)[] entries)
            => new(entries.ToImmutableDictionary(entry => entry.Entity,
                entry => new TrackingEnumerationSet(entry.Operations.ToImmutableHashSet(), entry.Count)),
                GlobalFlowStateDictionaryAnalysisValueKind.Known);

        private static HashSet<IOperation> GetReportableOperations(GlobalFlowStateDictionaryAnalysisValue value)
            => value.ReportableEnumerationSets.SelectMany(set => set.Operations).ToHashSet();

        private static void VerifyProjection(GlobalFlowStateDictionaryAnalysisValue value)
            => Assert.IsTrue(GetReportableOperations(value).SetEquals(
                value.TrackedEntities.Values.Where(set => set.IsMultipleEnumeration).SelectMany(set => set.Operations)));

        private (IDeferredTypeEntity[] Entities, IOperation[] Operations) CreateInputs()
        {
            var tree = CSharpSyntaxTree.ParseText("""
                class C
                {
                    void M(int first, int second, int third)
                    {
                        var a = first;
                        var b = second;
                        var c = third;
                    }
                }
                """, cancellationToken: TestContext.CancellationToken);
            var compilation = CSharpCompilation.Create("EnumerationStateTest", new[] { tree },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var root = tree.GetRoot(TestContext.CancellationToken);
            var model = compilation.GetSemanticModel(tree);
            var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
            var symbol = (IMethodSymbol)model.GetDeclaredSymbol(method, TestContext.CancellationToken);
            return (symbol.Parameters.Select(parameter => (IDeferredTypeEntity)new DeferredTypeSymbolEntity(parameter)).ToArray(),
                root.DescendantNodes().OfType<IdentifierNameSyntax>()
                    .Select(syntax => model.GetOperation(syntax, TestContext.CancellationToken))
                    .OfType<IParameterReferenceOperation>().Cast<IOperation>().ToArray());
        }

        private sealed class ThrowingHashEnumerationSet(ImmutableHashSet<IOperation> operations, EnumerationCount count)
            : TrackingEnumerationSet(operations, count)
        {
            protected override void ComputeHashCodeParts(ref RoslynHashCode hashCode)
                => throw new System.InvalidOperationException("Historical site hashing was not needed for this comparison.");
        }
    }
}
