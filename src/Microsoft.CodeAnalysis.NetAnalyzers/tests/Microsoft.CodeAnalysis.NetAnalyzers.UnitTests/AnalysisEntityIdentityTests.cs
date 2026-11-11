// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow;
using Microsoft.CodeAnalysis.FlowAnalysis.DataFlow.PointsToAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Microsoft.CodeAnalysis.NetAnalyzers.UnitTests
{
    [TestClass]
    public class AnalysisEntityIdentityTests
    {
        public TestContext TestContext { get; set; }

        [TestMethod]
        public void CollidingSymbolsKeepDistinctEntityKeys()
        {
            var (compilation, first, second, location) = CreateCollidingFields();
            Assert.IsFalse(SymbolEqualityComparer.Default.Equals(first.Symbol, second.Symbol));
            VerifyDistinct(first, second);
            Assert.IsFalse(first.EqualsIgnoringInstanceLocation(second));
            Assert.IsFalse(first.EqualsIgnoringIndices(second));
            var equivalent = AnalysisEntity.Create(
                HashCollisionProxy.Create((IFieldSymbol)compilation.GetTypeByMetadataName("C").GetMembers("First").Single()),
                first.Indices, first.Type, location, null, null);
            Assert.AreEqual(first.GetHashCode(), equivalent.GetHashCode());
            Assert.IsTrue(first.Equals(equivalent));
            using var map = new DictionaryAnalysisData<AnalysisEntity, int>();
            map.Add(first, 1);
            map.Add(second, 2);
            Assert.HasCount(2, map);
        }

        [TestMethod]
        public void CollidingTypesKeepDistinctEntityKeys()
        {
            var (compilation, _, _, _) = CreateCollidingFields();
            var firstType = HashCollisionProxy.Create<ITypeSymbol>(compilation.GetSpecialType(SpecialType.System_Int32));
            var secondType = HashCollisionProxy.Create<ITypeSymbol>(compilation.GetSpecialType(SpecialType.System_String));
            var indices = ImmutableArray.Create(AbstractIndex.Create(0));
            var first = AnalysisEntity.Create(null, indices, firstType, PointsToAbstractValue.UnknownNotNull, null, null);
            var second = AnalysisEntity.Create(null, indices, secondType, PointsToAbstractValue.UnknownNotNull, null, null);
            VerifyDistinct(first, second);
            Assert.IsFalse(first.EqualsIgnoringInstanceLocation(second));
            Assert.IsFalse(first.EqualsIgnoringIndices(second));
        }

        [TestMethod]
        public void DefaultSymbolEqualityAndHashingIgnoreNullableAnnotations()
        {
            var (compilation, _, _, _) = CreateCollidingFields();
            var type = compilation.GetSpecialType(SpecialType.System_String);
            var indices = ImmutableArray.Create(AbstractIndex.Create(0));
            var first = AnalysisEntity.Create(null, indices, type.WithNullableAnnotation(NullableAnnotation.Annotated),
                PointsToAbstractValue.UnknownNotNull, null, null);
            var second = AnalysisEntity.Create(null, indices, type.WithNullableAnnotation(NullableAnnotation.NotAnnotated),
                PointsToAbstractValue.UnknownNotNull, null, null);
            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
            Assert.IsTrue(first.Equals(second));
        }

        [TestMethod]
        public void ParentIdentitySurvivesHashCollisions()
        {
            var (compilation, first, second, location) = CreateCollidingFields();
            var member = (IFieldSymbol)compilation.GetTypeByMetadataName("Holder").GetMembers("Value").Single();
            var firstChild = AnalysisEntity.Create(member, ImmutableArray<AbstractIndex>.Empty, member.Type, location, first, null);
            var secondChild = AnalysisEntity.Create(member, ImmutableArray<AbstractIndex>.Empty, member.Type, location, second, null);
            VerifyDistinct(firstChild, secondChild);
            Assert.IsFalse(firstChild.EqualsIgnoringInstanceLocation(secondChild));
            Assert.IsFalse(firstChild.EqualsIgnoringIndices(secondChild));
        }

        [TestMethod]
        public void LocationsAndEntityBasedIndicesKeepDistinctKeys()
        {
            var (_, first, second, _) = CreateCollidingFields();
            var firstLocation = AbstractLocation.CreateSymbolLocation(first.Symbol, null);
            var secondLocation = AbstractLocation.CreateSymbolLocation(second.Symbol, null);
            Assert.AreEqual(firstLocation.GetHashCode(), secondLocation.GetHashCode());
            Assert.IsFalse(firstLocation.Equals(secondLocation));
            var firstPoints = PointsToAbstractValue.Create(firstLocation, mayBeNull: false);
            var secondPoints = PointsToAbstractValue.Create(secondLocation, mayBeNull: false);
            Assert.AreEqual(firstPoints.GetHashCode(), secondPoints.GetHashCode());
            Assert.IsFalse(firstPoints.Equals(secondPoints));
            var locations = ImmutableHashSet.Create(firstLocation, secondLocation);
            Assert.HasCount(2, locations);
            var points = PointsToAbstractValue.Create(locations, NullAbstractValue.NotNull);
            Assert.IsTrue(points.Equals(PointsToAbstractValue.Create(
                ImmutableHashSet.Create(secondLocation, firstLocation), NullAbstractValue.NotNull)));
            var firstIndex = AbstractIndex.Create(first);
            var secondIndex = AbstractIndex.Create(second);
            Assert.AreEqual(firstIndex.GetHashCode(), secondIndex.GetHashCode());
            Assert.IsFalse(firstIndex.Equals(secondIndex));
            var firstElement = AnalysisEntity.Create(null, ImmutableArray.Create(AbstractIndex.Create(0)),
                first.Type, firstPoints, null, null);
            var secondElement = AnalysisEntity.Create(null, firstElement.Indices, first.Type, secondPoints, null, null);
            VerifyDistinct(firstElement, secondElement);
            Assert.IsTrue(firstElement.EqualsIgnoringInstanceLocation(secondElement));
        }

        [TestMethod]
        public void DefaultLocationsAndReferenceRootsUseEntityIdentity()
        {
            var (_, first, second, _) = CreateCollidingFields();
            var firstLocation = AbstractLocation.CreateAnalysisEntityDefaultLocation(first);
            var secondLocation = AbstractLocation.CreateAnalysisEntityDefaultLocation(second);
            Assert.AreEqual(firstLocation.GetHashCode(), secondLocation.GetHashCode());
            Assert.IsFalse(firstLocation.Equals(secondLocation));
            var sharedLocation = PointsToAbstractValue.Create(
                ImmutableHashSet.Create(firstLocation, secondLocation), NullAbstractValue.NotNull);
            var indices = ImmutableArray.Create(AbstractIndex.Create(0));
            var firstElement = AnalysisEntity.Create(null, indices, first.Type, sharedLocation, null, first);
            var secondElement = AnalysisEntity.Create(null, indices, first.Type, sharedLocation, null, second);
            VerifyDistinct(firstElement, secondElement);
            Assert.IsFalse(firstElement.EqualsIgnoringInstanceLocation(secondElement));
        }

        [TestMethod]
        public void OperationAndCallStackIdentityDoNotUseHashes()
        {
            var tree = CSharpSyntaxTree.ParseText("""
                class C
                {
                    static object Make() => new object();
                    static void M()
                    {
                        object first = Make();
                        object second = Make();
                        first = null;
                        second = null;
                    }
                }
                """, cancellationToken: TestContext.CancellationToken);
            var compilation = CSharpCompilation.Create("OperationIdentity", new[] { tree }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var model = compilation.GetSemanticModel(tree);
            var method = tree.GetRoot(TestContext.CancellationToken).DescendantNodes()
                .OfType<MethodDeclarationSyntax>().Single(method => method.Identifier.ValueText == "M");
            var operation = model.GetOperation(method, TestContext.CancellationToken);
            var invocations = operation.DescendantsAndSelf().OfType<IInvocationOperation>().ToArray();
            var first = HashCollisionProxy.Create<IOperation>(invocations[0]);
            var second = HashCollisionProxy.Create<IOperation>(invocations[1]);
            var firstIndex = AbstractIndex.Create(first);
            var secondIndex = AbstractIndex.Create(second);
            Assert.AreEqual(firstIndex.GetHashCode(), secondIndex.GetHashCode());
            Assert.IsFalse(firstIndex.Equals(secondIndex));
            Assert.IsTrue(firstIndex.Equals(AbstractIndex.Create(first)));
            var type = compilation.GetSpecialType(SpecialType.System_Object);
            var firstLocation = AbstractLocation.CreateAllocationLocation(first, type, ImmutableStack<IOperation>.Empty);
            var secondLocation = AbstractLocation.CreateAllocationLocation(second, type, ImmutableStack<IOperation>.Empty);
            Assert.AreEqual(firstLocation.GetHashCode(), secondLocation.GetHashCode());
            Assert.IsFalse(firstLocation.Equals(secondLocation));
            var firstStack = ImmutableStack<IOperation>.Empty.Push(first);
            var secondStack = ImmutableStack<IOperation>.Empty.Push(second);
            var sameCreation = invocations[0];
            firstLocation = AbstractLocation.CreateAllocationLocation(sameCreation, type, firstStack);
            secondLocation = AbstractLocation.CreateAllocationLocation(sameCreation, type, secondStack);
            Assert.AreEqual(firstLocation.GetHashCode(), secondLocation.GetHashCode());
            Assert.IsFalse(firstLocation.Equals(secondLocation));
            Assert.IsTrue(firstLocation.Equals(AbstractLocation.CreateAllocationLocation(
                sameCreation, type, ImmutableStack<IOperation>.Empty.Push(first))));
            var references = operation.DescendantsAndSelf().OfType<ILocalReferenceOperation>().ToArray();
            var firstCapture = PointsToAbstractValue.Create(HashCollisionProxy.Create<IOperation>(references[0]));
            var secondCapture = PointsToAbstractValue.Create(HashCollisionProxy.Create<IOperation>(references[1]));
            Assert.AreEqual(firstCapture.GetHashCode(), secondCapture.GetHashCode());
            Assert.IsFalse(firstCapture.Equals(secondCapture));
        }

        [TestMethod]
        public void MapMergeKeepsUnmatchedKeysInCollidingBuckets()
        {
            var (_, first, second, _) = CreateCollidingFields();
            using var firstMap = new DictionaryAnalysisData<AnalysisEntity, int> { [first] = 1 };
            using var secondMap = new DictionaryAnalysisData<AnalysisEntity, int> { [second] = 2 };
            using var merged = new BitMapDomain().Merge(firstMap, secondMap);
            Assert.HasCount(2, merged);
            Assert.AreEqual(1, merged[first]);
            Assert.AreEqual(2, merged[second]);
        }

        private (Compilation Compilation, AnalysisEntity First, AnalysisEntity Second, PointsToAbstractValue Location) CreateCollidingFields()
        {
            var tree = CSharpSyntaxTree.ParseText("""
                struct Holder { public int Value; }
                class C { public Holder First; public Holder Second; }
                """, cancellationToken: TestContext.CancellationToken);
            var compilation = CSharpCompilation.Create("EntityIdentity", new[] { tree }, References,
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));
            var owner = compilation.GetTypeByMetadataName("C");
            var location = PointsToAbstractValue.Create(AbstractLocation.CreateThisOrMeLocation(owner, null), mayBeNull: false);
            AnalysisEntity Create(string name)
            {
                var symbol = HashCollisionProxy.Create((IFieldSymbol)owner.GetMembers(name).Single());
                return AnalysisEntity.Create(symbol, ImmutableArray<AbstractIndex>.Empty, symbol.Type, location, null, null);
            }

            return (compilation, Create("First"), Create("Second"), location);
        }

        private static void VerifyDistinct(AnalysisEntity first, AnalysisEntity second)
        {
            Assert.AreEqual(first.GetHashCode(), second.GetHashCode());
            Assert.IsFalse(first.Equals(second));
            Assert.HasCount(2, new HashSet<AnalysisEntity> { first, second });
        }

        private sealed class BitMapDomain() : AnalysisEntityMapAbstractDomain<int>(BitValueDomain.Instance, null)
        {
            protected override int GetDefaultValue(AnalysisEntity analysisEntity) => 0;
            protected override bool CanSkipNewEntry(AnalysisEntity analysisEntity, int value) => false;
            protected override void AssertValidEntryForMergedMap(AnalysisEntity analysisEntity, int value) { }
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

        private static MetadataReference[] References => new[] { MetadataReference.CreateFromFile(typeof(object).Assembly.Location) };
    }
}
