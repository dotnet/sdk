// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Analyzer.Utilities.FlowAnalysis.Analysis.TaintedDataAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.CopyAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests
{
    [TestClass]
    public class TrackedEntityChildrenTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void ChildLookupExcludesRootsAndUnrelatedInstances()
        {
            var (roots, children) = CreateEntities();
            using var builder = new TrackedEntitiesBuilder(PointsToAnalysisKind.Complete);
            foreach (var entity in roots.Concat(children))
            {
                builder.AddEntityAndPointsToValue(entity, PointsToAbstractValue.Unknown);
            }

            Assert.AreSame(children[0], builder.EnumerateChildEntities(roots[0].InstanceLocation).Single());
            Assert.AreSame(children[1], builder.EnumerateChildEntities(roots[1].InstanceLocation).Single());
            Assert.IsEmpty(builder.EnumerateChildEntities(PointsToAbstractValue.NoLocation));
        }

        [TestMethod]
        public void ChildLookupTracksNewEntitiesWithoutDuplicatingExistingOnes()
        {
            var (roots, children) = CreateEntities();
            using var builder = new TrackedEntitiesBuilder(PointsToAnalysisKind.Complete);
            builder.AddEntityAndPointsToValue(roots[0], PointsToAbstractValue.Unknown);
            Assert.IsEmpty(builder.EnumerateChildEntities(roots[0].InstanceLocation));
            builder.AddEntityAndPointsToValue(children[0], PointsToAbstractValue.Unknown);
            builder.AddEntityAndPointsToValue(children[0], PointsToAbstractValue.Unknown);
            builder.AddEntityAndPointsToValue(children[1], PointsToAbstractValue.Unknown);
            Assert.AreSame(children[0], builder.EnumerateChildEntities(roots[0].InstanceLocation).Single());
            Assert.AreSame(children[1], builder.EnumerateChildEntities(roots[1].InstanceLocation).Single());
            Assert.HasCount(3, builder.EnumerateEntities());
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        public void MutableChildLookupTracksEveryInsertionPath(int insertion)
        {
            var (roots, children) = CreateEntities();
            using var data = new TaintedDataAnalysisData();
            Assert.IsEmpty(data.GetChildAnalysisEntityCandidates(roots[0].InstanceLocation));
            IDictionary<AnalysisEntity, TaintedDataAbstractValue> map = data.CoreAnalysisData;
            var value = TaintedDataAbstractValue.NotTainted;
            switch (insertion)
            {
                case 0:
                    map[children[0]] = value;
                    break;
                case 1:
                    map.Add(children[0], value);
                    break;
                case 2:
                    map.Add(new KeyValuePair<AnalysisEntity, TaintedDataAbstractValue>(children[0], value));
                    break;
                default:
                    data.SetAbstractValue(children[0], value);
                    break;
            }

            map[children[0]] = value;
            Assert.AreSame(children[0], data.GetChildAnalysisEntityCandidates(roots[0].InstanceLocation).Single());
            Assert.IsEmpty(data.GetChildAnalysisEntityCandidates(roots[1].InstanceLocation));
            Assert.IsEmpty(data.GetChildAnalysisEntityCandidates(PointsToAbstractValue.NoLocation));
        }

        [TestMethod]
        [DataRow(0)]
        [DataRow(1)]
        [DataRow(2)]
        [DataRow(3)]
        public void MutableChildLookupTracksRemovalAndClear(int removal)
        {
            var (roots, children) = CreateEntities();
            using var data = new TaintedDataAnalysisData();
            var value = TaintedDataAbstractValue.NotTainted;
            data.CoreAnalysisData[children[0]] = value;
            data.CoreAnalysisData[children[1]] = value;
            Assert.AreSame(children[0], data.GetChildAnalysisEntityCandidates(roots[0].InstanceLocation).Single());
            IDictionary<AnalysisEntity, TaintedDataAbstractValue> map = data.CoreAnalysisData;
            switch (removal)
            {
                case 0:
                    Assert.IsTrue(map.Remove(children[0]));
                    break;
                case 1:
                    Assert.IsTrue(map.Remove(new KeyValuePair<AnalysisEntity, TaintedDataAbstractValue>(children[0], value)));
                    break;
                case 2:
                    data.RemoveEntries(children[0]);
                    break;
                default:
                    map.Clear();
                    break;
            }

            Assert.IsEmpty(data.GetChildAnalysisEntityCandidates(roots[0].InstanceLocation));
            if (removal == 3)
            {
                Assert.IsEmpty(data.GetChildAnalysisEntityCandidates(roots[1].InstanceLocation));
            }
            else
            {
                Assert.AreSame(children[1], data.GetChildAnalysisEntityCandidates(roots[1].InstanceLocation).Single());
            }

            map[children[0]] = value;
            Assert.AreSame(children[0], data.GetChildAnalysisEntityCandidates(roots[0].InstanceLocation).Single());
        }

        [TestMethod]
        public void MutableChildLookupHandlesSameCountKeyReplacement()
        {
            var (roots, children) = CreateEntities();
            using var data = new TaintedDataAnalysisData();
            data.CoreAnalysisData[children[0]] = TaintedDataAbstractValue.NotTainted;
            Assert.AreSame(children[0], data.GetChildAnalysisEntityCandidates(roots[0].InstanceLocation).Single());
            data.CoreAnalysisData.Remove(children[0]);
            data.CoreAnalysisData[children[1]] = TaintedDataAbstractValue.NotTainted;
            Assert.HasCount(1, data.CoreAnalysisData);
            Assert.IsEmpty(data.GetChildAnalysisEntityCandidates(roots[0].InstanceLocation));
            Assert.AreSame(children[1], data.GetChildAnalysisEntityCandidates(roots[1].InstanceLocation).Single());
        }

        [TestMethod]
        public void MutableChildLookupIsIndependentAcrossCloneAndMerge()
        {
            var (roots, children) = CreateEntities();
            using var first = new TaintedDataAnalysisData();
            first.CoreAnalysisData[children[0]] = TaintedDataAbstractValue.NotTainted;
            Assert.AreSame(children[0], first.GetChildAnalysisEntityCandidates(roots[0].InstanceLocation).Single());
            using var clone = first.Clone();
            clone.CoreAnalysisData[children[1]] = TaintedDataAbstractValue.NotTainted;
            Assert.AreSame(children[1], clone.GetChildAnalysisEntityCandidates(roots[1].InstanceLocation).Single());
            Assert.IsEmpty(first.GetChildAnalysisEntityCandidates(roots[1].InstanceLocation));

            using var second = new TaintedDataAnalysisData();
            second.CoreAnalysisData[children[1]] = TaintedDataAbstractValue.NotTainted;
            using var merged = first.WithMergedData(second,
                new MapAbstractDomain<AnalysisEntity, TaintedDataAbstractValue>(TaintedDataAnalysis.ValueDomainInstance));
            Assert.AreSame(children[0], merged.GetChildAnalysisEntityCandidates(roots[0].InstanceLocation).Single());
            Assert.AreSame(children[1], merged.GetChildAnalysisEntityCandidates(roots[1].InstanceLocation).Single());
            first.CoreAnalysisData.Clear();
            Assert.AreSame(children[0], merged.GetChildAnalysisEntityCandidates(roots[0].InstanceLocation).Single());
            Assert.AreSame(children[0], clone.GetChildAnalysisEntityCandidates(roots[0].InstanceLocation).Single());
        }

        [TestMethod]
        public void ValueContentChildIndexTracksStateChanges()
        {
            var (roots, children) = CreateEntities();
            using var data = new ValueContentAnalysisData();
            Assert.IsEmpty(data.GetChildAnalysisEntityCandidates(roots[0].InstanceLocation));
            data.SetAbstractValue(children[0], ValueContentAbstractValue.MayBeContainsNonLiteralState);
            Assert.AreSame(children[0], data.GetChildAnalysisEntityCandidates(roots[0].InstanceLocation).Single());
            using var clone = data.Clone();
            clone.CoreAnalysisData[children[1]] = ValueContentAbstractValue.ContainsNullLiteralState;
            data.RemoveEntries(children[0]);
            Assert.IsEmpty(data.GetChildAnalysisEntityCandidates(roots[0].InstanceLocation));
            Assert.AreSame(children[0], clone.GetChildAnalysisEntityCandidates(roots[0].InstanceLocation).Single());
            Assert.AreSame(children[1], clone.GetChildAnalysisEntityCandidates(roots[1].InstanceLocation).Single());
        }

        [TestMethod]
        public void CopyChildIndexObservesDirectCoreUpdates()
        {
            var (roots, children) = CreateEntities();
            using var data = new CopyAnalysisData();
            Assert.IsEmpty(data.GetChildAnalysisEntityCandidates(roots[0].InstanceLocation));
            data.SetAbstactValueForEntities(new CopyAbstractValue(children[0]), entityBeingAssigned: null);
            Assert.AreSame(children[0], data.GetChildAnalysisEntityCandidates(roots[0].InstanceLocation).Single());
            data.CoreAnalysisData[children[1]] = new CopyAbstractValue(children[1]);
            data.CoreAnalysisData.Remove(children[0]);
            using var clone = data.Clone();
            Assert.IsEmpty(clone.GetChildAnalysisEntityCandidates(roots[0].InstanceLocation));
            Assert.AreSame(children[1], clone.GetChildAnalysisEntityCandidates(roots[1].InstanceLocation).Single());
        }

        private (AnalysisEntity[] Roots, AnalysisEntity[] Children) CreateEntities()
        {
            var tree = CSharpSyntaxTree.ParseText("""
                struct S { public object Field; }
                class C { void M(S first, S second) {} }
                """, cancellationToken: TestContext.CancellationToken);
            var compilation = CSharpCompilation.Create("TrackedChildrenTest", new[] { tree },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var method = tree.GetRoot(TestContext.CancellationToken).DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
            var symbol = (IMethodSymbol)compilation.GetSemanticModel(tree).GetDeclaredSymbol(method, TestContext.CancellationToken);
            var roots = symbol.Parameters.Select(parameter =>
                AnalysisEntity.Create(parameter, ImmutableArray<AbstractIndex>.Empty, parameter.Type,
                    PointsToAbstractValue.Create(AbstractLocation.CreateSymbolLocation(parameter, null), mayBeNull: false),
                    parent: null, entityForInstanceLocation: null)).ToArray();
            var field = (IFieldSymbol)compilation.GetTypeByMetadataName("S").GetMembers("Field").Single();
            var children = roots.Select(root =>
                AnalysisEntity.Create(field, ImmutableArray<AbstractIndex>.Empty, field.Type,
                    root.InstanceLocation, root, entityForInstanceLocation: null)).ToArray();
            return (roots, children);
        }
    }
}
