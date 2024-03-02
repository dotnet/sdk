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
using Microsoft.DotNet.Tools.Tool.Install;
using Microsoft.DotNet.Tools.Tool.List;

namespace Microsoft.DotNet.Tools.Tool.Update
{
    internal class ToolUpdateAllCommand : CommandBase
    {
        private readonly bool _global;

        public ToolUpdateAllCommand(ParseResult parseResult)
            : base(parseResult)
        {
            _global = parseResult.GetValue(ToolUpdateAllCommandParser.GlobalOption);
        }

        public override int Execute()
        {
            if (_global)
            {
                ToolUpdateAllGlobalCommand(_parseResult);
            }
            else
            {
                ToolUpdateAllLocalCommand(_parseResult);
            }
            return 0;
        }

        private int ToolUpdateAllGlobalCommand(ParseResult parseResult)
        {
            var toolListCommand = new ToolListGlobalOrToolPathCommand(parseResult);
            var toolList = toolListCommand.GetPackages(null, null);

            foreach (var tool in toolList)
            {
                List<string> args = BuildUpdateCommandArguments(
                    toolId: tool.Id.ToString(),
                    isGlobal: true,
                    toolPath: null,
                    configFile: parseResult.GetValue(ToolUpdateAllCommandParser.ConfigOption),
                    addSource: parseResult.GetValue(ToolUpdateAllCommandParser.AddSourceOption),
                    framework: parseResult.GetValue(ToolUpdateAllCommandParser.FrameworkOption),
                    prerelease: parseResult.GetValue(ToolUpdateAllCommandParser.PrereleaseOption),
                    verbosity: parseResult.GetValue(ToolUpdateAllCommandParser.VersionOption),
                    manifestPath: null
                    );
                ParseResult toolParseResult = Parser.Instance.Parse(args.ToArray());
                var toolUpdateCommand = new ToolUpdateCommand(toolParseResult);
                toolUpdateCommand.Execute();
            }
            return 0;
        }

        private int ToolUpdateAllLocalCommand(ParseResult parseResult)
        {
            var toolListLocalCommand = new ToolListLocalCommand(parseResult);
            var toolListLocal = toolListLocalCommand.GetPackages(null);

            foreach (var (package, manifestPath) in toolListLocal)
            {
                // var argsList = new List<string> { "dotnet", "tool", "update", package.PackageId.ToString(), "--local" };
                List<string> argsList = BuildUpdateCommandArguments(
                    toolId: package.PackageId.ToString(),
                    isGlobal: false,
                    toolPath: parseResult.GetValue(ToolUpdateAllCommandParser.ToolPathOption),
                    configFile: parseResult.GetValue(ToolUpdateAllCommandParser.ConfigOption),
                    addSource: parseResult.GetValue(ToolUpdateAllCommandParser.AddSourceOption),
                    framework: parseResult.GetValue(ToolUpdateAllCommandParser.FrameworkOption),
                    prerelease: parseResult.GetValue(ToolUpdateAllCommandParser.PrereleaseOption),
                    verbosity: parseResult.GetValue(ToolUpdateAllCommandParser.VersionOption),
                    manifestPath: manifestPath.Value
                    );

                ParseResult toolParseResult = Parser.Instance.Parse(argsList.ToArray());
                var toolUpdateCommand = new ToolUpdateCommand(toolParseResult);
                toolUpdateCommand.Execute();
            }
            return 0;
        }

        private List<string> BuildUpdateCommandArguments(string toolId, bool isGlobal, string toolPath, string configFile, string[] addSource, string framework, bool prerelease, string verbosity, string manifestPath)
        {
            List<string> args = new List<string> { "dotnet", "tool", "update", toolId };

            if (isGlobal)
            {
                args.Add("--global");
            }
            else if (!string.IsNullOrEmpty(toolPath))
            {
                args.AddRange(new[] { "--tool-path", toolPath });
            } else
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
                args.AddRange(new[] {"--tool-manifest", manifestPath});
            }


            if (!string.IsNullOrEmpty(verbosity))
            {
                args.AddRange(new[] { "-v", verbosity });
            }

            return args;
        }

    }
}
