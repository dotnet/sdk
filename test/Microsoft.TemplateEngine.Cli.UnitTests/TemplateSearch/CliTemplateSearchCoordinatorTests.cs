// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Cli.NuGet;
using Microsoft.TemplateEngine.Cli.TemplateSearch;
using Microsoft.TemplateEngine.Mocks;
using Microsoft.TemplateSearch.Common.Abstractions;

namespace Microsoft.TemplateEngine.Cli.UnitTests.TemplateSearch
{
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
        public void IsConfirmedAvailable_PackageIdComparisonIsCaseSensitive()
        {
            ITemplatePackageInfo packageInfo = new MockTemplatePackageInfo("PackOne", "1.0.0");
            IReadOnlySet<PackageAvailabilityCandidate> availablePackages = new HashSet<PackageAvailabilityCandidate>
            {
                new("packone", "1.0.0"),
            };

            Assert.IsFalse(CliTemplateSearchCoordinator.IsConfirmedAvailable(packageInfo, availablePackages));
        }
    }
}
