using System.CommandLine;
using Microsoft.DotNet.Cli;
namespace Microsoft.DotNet.Tools.Package.Search
{
    internal class PackageSearchCommand : CommandBase
    {
        private string _source;
        private string _searchArgument;
        private bool _exactMatch;
        private string _verbosity;
        private bool _prerelease;

        public PackageSearchCommand(ParseResult parseResult) : base(parseResult)
        {
            _source = parseResult.GetValue(PackageSearchCommandParser.Source);
            _searchArgument = parseResult.GetValue(PackageSearchCommandParser.SearchTermArgument);
            _exactMatch = parseResult.GetValue(PackageSearchCommandParser.ExactMatch);
            _verbosity = parseResult.GetValue(PackageSearchCommandParser.Verbosity);
            _prerelease = parseResult.GetValue(PackageSearchCommandParser.Prerelease);
        }
        public override int Execute()
        {
            Console.WriteLine(_searchArgument);
            return 0;
        }
    }
}
