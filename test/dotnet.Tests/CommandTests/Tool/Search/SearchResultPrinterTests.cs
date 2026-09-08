// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.DotNet.Cli.Commands;
using Microsoft.DotNet.Cli.Commands.Tool.Search;
using Microsoft.DotNet.Cli.ToolPackage;
using NuGet.Configuration;

namespace dotnet.Tests.ToolSearchTests
{
    [TestClass]
    public class SearchResultPrinterTests
    {
        private readonly BufferedReporter _reporter;
        private readonly BufferedReporter _errorReporter;
        private readonly SearchResultPrinter _searchResultPrinter;
        private readonly SearchResultPackage _filledSearchResultPackage;
        private readonly SearchResultPackage _mostEmptyToCheckNullException;

        public SearchResultPrinterTests()
        {
            _reporter = new BufferedReporter();
            _errorReporter = new BufferedReporter();
            _searchResultPrinter = new SearchResultPrinter(_reporter, _errorReporter);

            _filledSearchResultPackage = new SearchResultPackage(
                new PackageId("my.tool"),
                "1.0.0",
                "my tool description",
                "my tool summary",
                new List<string> { "tag1", "tag2" },
                new List<string> { "author1", "author2" },
                10,
                true,
                new List<SearchResultPackageVersion> { new SearchResultPackageVersion("1.0.0", 10) });
            _mostEmptyToCheckNullException = new SearchResultPackage(
                new PackageId("my.tool"),
                "1.0.0",
                null,
                null,
                new List<string>(),
                new List<string>(),
                1244,
                true,
                new List<SearchResultPackageVersion> { new SearchResultPackageVersion("1.0.0", 10), new SearchResultPackageVersion("0.9.0", 1234) });
        }

        [TestMethod]
        public void WhenDetailedIsFalseResultHasNecessaryInfo()
        {
            var searchResultPackages =
                new List<SearchResultPackage> { _filledSearchResultPackage, _mostEmptyToCheckNullException };
            _searchResultPrinter.Print(false, searchResultPackages);

            string[] expectedInformation =
            {
                _filledSearchResultPackage.Id.ToString(), _filledSearchResultPackage.Authors.First(),
                _filledSearchResultPackage.TotalDownloads.ToString(),
                _filledSearchResultPackage.Versions.First().Version,
                _filledSearchResultPackage.Versions.First().Downloads.ToString(),
                _mostEmptyToCheckNullException.Id.ToString()
            };

            foreach (var expectedInformationToBePresent in expectedInformation)
                _reporter.Lines.Should().Contain(l => l.Contains(expectedInformationToBePresent),
                    $"Expect \"{expectedInformationToBePresent}\" to be present");

            _reporter.Lines.Should().NotContain(l => l.Contains(Required(_filledSearchResultPackage.Description)));
            _reporter.Lines.Should().NotContain(l => l.Contains(Required(_filledSearchResultPackage.Summary)));
            _reporter.Lines.Should().NotContain(l => l.Contains(_filledSearchResultPackage.Tags.First()));
        }

        [TestMethod]
        public void WhenDetailedIsTrueResultHasNecessaryInfo()
        {
            var searchResultPackages =
                new List<SearchResultPackage> { _filledSearchResultPackage, _mostEmptyToCheckNullException };
            _searchResultPrinter.Print(true, searchResultPackages);

            string[] expectedInformation =
            {
                _filledSearchResultPackage.Id.ToString(), _filledSearchResultPackage.Authors.First(),
                _filledSearchResultPackage.TotalDownloads.ToString(),
                _filledSearchResultPackage.Versions.First().Version,
                _filledSearchResultPackage.Versions.First().Downloads.ToString(),
                _mostEmptyToCheckNullException.Id.ToString(), Required(_filledSearchResultPackage.Description),
                Required(_filledSearchResultPackage.Summary), _filledSearchResultPackage.Tags.First(),
                _filledSearchResultPackage.Versions.First().Version,
                _filledSearchResultPackage.Versions.First().Downloads.ToString(),
            };

            foreach (var expectedInformationToBePresent in expectedInformation)
                _reporter.Lines.Should().Contain(l => l.Contains(expectedInformationToBePresent),
                    $"Expect \"{expectedInformationToBePresent}\" to be present");

            _reporter.Lines.Should().ContainSingle(l => l.Contains($"{CliCommandStrings.Authors}:"));
            _reporter.Lines.Should().ContainSingle(l => l.Contains($"{CliCommandStrings.Tags}:"));
        }

        [TestMethod]
        public void WhenInputIsEmptyDetailIsFalseItShouldPrintNoResultMessage()
        {
            var searchResultPackages =
                new List<SearchResultPackage>();
            _searchResultPrinter.Print(false, searchResultPackages);
            _reporter.Lines.Count.Should().Be(1);
            _reporter.Lines.Should().Contain(CliCommandStrings.NoResult);
        }

        [TestMethod]
        public void WhenInputIsEmptyDetailIsTrueItShouldPrintNoResultMessage()
        {
            var searchResultPackages =
                new List<SearchResultPackage>();
            _searchResultPrinter.Print(true, searchResultPackages);
            _reporter.Lines.Count.Should().Be(1);
            _reporter.Lines.Should().Contain(CliCommandStrings.NoResult);
        }

        [TestMethod]
        public void PrintSourceHeadingWritesTheSourceUrlToTheReporter()
        {
            var source = new PackageSource("https://contoso.example/v3/index.json");

            _searchResultPrinter.PrintSourceHeading(source);

            _reporter.Lines.Should().ContainSingle(l => l.Contains(source.Source));
            _errorReporter.Lines.Should().BeEmpty();
        }

        [TestMethod]
        public void PrintSourceFailureWritesTheHeadingAndMessageToTheErrorReporter()
        {
            var source = new PackageSource("https://contoso.example/v3/index.json");
            const string failureMessage = "Something went wrong contacting the source.";

            _searchResultPrinter.PrintSourceFailure(source, failureMessage);

            _errorReporter.Lines.Should().Contain(l => l.Contains(source.Source));
            _errorReporter.Lines.Should().Contain(failureMessage);
            _reporter.Lines.Should().BeEmpty();
        }

        [TestMethod]
        public void PrintInvalidSourceWritesToTheErrorReporter()
        {
            const string invalidSource = "not a valid source";

            _searchResultPrinter.PrintInvalidSource(invalidSource);

            _errorReporter.Lines.Should().ContainSingle(l => l.Contains(invalidSource));
            _reporter.Lines.Should().BeEmpty();
        }

        [TestMethod]
        public void WhenErrorReporterIsNotProvidedItDefaultsToReporterError()
        {
            var printer = new SearchResultPrinter(_reporter);
            var source = new PackageSource("https://contoso.example/v3/index.json");

            Action a = () => printer.PrintSourceFailure(source, "boom");
            a.Should().NotThrow();
        }

        private static string Required(string? value) =>
            value ?? throw new InvalidOperationException("The test package metadata must contain this value.");
    }
}
