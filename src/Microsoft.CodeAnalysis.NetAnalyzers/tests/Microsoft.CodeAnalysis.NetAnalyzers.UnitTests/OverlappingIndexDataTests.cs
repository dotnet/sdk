// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.ValueContentAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests
{
    [TestClass]
    public class OverlappingIndexDataTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        [DataRow(false, false)]
        [DataRow(false, true)]
        [DataRow(true, false)]
        [DataRow(true, true)]
        public void IndexedCandidatesMatchRecursivePropagation(bool aliases, bool childIndexFirst)
        {
            var (entities, type) = CreateEntities(aliases);
            using var actual = new ValueContentAnalysisData();
            var expected = new Dictionary<AnalysisEntity, ValueContentAbstractValue>();
            if (childIndexFirst)
            {
                Assert.IsEmpty(actual.GetChildAnalysisEntityCandidates(entities[0].InstanceLocation));
            }

            foreach (var (index, literal) in new[] { (0, 0), (1, 2), (4, 4), (6, 6), (2, 1), (3, 3), (0, 5), (5, 7), (2, 1), (1, 9) })
            {
                var entity = entities[index];
                var value = ValueContentAbstractValue.Create(literal, type);
                SetRecursively(expected, entity, value);
                actual.SetAbstractValue(entity, value);
                Verify(expected, actual);
            }
        }

        [TestMethod]
        [DataRow(false)]
        [DataRow(true)]
        public void IndexedCandidatesTrackClearRemovalAndImport(bool aliases)
        {
            var (entities, type) = CreateEntities(aliases);
            using var actual = new ValueContentAnalysisData();
            var expected = new Dictionary<AnalysisEntity, ValueContentAbstractValue>();
            var zero = ValueContentAbstractValue.Create(0, type);
            var one = ValueContentAbstractValue.Create(1, type);
            var two = ValueContentAbstractValue.Create(2, type);
            actual.SetAbstractValue(entities[0], zero);
            SetRecursively(expected, entities[0], zero);
            actual.SetAbstractValue(entities[2], one);
            SetRecursively(expected, entities[2], one);
            actual.CoreAnalysisData.Clear();
            expected.Clear();
            actual.CoreAnalysisData.Add(entities[4], zero);
            expected.Add(entities[4], zero);
            actual.SetAbstractValue(entities[2], two);
            SetRecursively(expected, entities[2], two);
            Verify(expected, actual);
            actual.CoreAnalysisData.Remove(entities[2]);
            expected.Remove(entities[2]);
            actual.CoreAnalysisData[entities[3]] = one;
            expected[entities[3]] = one;
            actual.SetAbstractValue(entities[5], two);
            SetRecursively(expected, entities[5], two);
            Verify(expected, actual);
            using var clone = actual.Clone();
            clone.SetAbstractValue(entities[2], zero);
            SetRecursively(expected, entities[2], zero);
            Verify(expected, clone);
        }

        [TestMethod]
        public void IndexFreeComparisonMatchesProjectedEntityEquality()
        {
            var (entities, _) = CreateEntities(aliases: false);

            foreach (var first in entities)
            {
                foreach (var second in entities)
                {
                    Assert.AreEqual(first.WithIndices(second.Indices).Equals(second), first.EqualsIgnoringIndices(second));
                }
            }
        }

        [TestMethod]
        public void UnchangedValueContentMergesReuseExistingValues()
        {
            var (_, type) = CreateEntities(aliases: false);
            var zero = ValueContentAbstractValue.Create(0, type);
            var one = ValueContentAbstractValue.Create(1, type);
            var merged = zero.Merge(one);
            Assert.AreSame(zero, zero.Merge(zero));
            Assert.AreSame(merged, merged.Merge(zero));
            Assert.AreSame(merged, zero.Merge(merged));
            var maybe = merged.Merge(ValueContentAbstractValue.MayBeContainsNonLiteralState);
            Assert.AreEqual(ValueContainsNonLiteralState.Maybe, maybe.NonLiteralState);
            Assert.AreSame(maybe, maybe.Merge(zero));
            Assert.IsTrue(maybe.LiteralValues.SetEquals(merged.LiteralValues));
        }

        [TestMethod]
        [DataRow(2, 4, ValueContainsNonLiteralState.No)]
        [DataRow(3, 9, ValueContainsNonLiteralState.No)]
        [DataRow(4, 0, ValueContainsNonLiteralState.Maybe)]
        public void SelfMergePreservesBinaryProductLiteralBound(int width, int expectedCount, ValueContainsNonLiteralState expectedState)
        {
            var (_, type) = CreateEntities(aliases: false);
            ValueContentAbstractValue Merge(int[] literals)
                => literals.Take(width).Select(literal => ValueContentAbstractValue.Create(literal, type))
                    .Aggregate((first, second) => first.Merge(second));
            var product = Merge(new[] { 2, 3, 5, 7 }).MergeBinaryOperation(
                Merge(new[] { 11, 13, 17, 19 }), BinaryOperatorKind.Multiply, type, type, type);
            Assert.HasCount(width * width, product.LiteralValues);
            var merged = product.Merge(product);
            Assert.HasCount(expectedCount, merged.LiteralValues);
            Assert.AreEqual(expectedState, merged.NonLiteralState);
        }

        [TestMethod]
        [DataRow(10)]
        [DataRow(11)]
        [DataRow(16)]
        public void WideningMatchesRecursivePropagation(int writes)
        {
            var (entities, type) = CreateEntities(aliases: false, additionalIndices: writes - 1);
            var indexed = new[] { entities[2] }.Concat(entities.Skip(7)).ToArray();
            using var actual = new ValueContentAnalysisData();
            var expected = new Dictionary<AnalysisEntity, ValueContentAbstractValue>();
            var zero = ValueContentAbstractValue.Create(0, type);

            foreach (var entity in entities.Take(2))
            {
                actual.SetAbstractValue(entity, zero);
                SetRecursively(expected, entity, zero);
            }

            for (var index = 0; index < writes; index++)
            {
                var value = ValueContentAbstractValue.Create(index, type);
                SetRecursively(expected, indexed[index], value);
                actual.SetAbstractValue(indexed[index], value);
                Verify(expected, actual);
            }
        }

        [TestMethod]
        public void WideningPreservesDictionaryOrderAfterKeyReuse()
        {
            var (entities, type) = CreateEntities(aliases: false, additionalIndices: 15);
            var indexed = new[] { entities[2] }.Concat(entities.Skip(7)).ToArray();
            using var actual = new ValueContentAnalysisData();
            var expected = new Dictionary<AnalysisEntity, ValueContentAbstractValue>();
            var zero = ValueContentAbstractValue.Create(0, type);

            foreach (var entity in entities.Take(2).Concat(indexed))
            {
                actual.SetAbstractValue(entity, zero);
                SetRecursively(expected, entity, zero);
            }

            actual.CoreAnalysisData.Remove(entities[0]);
            expected.Remove(entities[0]);
            actual.CoreAnalysisData.Remove(indexed[1]);
            expected.Remove(indexed[1]);
            actual.CoreAnalysisData.Add(entities[6], zero);
            expected.Add(entities[6], zero);
            actual.CoreAnalysisData.Add(indexed[1], zero);
            expected.Add(indexed[1], zero);

            for (var index = 0; index < indexed.Length; index++)
            {
                var value = ValueContentAbstractValue.Create(index, type);
                SetRecursively(expected, indexed[index], value);
                actual.SetAbstractValue(indexed[index], value);
                Verify(expected, actual);
            }
        }

        [TestMethod]
        public void VirtualSettersObserveCompletedOverlappingUpdates()
        {
            var (entities, _) = CreateEntities(aliases: false, additionalIndices: 1);
            var first = entities[2];
            var second = entities[3];
            var third = entities[7];
            var completed = new List<AnalysisEntity>();
            using var actual = new CallbackAnalysisData((data, entity) =>
            {
                Assert.AreEqual(1, data.CoreAnalysisData[third]);
                completed.Add(entity);
            });
            actual.CoreAnalysisData.Add(first, 1);
            actual.CoreAnalysisData.Add(second, 0);
            actual.CoreAnalysisData.Add(third, 0);

            actual.SetAbstractValue(first, 1);

            Assert.AreSequenceEqual(new[] { third, second, third, first }, completed);
            Assert.IsTrue(actual.CoreAnalysisData.Values.All(value => value == 1));
        }

        private sealed class CallbackAnalysisData(Action<CallbackAnalysisData, AnalysisEntity> onCompleted)
            : AnalysisEntityBasedPredicateAnalysisData<int>
        {
            protected override AbstractValueDomain<int> ValueDomain => BitValueDomain.Instance;

            public override void SetAbstractValue(AnalysisEntity key, int value)
            {
                base.SetAbstractValue(key, value);
                onCompleted(this, key);
            }

            public override AnalysisEntityBasedPredicateAnalysisData<int> Clone() => throw new NotSupportedException();
            public override AnalysisEntityBasedPredicateAnalysisData<int> WithMergedData(
                AnalysisEntityBasedPredicateAnalysisData<int> data, MapAbstractDomain<AnalysisEntity, int> coreDataAnalysisDomain)
                => throw new NotSupportedException();
            public override int Compare(
                AnalysisEntityBasedPredicateAnalysisData<int> other, MapAbstractDomain<AnalysisEntity, int> coreDataAnalysisDomain)
                => throw new NotSupportedException();
        }

        private sealed class BitValueDomain : AbstractValueDomain<int>
        {
            public static readonly BitValueDomain Instance = new();
            public override int Bottom => 0;
            public override int UnknownOrMayBeValue => -1;
            public override int Merge(int value1, int value2) => value1 | value2;
            public override int Compare(int oldValue, int newValue, bool assertMonotonicity)
                => oldValue == newValue ? 0 : (oldValue | newValue) == newValue ? -1 : 1;
        }

        private static void Verify(
            Dictionary<AnalysisEntity, ValueContentAbstractValue> expected,
            AnalysisEntityBasedPredicateAnalysisData<ValueContentAbstractValue> actual)
        {
            Assert.HasCount(expected.Count, actual.CoreAnalysisData);

            foreach (var (entity, value) in expected)
            {
                Assert.IsTrue(actual.CoreAnalysisData.TryGetValue(entity, out var actualValue));
                Assert.AreEqual(value.NonLiteralState, actualValue.NonLiteralState);
                Assert.IsTrue(value.LiteralValues.SetEquals(actualValue.LiteralValues));
            }
        }

        private static void SetRecursively(
            Dictionary<AnalysisEntity, ValueContentAbstractValue> data, AnalysisEntity key, ValueContentAbstractValue value)
        {
            data[key] = value;
            if (key.Indices.All(index => index.IsConstant()))
            {
                return;
            }

            var updates = new List<(AnalysisEntity, ValueContentAbstractValue)>();

            foreach (var entity in data.Keys)
            {
                if (entity.Indices.Length != key.Indices.Length || entity == key)
                {
                    continue;
                }

                var overlaps = true;

                for (var index = 0; index < key.Indices.Length; index++)
                {
                    if (entity.Indices[index].IsConstant() && key.Indices[index].IsConstant() &&
                        !entity.Indices[index].Equals(key.Indices[index]))
                    {
                        overlaps = false;
                        break;
                    }
                }

                if (overlaps && entity.WithIndices(key.Indices).Equals(key))
                {
                    var merged = ValueContentAnalysis.ValueDomainInstance.Merge(value, data[entity]);
                    if (!data[entity].Equals(merged))
                    {
                        updates.Add((entity, merged));
                    }
                }
            }

            foreach (var (entity, merged) in updates)
            {
                SetRecursively(data, entity, merged);
            }
        }

        private (AnalysisEntity[] Entities, ITypeSymbol Type) CreateEntities(bool aliases, int additionalIndices = 0)
        {
            var tree = CSharpSyntaxTree.ParseText($$"""
                class C
                {
                    static void M(int[] first, int[] second, int index, int other)
                    {
                        first[index] = 0;
                        first[other] = 0;
                        {{string.Join("\n", Enumerable.Repeat("first[index] = 0;", additionalIndices))}}
                    }
                }
                """, cancellationToken: TestContext.CancellationToken);
            var compilation = CSharpCompilation.Create("OverlapTest", new[] { tree },
                new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) },
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var root = tree.GetRoot(TestContext.CancellationToken);
            var method = root.DescendantNodes().OfType<MethodDeclarationSyntax>().Single();
            var model = compilation.GetSemanticModel(tree);
            var owner = (IMethodSymbol)model.GetDeclaredSymbol(method, TestContext.CancellationToken);
            var firstLocation = PointsToAbstractValue.Create(AbstractLocation.CreateSymbolLocation(owner.Parameters[0], null), mayBeNull: false);
            var secondLocation = aliases ? firstLocation :
                PointsToAbstractValue.Create(AbstractLocation.CreateSymbolLocation(owner.Parameters[1], null), mayBeNull: false);
            var type = compilation.GetSpecialType(SpecialType.System_Int32);
            var references = model.GetOperation(method, TestContext.CancellationToken).DescendantsAndSelf()
                .OfType<IParameterReferenceOperation>().ToArray();
            var index = AbstractIndex.Create(references.First(operation => operation.Parameter.Name == "index"));
            var other = AbstractIndex.Create(references.First(operation => operation.Parameter.Name == "other"));
            AnalysisEntity Element(PointsToAbstractValue location, params AbstractIndex[] indices)
                => AnalysisEntity.Create(null, indices.ToImmutableArray(), type, location, parent: null, entityForInstanceLocation: null);
            return (new[]
            {
                Element(firstLocation, AbstractIndex.Create(0)),
                Element(firstLocation, AbstractIndex.Create(1)),
                Element(firstLocation, index),
                Element(firstLocation, other),
                Element(secondLocation, AbstractIndex.Create(0)),
                Element(secondLocation, index),
                Element(firstLocation, AbstractIndex.Create(0), AbstractIndex.Create(0))
            }.Concat(references.Where(operation => operation.Parameter.Name == "index").Skip(1)
                .Select(operation => Element(firstLocation, AbstractIndex.Create(operation)))).ToArray(), type);
        }
    }
}
