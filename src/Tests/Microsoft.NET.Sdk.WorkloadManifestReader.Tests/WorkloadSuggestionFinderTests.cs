﻿// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using FluentAssertions;

using Microsoft.NET.Sdk.WorkloadManifestReader;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

using Xunit;

using WorkloadSuggestionCandidate = Microsoft.NET.Sdk.WorkloadManifestReader.WorkloadSuggestionFinder.WorkloadSuggestionCandidate;

namespace ManifestReaderTests
{
    public class WorkloadSuggestionFinderTests
    {
        private const string fakeRootPath = "fakeRootPath";

        [Fact]
        public void CanSuggestSimpleWorkload()
        {
            var manifestProvider = new FakeManifestProvider(Path.Combine("Manifests", "Sample.json"));
            var resolver = WorkloadResolver.CreateForTests(manifestProvider, fakeRootPath, ManifestTests.TEST_RUNTIME_IDENTIFIER_CHAIN);

            FakeFileSystemChecksSoThesePackagesAppearInstalled(resolver, "Xamarin.Android.Sdk", "Xamarin.Android.BuildTools");

            var suggestions = resolver.GetWorkloadSuggestionForMissingPacks(new[] { "Mono.Android.Sdk" });
            suggestions.Count().Should().Be(1);
            suggestions.First().Id.Should().Be("xamarin-android-build");
        }

        [Fact]
        public void CanSuggestTwoWorkloadsToFulfilTwoRequirements()
        {
            var manifestProvider = new FakeManifestProvider(Path.Combine("Manifests", "Sample.json"));
            var resolver = WorkloadResolver.CreateForTests(manifestProvider, fakeRootPath, ManifestTests.TEST_RUNTIME_IDENTIFIER_CHAIN);

            FakeFileSystemChecksSoThesePackagesAppearInstalled(resolver,
                //xamarin-android-build is fully installed
                "Xamarin.Android.Sdk",
                "Xamarin.Android.BuildTools",
                "Xamarin.Android.Framework",
                "Xamarin.Android.Runtime",
                "Mono.Android.Sdk");

            var suggestions = resolver.GetWorkloadSuggestionForMissingPacks(new[] { "Mono.Android.Runtime.x86", "Mono.Android.Runtime.Armv7a" });
            suggestions.Count().Should().Be(2);
            suggestions.Should().Contain(s => s.Id == "xamarin-android-build-armv7a");
            suggestions.Should().Contain(s => s.Id == "xamarin-android-build-x86");
        }

        [Fact]
        public void CanSuggestWorkloadThatFulfillsTwoRequirements()
        {
            var manifestProvider = new FakeManifestProvider(Path.Combine("Manifests", "Sample.json"));
            var resolver = WorkloadResolver.CreateForTests(manifestProvider, fakeRootPath, ManifestTests.TEST_RUNTIME_IDENTIFIER_CHAIN);

            FakeFileSystemChecksSoThesePackagesAppearInstalled(resolver,
                //xamarin-android-build is fully installed
                "Xamarin.Android.Sdk",
                "Xamarin.Android.BuildTools",
                "Xamarin.Android.Framework",
                "Xamarin.Android.Runtime",
                "Mono.Android.Sdk");

            var suggestions = resolver.GetWorkloadSuggestionForMissingPacks(new[] { "Xamarin.Android.Templates", "Xamarin.Android.LLVM.Aot.armv7a" });
            suggestions.Count().Should().Be(1);
            suggestions.First().Id.Should().Be("xamarin-android-complete");
        }

        [Fact]
        public static void CanFindSimpleAndPartialSuggestions()
        {
            var workloads = new(string workloadId, string[] packIds)[]
            {
                ("workload1", new[] { "pack1" }), // irrelevant
                ("workload2", new[] { "pack1", "pack2" }), //partial
                ("workload3", new[] { "pack1", "pack2", "pack4" }), //partial
                ("workload4", new[] { "pack2", "pack3" }), //complete
                ("workload5", new[] { "pack2", "pack3", "pack4" }), //complete
                ("workload6", new[] { "pack2", "pack4" }) //partial
            }
            .Select (a => (new WorkloadDefinitionId(a.workloadId), a.packIds.Select(p => new WorkloadPackId(p)).ToHashSet()));

            var requestedPacks = new[]
            {
                "pack2",
                "pack3"
            }
            .Select(s => new WorkloadPackId(s)).ToHashSet();

            WorkloadSuggestionFinder.FindPartialSuggestionsAndSimpleCompleteSuggestions(
                requestedPacks, workloads,
                out List<WorkloadSuggestionCandidate> partialSuggestions,
                out HashSet<WorkloadSuggestionCandidate> completeSimpleSuggestions);

            Assert.Equal(3, partialSuggestions.Count);
            Assert.Contains(partialSuggestions, p => p.Workloads.Single().ToString() == "workload2");
            Assert.Contains(partialSuggestions, p => p.Workloads.Single().ToString() == "workload3");
            Assert.Contains(partialSuggestions, p => p.Workloads.Single().ToString() == "workload6");

            Assert.Equal(2, completeSimpleSuggestions.Count);
            Assert.Contains(completeSimpleSuggestions, p => p.Workloads.Single().ToString() == "workload4");
            Assert.Contains(completeSimpleSuggestions, p => p.Workloads.Single().ToString() == "workload5");
        }

