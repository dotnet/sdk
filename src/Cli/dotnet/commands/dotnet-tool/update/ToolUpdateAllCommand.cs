// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.CommandLine;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.DotNet.Cli;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.DotNet.ToolPackage;
using Microsoft.DotNet.Tools.Tool.Common;
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.DotNet.Tools.Tool.List;

namespace Microsoft.DotNet.Tools.Tool.Update
{
    internal class ToolUpdateAllCommand : CommandBase
    {
        private readonly bool _global;
        private readonly IReporter _reporter;

        public ToolUpdateAllCommand(ParseResult parseResult, IReporter reporter = null)
            : base(parseResult)
        {
            _global = parseResult.GetValue(ToolUpdateAllCommandParser.GlobalOption);
            _reporter = reporter;
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
            var toolListCommand = new ToolListGlobalOrToolPathCommand(_parseResult);
            var toolList = toolListCommand.GetPackages(null, null);
            UpdateTools(toolList.Select(tool => tool.Id.ToString()), true, null);
        }

        private void UpdateAllLocalTools()
        {
            var toolListLocalCommand = new ToolListLocalCommand(_parseResult);
            var toolListLocal = toolListLocalCommand.GetPackages(null);
            foreach (var (package, manifestPath) in toolListLocal)
            {
                UpdateTools(new[] { package.PackageId.ToString() }, false, manifestPath.Value);
            }
        }

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
                    manifestPath: manifestPath
                );

                var toolParseResult = Parser.Instance.Parse(args);
                var toolUpdateCommand = new ToolUpdateCommand(toolParseResult, reporter: _reporter);
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
