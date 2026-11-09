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
        [DataRow("1.0.0", "PackOne", "1.0.0", true)]
        [DataRow("1.0.0", "PackOne", "2.0.0", false)]
        [DataRow("1.0.0", "PackTwo", "1.0.0", false)]
        [DataRow("1.0.0", null, null, false)]
        [DataRow(null, null, null, true)]
        [DataRow("", null, null, true)]
        [DataRow("1.0.0", "packone", "1.0.0", false)]
        public void IsConfirmedAvailable_MatchesExactPackageIdentity(
            string? packageVersion,
            string? availablePackageId,
            string? availablePackageVersion,
            bool expected)
        {
            ITemplatePackageInfo packageInfo = new MockTemplatePackageInfo("PackOne", packageVersion);
            HashSet<PackageAvailabilityCandidate> availablePackages = [];
            if (availablePackageId != null)
            {
                availablePackages.Add(new(availablePackageId, availablePackageVersion!));
            }

            Assert.AreEqual(expected, CliTemplateSearchCoordinator.IsConfirmedAvailable(packageInfo, availablePackages));
        }
    }
}