        [Fact]
        public static void SuggestionsArePermutedCorrectly()
        {
            static HashSet<WorkloadPackId> ConstructPackHash (params string[] packIds)
                => new HashSet<WorkloadPackId> (packIds.Select(id => new WorkloadPackId(id)));

            static HashSet<WorkloadDefinitionId> ConstructWorkloadHash (params string[] workloadIds)
                => new HashSet<WorkloadDefinitionId> (workloadIds.Select(id => new WorkloadDefinitionId(id)));

            static WorkloadSuggestionCandidate ConstructCandidate(string[] workloadIds, string[] packIds, string[] unsatisfiedPackIds)
                => new WorkloadSuggestionCandidate (ConstructWorkloadHash(workloadIds), ConstructPackHash(packIds), ConstructPackHash(unsatisfiedPackIds));

            //we're looking for suggestions with "pack1", "pack2", "pack3"
            var partialSuggestions = new List<WorkloadSuggestionCandidate>
            {
                ConstructCandidate(new[] { "workload1" }, new[] { "pack1" }, new[] { "pack2", "pack3" }),
                ConstructCandidate(new[] { "workload2" }, new[] { "pack1", "pack2" }, new[] { "pack3" }),
                ConstructCandidate(new[] { "workload3" }, new[] { "pack2" }, new[] { "pack1", "pack3" }),
                ConstructCandidate(new[] { "workload4" }, new[] { "pack3" }, new[] { "pack1", "pack2" }),
                ConstructCandidate(new[] { "workload5" }, new[] { "pack2", "pack3" }, new[] { "pack1" })
            };

            var completeSuggestions = WorkloadSuggestionFinder.GatherUniqueCompletePermutedSuggestions(partialSuggestions);

            Assert.Equal(4, completeSuggestions.Count);

            static int CountMatchingSuggestions(HashSet<WorkloadSuggestionCandidate> suggestions, params string[] workloadIds)
            {
                int found = 0;
                foreach(var suggestion in suggestions)
                {
                    if (suggestion.Workloads.Count == workloadIds.Length)
                    {
                        if (workloadIds.All(id => suggestion.Workloads.Contains(new WorkloadDefinitionId(id))))
                        {
                            found++;
                        }
                    }
                }
                return found;
            }

            Assert.Equal(1, CountMatchingSuggestions(completeSuggestions, "workload1", "workload3", "workload4"));
            Assert.Equal(1, CountMatchingSuggestions(completeSuggestions, "workload1", "workload5"));
            Assert.Equal(1, CountMatchingSuggestions(completeSuggestions, "workload2", "workload4"));
            Assert.Equal(1, CountMatchingSuggestions(completeSuggestions, "workload2", "workload5"));
        }

        [Fact]
        public static void CanDetermineBestSuggestion()
        {
            static WorkloadSuggestionFinder.WorkloadSuggestion Suggestion(int extraPacks, params string[] workloadIds)
                => new WorkloadSuggestionFinder.WorkloadSuggestion(new HashSet<WorkloadDefinitionId>(workloadIds.Select(id => new WorkloadDefinitionId(id))), extraPacks);

            var suggestions = new[]
            {
                Suggestion(10, "ReallyBigWorkloadWithLotsOfUnnecessaryPacks"),
                Suggestion(3, "ModerateWorkloadWithSomeUnnecessaryPacks"),
                Suggestion(0, "CombinationOfThreeWorkloads", "That", "HasNoExtraPacks"),
                Suggestion(0, "TheBest", "Match"), // is one of the suggestions with the fewest extra packs, and of those, the one with the fewest workloads
                Suggestion(2, "CombinationOfTwoWorkloads", "WithACoupleExtraPacks"),
            };

            var best = WorkloadSuggestionFinder.GetBestSuggestion(suggestions);

            Assert.Equal(0, best.ExtraPacks);
            Assert.Equal(2, best.Workloads.Count);
            Assert.Contains(new WorkloadDefinitionId("TheBest"), best.Workloads);
            Assert.Contains(new WorkloadDefinitionId("Match"), best.Workloads);
        }

        private static void FakeFileSystemChecksSoThesePackagesAppearInstalled(WorkloadResolver resolver, params string[] ids)
        {
            var installedPacks = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var id in ids)
            {
                installedPacks.Add(id);
            }

            resolver.ReplaceFilesystemChecksForTest(
                fileName =>
                {
                    var versionDir = Path.GetDirectoryName(fileName);
                    var idDir = Path.GetDirectoryName(versionDir);
                    return installedPacks.Contains(Path.GetFileName(idDir));
                },
                dirName =>
                {
                    var idDir = Path.GetDirectoryName(dirName);
                    return installedPacks.Contains(Path.GetFileName(idDir));
                });
        }
    }
}
