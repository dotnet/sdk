using System.CommandLine;
using System.Diagnostics;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.commands.package.search;
using System;
namespace Microsoft.DotNet.Tools.Package.Search
{
    internal class PackageSearchCommand : CommandBase
    {
        private string _searchArgument;
        private List<string> _sources;
        private int? _take;
        private int? _skip;
        private bool _exactMatch;
        private bool _interactive;
        private bool _prerelease;
        private readonly NuGetSearchApiRequest _nugetToolSearchApiRequest;

        public PackageSearchCommand(ParseResult parseResult) : base(parseResult)
        {
            _sources = parseResult.GetValue(PackageSearchCommandParser.Sources);
            _take = parseResult.GetValue(PackageSearchCommandParser.Take);
            _skip = parseResult.GetValue(PackageSearchCommandParser.Skip);
            _searchArgument = parseResult.GetValue(PackageSearchCommandParser.SearchTermArgument);
            _exactMatch = parseResult.GetValue(PackageSearchCommandParser.ExactMatch);
            _interactive = parseResult.GetValue(PackageSearchCommandParser.Interactive);
            _prerelease = parseResult.GetValue(PackageSearchCommandParser.Prerelease);
            _nugetToolSearchApiRequest = new NuGetSearchApiRequest(_searchArgument, _skip, _take, _prerelease, _exactMatch, _sources);
        }
        public override int Execute()
        {
            Task.Run(() => _nugetToolSearchApiRequest.ExecuteCommandAsync()).Wait();
            return 0;
        }
    }
}
