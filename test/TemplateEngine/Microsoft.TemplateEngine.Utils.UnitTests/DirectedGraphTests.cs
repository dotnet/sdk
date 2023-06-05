// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FluentAssertions;
using Xunit;

namespace Microsoft.TemplateEngine.Utils.UnitTests
{
    public class DirectedGraphTests
    {
        public static IEnumerable<object?[]> DirectedGraphHasCycleData()
        {
            List<int> empty = new List<int>();

            yield return new object?[] { new Dictionary<int, HashSet<int>?>(), false, empty, empty };
            yield return new object?[] { new Dictionary<int, HashSet<int>?>() { { 5, null } }, false, empty, new List<int>() { 5 } };
            yield return new object?[] { new Dictionary<int, HashSet<int>?>() { { 5, null }, { 6, null }, { 7, null } }, false, empty, new List<int>() { 5, 6, 7 } };
            yield return new object?[]
            {
                new Dictionary<int, HashSet<int>?>()
                {
                    { 5, new HashSet<int>() { 6, 7 } }, { 6, new HashSet<int>() { 7 } }, { 7, null }
                },
                false,
                empty,
                new List<int>() { 7, 6, 5 }
            };
            yield return new object?[]
            {
                new Dictionary<int, HashSet<int>?>()
                {
                    { 5, new HashSet<int>() { 6, 7, 8, 9, 10, 11 } },
                    { 6, new HashSet<int>() { 20, 40, 50 } },
                    { 7, new HashSet<int>() { 20, 8 } },
                    { 12, new HashSet<int>() { 5, 13, 9, 20, 100 } },
                    { 13, new HashSet<int>() { 20, 8, 30, 5 } }
                },
                false,
                empty,
                new List<int>() { 8, 9, 10, 11, 20, 40, 50, 100, 30, 7, 6, 5, 13, 12 }
            };

            yield return new object?[] { new Dictionary<int, HashSet<int>?>() { { 5, new HashSet<int>() { 5 } } }, true, new List<int>() { 5, 5 }, empty };
            yield return new object?[]
            {
                new Dictionary<int, HashSet<int>?>()
                {
                    { 5, new HashSet<int>() { 6 } }, { 6, new HashSet<int>() { 5 } }
                },
                true,
                new List<int>() { 5, 6, 5 },
                empty
            };
            yield return new object?[]
            {
                new Dictionary<int, HashSet<int>?>()
                {
                    { 5, new HashSet<int>() { 6, 7, 8, 9, 10, 11 } },
                    { 6, new HashSet<int>() { 20, 40, 50 } },
                    { 7, new HashSet<int>() { 20, 8 } },
                    { 12, new HashSet<int>() { 5, 13, 9, 20, 100 } },
                    { 13, new HashSet<int>() { 20, 8, 30, 5 } },
                    { 40, new HashSet<int>() { 5 } }
                },
                true,
                new List<int>() { 5, 6, 40, 5 },
                empty
            };
            yield return new object?[]
            {
                new Dictionary<int, HashSet<int>?>()
                {
                    { 5, new HashSet<int>() { 6, 7, 8, 9, 10, 11 } },
                    { 6, new HashSet<int>() { 20, 40, 50 } },
                    { 7, new HashSet<int>() { 20, 8 } },
                    { 12, new HashSet<int>() { 5, 13, 9, 20, 100 } },
                    { 13, new HashSet<int>() { 20, 8, 30, 5 } },
                    { 40, new HashSet<int>() { 6 } }
                },
                true,
                new List<int>() { 6, 40, 6 },
                empty
            };
        }

        [Theory]
        [MemberData(nameof(DirectedGraphHasCycleData))]
#pragma warning disable xUnit1026 // Theory methods should use all of their parameters
        public void HasCycleTests(Dictionary<int, HashSet<int>> dependencies, bool shouldHaveCycle, IReadOnlyList<int> expectedCycle, IReadOnlyList<int> unused)
#pragma warning restore xUnit1026 // Theory methods should use all of their parameters
        {
            new DirectedGraph<int>(dependencies).HasCycle(out IReadOnlyList<int> cycle).Should().Be(shouldHaveCycle);
            cycle.Should().BeEquivalentTo(expectedCycle, options => options.WithStrictOrdering());
            //cycle.SequenceEqual(expectedCycle).Should().BeTrue();
        }

        [Theory]
        [MemberData(nameof(DirectedGraphHasCycleData))]
#pragma warning disable xUnit1026 // Theory methods should use all of their parameters
        public void EnumerateTopologicalSortTests(Dictionary<int, HashSet<int>> dependencies, bool shouldHaveCycle, IReadOnlyList<int> unused, IReadOnlyList<int> expectedOrder)
#pragma warning restore xUnit1026 // Theory methods should use all of their parameters
        {
            new DirectedGraph<int>(dependencies).TryGetTopologicalSort(out IReadOnlyList<int> order).Should().Be(!shouldHaveCycle);
            if (!shouldHaveCycle)
            {
                order.Should().BeEquivalentTo(expectedOrder, options => options.WithStrictOrdering());
            }
        }

