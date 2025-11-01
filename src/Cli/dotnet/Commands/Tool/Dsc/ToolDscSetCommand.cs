// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

using System.CommandLine;
using System.Text.Json;
using Microsoft.DotNet.Cli.Commands.Tool.Install;
using Microsoft.DotNet.Cli.ToolPackage;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.Extensions.EnvironmentAbstractions;
using NuGet.Versioning;

namespace Microsoft.DotNet.Cli.Commands.Tool.Dsc;

internal class ToolDscSetCommand : CommandBase
{
    private readonly string _input;

    public ToolDscSetCommand(ParseResult parseResult)
        : base(parseResult)
    {
        _input = parseResult.GetValue(ToolDscSetCommandParser.InputOption);
    }

    public override int Execute()
    {
        try
        {
            var inputState = DscWriter.ReadAndDeserializeInput(_input);
            var resultState = new DscToolsState();
            bool hasFailures = false;

            foreach (var tool in inputState?.Tools ?? Enumerable.Empty<DscToolState>())
            {
                // Skip tools that should not exist (Exist == false)
                if (tool.Exist == false)
                {
                    DscWriter.WriteDebug($"Skipping tool {tool.PackageId}: _exist is false (tool removal not supported)");
                    continue;
                }

                // Only support Global scope for now
                var scope = tool.Scope ?? DscToolScope.Global;
                if (scope != DscToolScope.Global)
                {
                    DscWriter.WriteDebug($"Skipping tool {tool.PackageId}: only Global scope is currently supported");
                    continue;
                }

                DscWriter.WriteDebug($"Setting desired state for tool: {tool.PackageId}");

                // Parse packageId and version
                var (packageId, versionRange) = tool.ParsePackageIdentity();
                
                if (string.IsNullOrEmpty(packageId))
                {
                    DscWriter.WriteError($"Invalid packageId: {tool.PackageId}");
                    continue;
                }

                // Install or update the tool
                try
                {
                    string installArgs = $"tool install -g {packageId}";
                    if (versionRange != null)
                    {
                        installArgs += $" --version \"{versionRange.OriginalString}\"";
                    }

                    var installParseResult = Parser.Parse($"dotnet {installArgs}");
                    
                    var installCommand = new ToolInstallGlobalOrToolPathCommand(
                        installParseResult,
                        packageId: new PackageId(packageId),
                        createToolPackageStoreDownloaderUninstaller: null,
                        createShellShimRepository: null,
                        environmentPathInstruction: null,
                        reporter: null);

                    int exitCode = installCommand.Execute();

                    if (exitCode != 0)
                    {
                        DscWriter.WriteError($"Failed to install/update tool {packageId}");
                        hasFailures = true;
                        continue;
                    }

                    DscWriter.WriteDebug($"Tool {packageId} is at desired state");
                }
                catch (Exception ex)
                {
                    DscWriter.WriteError($"Error installing/updating tool {packageId}: {ex.Message}");
                    hasFailures = true;
                    continue;
                }

                // Query final state after installation
                var finalState = DscWriter.QueryToolState(new DscToolState 
                { 
                    PackageId = packageId, 
                    Scope = scope 
                });

                resultState.Tools.Add(finalState);
            }

            DscWriter.WriteResult(resultState);
            return hasFailures ? 1 : 0;
        }
        catch (Exception ex)
        {
            DscWriter.WriteError($"Unexpected error: {ex.Message}");
            return 1;
        }
    }
}
