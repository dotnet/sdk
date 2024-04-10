/*// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolManifest;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.List;
using CreateShellShimRepository = Microsoft.DotNet.Tools.Tool.Install.CreateShellShimRepository;

namespace Microsoft.DotNet.Tools.Tool.Update
{
    internal class ToolUpdateAllCommand : CommandBase
    {
        private readonly bool _global;
        private readonly IReporter _reporter;

        // Testing for global update
        private readonly CreateToolPackageStoresAndDownloaderAndUninstaller _uninstaller;
        private readonly CreateShellShimRepository _createShell;

        private readonly CreateToolPackageStore _createToolPackageStore;

        // Testing for Local Update
        private readonly IToolPackageDownloader _toolPackageDownloader;
        private readonly IToolManifestFinder _toolManifestFinder;
        private readonly IToolManifestEditor _toolManifestEditor;
        private readonly ILocalToolsResolverCache _localToolsResolverCache;

        private readonly IToolManifestInspector _manifestInspector;


        *//*public ToolUpdateAllCommand(
            ParseResult parseResult,
            CreateToolPackageStoresAndDownloaderAndUninstaller createToolPackageStoreDownloaderUninstaller = null,
            CreateShellShimRepository createShellShimRepository = null,
            IReporter reporter = null,
            CreateToolPackageStore createToolPackageStore = null,
            IToolPackageDownloader toolPackageDownloader = null,
            IToolManifestFinder toolManifestFinder = null,
            IToolManifestEditor toolManifestEditor = null,
            ILocalToolsResolverCache localToolsResolverCache = null,
            IToolManifestInspector manifestInspector = null)
            : base(parseResult)
        {
            _global = parseResult.GetValue(ToolUpdateAllCommandParser.GlobalOption);
            _reporter = reporter;
            _uninstaller = createToolPackageStoreDownloaderUninstaller;
            _createShell = createShellShimRepository;
            _createToolPackageStore = createToolPackageStore;
            _toolPackageDownloader = toolPackageDownloader;
            _toolManifestFinder = toolManifestFinder;
            _toolManifestEditor = toolManifestEditor;
            _localToolsResolverCache = localToolsResolverCache;
            _manifestInspector = manifestInspector;
        }

        public override int Execute()
        {
            ToolAppliedOption.EnsureNoConflictGlobalLocalToolPathOption(
                _parseResult,
                LocalizableStrings.UpdateToolCommandInvalidGlobalAndLocalAndToolPath);

            if (_global)
            {
                UpdateAllGlobalTools();
            }
            else
            {
                UpdateAllLocalTools();
            }
            return 0;
        }

        private void UpdateAllGlobalTools()
        {
            var toolListCommand = new ToolListGlobalOrToolPathCommand(
                _parseResult,
                _createToolPackageStore
                );
            var toolList = toolListCommand.GetPackages(null, null);
            UpdateTools(toolList.Select(tool => tool.Id.ToString()), true, null);
        }

        private void UpdateAllLocalTools()
        {
            var toolListLocalCommand = new ToolListLocalCommand(_parseResult, _manifestInspector);
            var toolListLocal = toolListLocalCommand.GetPackages(null);
            foreach (var (package, manifestPath) in toolListLocal)
            {
                UpdateTools(new[] { package.PackageId.ToString() }, false, manifestPath.Value);
            }
        }*//*

        private void UpdateTools(IEnumerable<string> toolIds, bool isGlobal, string manifestPath)
        {
            foreach (var toolId in toolIds)
            {
                var args = BuildUpdateCommandArguments(
                    toolId: toolId,
                    isGlobal: isGlobal,
                    toolPath: _parseResult.GetValue(ToolUpdateAllCommandParser.ToolPathOption),
                    configFile: _parseResult.GetValue(ToolUpdateAllCommandParser.ConfigOption),
                    addSource: _parseResult.GetValue(ToolUpdateAllCommandParser.AddSourceOption),
                    framework: _parseResult.GetValue(ToolUpdateAllCommandParser.FrameworkOption),
                    prerelease: _parseResult.GetValue(ToolUpdateAllCommandParser.PrereleaseOption),
                    verbosity: _parseResult.GetValue(ToolUpdateAllCommandParser.VerbosityOption),
                    manifestPath: _parseResult.GetValue(ToolUpdateAllCommandParser.ToolManifestOption)
                );

                var toolParseResult = Parser.Instance.Parse(args);
                var toolUpdateCommand = new ToolUpdateCommand(
                    toolParseResult,
                    reporter: _reporter,
                    createToolPackageStoreDownloaderUninstaller: _uninstaller,
                    createShellShimRepository: _createShell,
                    toolPackageDownloader: _toolPackageDownloader,
                    toolManifestFinder: _toolManifestFinder,
                    toolManifestEditor: _toolManifestEditor,
                    localToolsResolverCache: _localToolsResolverCache
                    );
                toolUpdateCommand.Execute();
            }
        }

        private string[] BuildUpdateCommandArguments(string toolId,
            bool isGlobal,
            string toolPath,
            string configFile,
            string[] addSource,
            string framework,
            bool prerelease,
            VerbosityOptions verbosity,
            string manifestPath)
        {
            List<string> args = new List<string> { "dotnet", "tool", "update", toolId };

            if (isGlobal)
            {
                args.Add("--global");
            }
            else if (!string.IsNullOrEmpty(toolPath))
            {
                args.AddRange(new[] { "--tool-path", toolPath });
            }
            else
            {
                args.Add("--local");
            }

            if (!string.IsNullOrEmpty(configFile))
            {
                args.AddRange(new[] { "--configFile", configFile });
            }

            if (addSource != null && addSource.Length > 0)
            {
                foreach (var source in addSource)
                {
                    args.AddRange(new[] { "--add-source", source });
                }
            }

            if (!string.IsNullOrEmpty(framework))
            {
                args.AddRange(new[] { "--framework", framework });
            }

            if (prerelease)
            {
                args.Add("--prerelease");
            }

            if (!string.IsNullOrEmpty(manifestPath))
            {
                args.AddRange(new[] { "--tool-manifest", manifestPath });
            }

            args.AddRange(new[] { "--verbosity", verbosity.ToString() });

            return args.ToArray();
        }

    }
}

*/
