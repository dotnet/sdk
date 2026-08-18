// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Cli.NuGet;
using Microsoft.TemplateEngine.Cli.TemplateSearch;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateSearch.Common.Abstractions;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateSearch
{
    /// <summary>
    /// Focused tests for <see cref="CliTemplateSearchCoordinator.IsConfirmedAvailable(ITemplatePackageInfo, IReadOnlySet{PackageAvailabilityCandidate})"/>,
    /// which narrows .NET template catalog hits down to package id + version pairs confirmed available
    /// from at least one of the NuGet feeds selected for the invocation.
    /// </summary>
    [TestClass]
    public class CliTemplateSearchCoordinatorTests : BaseTest
    {
        [TestMethod]
        public void IsConfirmedAvailable_ExactIdAndVersionMatch_ReturnsTrue()
        {
            ITemplatePackageInfo packageInfo = new MockTemplatePackageInfo("PackOne", "1.0.0");
            IReadOnlySet<PackageAvailabilityCandidate> availablePackages = new HashSet<PackageAvailabilityCandidate>
            {
                new("PackOne", "1.0.0"),
            };

            Assert.IsTrue(CliTemplateSearchCoordinator.IsConfirmedAvailable(packageInfo, availablePackages));
        }

        [TestMethod]
        public void IsConfirmedAvailable_VersionMismatch_ReturnsFalse()
        {
            ITemplatePackageInfo packageInfo = new MockTemplatePackageInfo("PackOne", "1.0.0");
            IReadOnlySet<PackageAvailabilityCandidate> availablePackages = new HashSet<PackageAvailabilityCandidate>
            {
                new("PackOne", "2.0.0"),
            };

            Assert.IsFalse(CliTemplateSearchCoordinator.IsConfirmedAvailable(packageInfo, availablePackages));
        }

        [TestMethod]
        public void IsConfirmedAvailable_DifferentPackageId_ReturnsFalse()
        {
            ITemplatePackageInfo packageInfo = new MockTemplatePackageInfo("PackOne", "1.0.0");
            IReadOnlySet<PackageAvailabilityCandidate> availablePackages = new HashSet<PackageAvailabilityCandidate>
            {
                new("PackTwo", "1.0.0"),
            };

            Assert.IsFalse(CliTemplateSearchCoordinator.IsConfirmedAvailable(packageInfo, availablePackages));
        }

        [TestMethod]
        public void IsConfirmedAvailable_EmptyAvailableSet_ReturnsFalse()
        {
            ITemplatePackageInfo packageInfo = new MockTemplatePackageInfo("PackOne", "1.0.0");
            IReadOnlySet<PackageAvailabilityCandidate> availablePackages = new HashSet<PackageAvailabilityCandidate>();

            Assert.IsFalse(CliTemplateSearchCoordinator.IsConfirmedAvailable(packageInfo, availablePackages));
        }

        [TestMethod]
        public void IsConfirmedAvailable_NullVersionOnCatalogHit_PassesThroughAsAvailable()
        {
            // The catalog does not always report a version for a hit; when it doesn't, there is no
            // package id + version pair to validate against a feed, so the hit is passed through unfiltered.
            ITemplatePackageInfo packageInfo = new MockTemplatePackageInfo("PackOne", version: null);
            IReadOnlySet<PackageAvailabilityCandidate> availablePackages = new HashSet<PackageAvailabilityCandidate>();

            Assert.IsTrue(CliTemplateSearchCoordinator.IsConfirmedAvailable(packageInfo, availablePackages));
        }

        [TestMethod]
        public void IsConfirmedAvailable_EmptyVersionOnCatalogHit_PassesThroughAsAvailable()
        {
            ITemplatePackageInfo packageInfo = new MockTemplatePackageInfo("PackOne", version: string.Empty);
            IReadOnlySet<PackageAvailabilityCandidate> availablePackages = new HashSet<PackageAvailabilityCandidate>();

            Assert.IsTrue(CliTemplateSearchCoordinator.IsConfirmedAvailable(packageInfo, availablePackages));
        }

        [TestMethod]
        public void IsConfirmedAvailable_VersionComparisonIsCaseSensitive()
        {
            // PackageAvailabilityCandidate is a record struct with default (ordinal) equality; a differently-cased
            // package id must not be treated as a match.
            ITemplatePackageInfo packageInfo = new MockTemplatePackageInfo("PackOne", "1.0.0");
            IReadOnlySet<PackageAvailabilityCandidate> availablePackages = new HashSet<PackageAvailabilityCandidate>
            {
                new("packone", "1.0.0"),
            };

            Assert.IsFalse(CliTemplateSearchCoordinator.IsConfirmedAvailable(packageInfo, availablePackages));
        }
    }
}
