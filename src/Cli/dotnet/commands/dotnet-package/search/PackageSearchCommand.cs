using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.NugetSearch;
using Microsoft.DotNet.Tools.Tool.Search; //searchresultpackage
namespace Microsoft.DotNet.Tools.Package.Search
{
    internal class PackageSearchCommand : CommandBase
    {
        private string _source;
        private string _searchArgument;
        private bool _exactMatch;
        private bool _prerelease;
        private readonly NugetSearchResultPrinter _searchResultPrinter;
        private readonly INugetSearchApiRequest _nugetToolSearchApiRequest;

        public PackageSearchCommand(ParseResult parseResult) : base(parseResult)
        {
            _source = parseResult.GetValue(PackageSearchCommandParser.Source);
            _searchArgument = parseResult.GetValue(PackageSearchCommandParser.SearchTermArgument);
            _exactMatch = parseResult.GetValue(PackageSearchCommandParser.ExactMatch);
            _prerelease = parseResult.GetValue(PackageSearchCommandParser.Prerelease);
            _searchResultPrinter = new NugetSearchResultPrinter(Reporter.Output);
            _nugetToolSearchApiRequest = new NugetSearchApiRequest();
        }
        public override int Execute()
        {
            NugetSearchApiParameter nugetSearchApiParameter = new NugetSearchApiParameter(_searchArgument, prerelease: _prerelease);
            IReadOnlyCollection<SearchResultPackage> searchResultPackages =
                NugetSearchApiResultDeserializer.Deserialize(
                    _nugetToolSearchApiRequest.GetResult(nugetSearchApiParameter).GetAwaiter().GetResult());

            _searchResultPrinter.Print(_exactMatch, _searchArgument, searchResultPackages);
            return 0;
        }
    }
}
