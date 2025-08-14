// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable disable

namespace Microsoft.DotNet.Build.Tasks
{
    public class EnvironmentFilter
    {
        private const string MSBuildEnvironmentVariablePrefix = "MSBuild";
        private const string DotNetEnvironmentVariablePrefix = "DOTNET";
        private const string NugetEnvironmentVariablePrefix = "NUGET";

        private IEnumerable<string> _prefixesOfEnvironmentVariablesToRemove = new string []
        {
            MSBuildEnvironmentVariablePrefix,
            DotNetEnvironmentVariablePrefix,
            NugetEnvironmentVariablePrefix
        };

        private IEnumerable<string> _environmentVariablesToRemove = new string []
        {
            "CscToolExe", "VbcToolExe"
        };

        private IEnumerable<string> _environmentVariablesToKeep = new string []
        {
            "DOTNET_CLI_TELEMETRY_SESSIONID",
            "DOTNET_CLI_UI_LANGUAGE",
            "DOTNET_MULTILEVEL_LOOKUP",
            "DOTNET_RUNTIME_ID",
            "NUGET_PACKAGES"
        };

        public IEnumerable<string> GetEnvironmentVariableNamesToRemove()
        {
            var allEnvironmentVariableNames = Environment
                .GetEnvironmentVariables()
                .Keys
                .Cast<string>();

            var environmentVariablesToRemoveByPrefix = allEnvironmentVariableNames
                .Where(e => _prefixesOfEnvironmentVariablesToRemove.Any(p => e.StartsWith(p)));
                        
            var environmentVariablesToRemoveByName = allEnvironmentVariableNames
                .Where(e => _environmentVariablesToRemove.Contains(e));
            
            var environmentVariablesToRemove = environmentVariablesToRemoveByName
                .Concat(environmentVariablesToRemoveByPrefix)
                .Distinct()
                .Except(_environmentVariablesToKeep);
            
            return environmentVariablesToRemove;
        }
    }
}