        public static IEnumerable<object?[]> DirectedGraphSubgraphData()
        {
            HashSet<int> empty = new HashSet<int>();

            Dictionary<int, HashSet<int>> graphA = new Dictionary<int, HashSet<int>>()
            {
                { 1, new HashSet<int>() { 10, 20, 30 } },
                { 10, new HashSet<int>() { 100, 200 } },
                { 20, new HashSet<int>() { 200 } },
                { 30, new HashSet<int>() { 300 } },
                { 300, new HashSet<int>() { 1000, 2000 } },
                { 1000, new HashSet<int>() { 20 } },
            };

            Dictionary<int, HashSet<int>> graphB = new Dictionary<int, HashSet<int>>()
            {
                { 1, new HashSet<int>() { 10, 20, 30 } },
                { 10, new HashSet<int>() { 100 } },
                { 20, new HashSet<int>() { 100 } },
                { 30, new HashSet<int>() { 10 } },
                { 100, new HashSet<int>() { 30 } },
            };

            yield return new object?[] { graphA, new List<int>() { 1 }, true, new Dictionary<int, HashSet<int>>() { { 1, empty } } };
            yield return new object?[] { graphA, new List<int>() { 1 }, false, new Dictionary<int, HashSet<int>>() };
            yield return new object?[] { graphA, new List<int>() { 10 }, true, new Dictionary<int, HashSet<int>>() { { 1, new HashSet<int>() { 10 } }, { 10, empty } } };
            yield return new object?[] { graphA, new List<int>() { 10 }, false, new Dictionary<int, HashSet<int>>() { { 1, empty } } };
            yield return new object?[]
            {
                graphA,
                new List<int>() { 20 },
                true,
                new Dictionary<int, HashSet<int>>()
                {
                    { 1, new HashSet<int>() { 20, 30 } },
                    { 1000, new HashSet<int>() { 20 } },
                    { 300, new HashSet<int>() { 1000 } },
                    { 30, new HashSet<int>() { 300 } },
                    { 20, empty }
                }
            };
            yield return new object?[]
            {
                graphA,
                new List<int>() { 20, 300, 30 },
                true,
                new Dictionary<int, HashSet<int>>()
                {
                    { 1, new HashSet<int>() { 20, 30 } },
                    { 1000, new HashSet<int>() { 20 } },
                    { 300, new HashSet<int>() { 1000 } },
                    { 30, new HashSet<int>() { 300 } },
                    { 20, empty }
                }
            };
            yield return new object?[]
            {
                graphA,
                new List<int>() { 20 },
                false,
                new Dictionary<int, HashSet<int>>()
                {
                    { 1, new HashSet<int>() { 30 } },
                    { 1000, new HashSet<int>() },
                    { 300, new HashSet<int>() { 1000 } },
                    { 30, new HashSet<int>() { 300 } },
                }
            };
            yield return new object?[]
            {
                graphA,
                new List<int>() { 20, 300, 30 },
                false,
                new Dictionary<int, HashSet<int>>()
                {
                    { 1, new HashSet<int>() { 30 } },
                    { 1000, new HashSet<int>() },
                    { 300, new HashSet<int>() { 1000 } },
                    { 30, new HashSet<int>() { 300 } },
                }
            };
            yield return new object?[]
            {
                graphA,
                new List<int>() { 100, 30 },
                false,
                new Dictionary<int, HashSet<int>>()
                {
                    { 1, new HashSet<int>() { 10 } },
                    { 10, empty },
                }
            };

            yield return new object?[] { graphB, new List<int>() { 10 }, true, graphB };
            yield return new object?[] { graphB, new List<int>() { 10 }, false, graphB };
            yield return new object?[] { graphB, new List<int>() { 10, 100 }, true, graphB };
            yield return new object?[] { graphB, new List<int>() { 10, 100 }, false, graphB };
            yield return new object?[] { graphB, new List<int>() { 1 }, true, new Dictionary<int, HashSet<int>>() { { 1, empty } } };
            yield return new object?[] { graphB, new List<int>() { 1 }, false, new Dictionary<int, HashSet<int>>() };

            yield return new object?[] { graphB, new List<int>() { 20 }, true, new Dictionary<int, HashSet<int>>() { { 1, new HashSet<int>() { 20 } }, { 20, empty } } };
            yield return new object?[] { graphB, new List<int>() { 20 }, false, new Dictionary<int, HashSet<int>>() { { 1, empty } } };
        }

        [Theory]
        [MemberData(nameof(DirectedGraphSubgraphData))]
        public void GetSubGraphDependandOnVerticesTests(Dictionary<int, HashSet<int>> dependencies, IReadOnlyList<int> vertices, bool includeSeedVertices, Dictionary<int, HashSet<int>> expectedResult)
        {
            var result = new DirectedGraph<int>(dependencies).GetSubGraphDependandOnVertices(vertices, includeSeedVertices);
            TestAreEquivalent(result, expectedResult);
        }

        private static void TestAreEquivalent(DirectedGraph<int> actual, Dictionary<int, HashSet<int>> expected)
        {
            actual.DependenciesMap.Keys.Should()
                .BeEquivalentTo(expected.Keys, options => options.WithoutStrictOrdering());

            foreach (int key in expected.Keys)
            {
                actual.DependenciesMap[key].Should().BeEquivalentTo(expected[key], options => options.WithoutStrictOrdering());
            }
        }
    }
}
