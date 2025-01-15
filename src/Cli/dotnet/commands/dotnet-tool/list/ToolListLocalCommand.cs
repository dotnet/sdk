// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.Extensions.EnvironmentAbstractions;

namespace Microsoft.DotNet.Tools.Tool.List
{
    internal class ToolListLocalCommand : CommandBase
    {
        private readonly IToolManifestInspector _toolManifestInspector;
        private readonly IReporter _reporter;
        private const string CommandDelimiter = ", ";

        public ToolListLocalCommand(
            ParseResult parseResult,
            IToolManifestInspector toolManifestInspector = null,
            IReporter reporter = null)
            : base(parseResult)
        {
            _reporter = (reporter ?? Reporter.Output);

            _toolManifestInspector = toolManifestInspector ??
                                     new ToolManifestFinder(new DirectoryPath(Directory.GetCurrentDirectory()));
        }

        public override int Execute()
        {
            var packageIdArgument = _parseResult.GetValue(ToolListCommandParser.PackageIdArgument);
            PackageId? packageId = null;
            if (!string.IsNullOrWhiteSpace(packageIdArgument))
            {
                packageId = new PackageId(packageIdArgument);
            }
            var packageEnumerable = GetPackages(packageId);

            var formatValue = _parseResult.GetValue(ToolListCommandParser.ToolListFormatOption);
            if (formatValue is ToolListOutputFormat.json)
            {
                PrintJson(packageEnumerable);
            }
            else
            {
                PrintTable(packageEnumerable);
            }

            if (packageId.HasValue && !packageEnumerable.Any())
            {
                // return 1 if target package was not found
                return 1;
            }
            return 0;
        }

        public IEnumerable<(ToolManifestPackage, FilePath)> GetPackages(PackageId? packageId)
        {
            return _toolManifestInspector.Inspect().Where(
                 (t) => PackageIdMatches(t.toolManifestPackage, packageId)
                 );
        }

        private bool PackageIdMatches(ToolManifestPackage package, PackageId? packageId)
        {
            return !packageId.HasValue || package.PackageId.Equals(packageId);
        }

        private void PrintTable(IEnumerable<(ToolManifestPackage toolManifestPackage, FilePath SourceManifest)> packageEnumerable)
        {
            var table = new PrintableTable<(ToolManifestPackage toolManifestPackage, FilePath SourceManifest)>();
            table.AddColumn(
                LocalizableStrings.PackageIdColumn,
                p => p.toolManifestPackage.PackageId.ToString());
            table.AddColumn(
                LocalizableStrings.VersionColumn,
                p => p.toolManifestPackage.Version.ToNormalizedString());
            table.AddColumn(
                LocalizableStrings.CommandsColumn,
                p => string.Join(CommandDelimiter, p.toolManifestPackage.CommandNames.Select(c => c.Value)));
            table.AddColumn(
                LocalizableStrings.ManifestFileColumn,
                p => p.SourceManifest.Value);
            table.PrintRows(packageEnumerable, l => _reporter.WriteLine(l));
        }

        private void PrintJson(IEnumerable<(ToolManifestPackage toolManifestPackage, FilePath SourceManifest)> packageEnumerable)
        {
            var jsonData = new VersionedDataContract<LocalToolListJsonContract[]>()
            {
                Data = packageEnumerable.Select(p => new LocalToolListJsonContract
                {
                    PackageId = p.toolManifestPackage.PackageId.ToString(),
                    Version = p.toolManifestPackage.Version.ToNormalizedString(),
                    Commands = p.toolManifestPackage.CommandNames.Select(c => c.Value).ToArray(),
                    Manifest = p.SourceManifest.Value
                }).ToArray()
            };
            var jsonText = System.Text.Json.JsonSerializer.Serialize(jsonData, JsonHelper.NoEscapeSerializerOptions);
            _reporter.WriteLine(jsonText);
        }
    }
}
