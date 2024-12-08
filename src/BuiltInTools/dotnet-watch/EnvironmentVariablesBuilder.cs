// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Watch
{
    internal sealed class EnvironmentVariablesBuilder
    {
        private static readonly char s_startupHooksSeparator = Path.PathSeparator;
        private const char AssembliesSeparator = ';';

        public List<string> DotNetStartupHooks { get; } = [];
        public List<string> AspNetCoreHostingStartupAssemblies { get; } = [];

        /// <summary>
        /// Environment variables set on the dotnet run process.
        /// </summary>
        private readonly Dictionary<string, string> _variables = [];

        public static EnvironmentVariablesBuilder FromCurrentEnvironment()
        {
            var builder = new EnvironmentVariablesBuilder();

            if (Environment.GetEnvironmentVariable(EnvironmentVariables.Names.DotNetStartupHooks) is { } dotnetStartupHooks)
            {
                builder.DotNetStartupHooks.AddRange(dotnetStartupHooks.Split(s_startupHooksSeparator));
            }

            if (Environment.GetEnvironmentVariable(EnvironmentVariables.Names.AspNetCoreHostingStartupAssemblies) is { } assemblies)
            {
                builder.AspNetCoreHostingStartupAssemblies.AddRange(assemblies.Split(AssembliesSeparator));
            }

            return builder;
        }

        public void SetVariable(string name, string value)
        {
            // should use AspNetCoreHostingStartupAssembliesVariable/DotNetStartupHookDirective
            Debug.Assert(!name.Equals(EnvironmentVariables.Names.AspNetCoreHostingStartupAssemblies, StringComparison.OrdinalIgnoreCase));
            Debug.Assert(!name.Equals(EnvironmentVariables.Names.DotNetStartupHooks, StringComparison.OrdinalIgnoreCase));

            _variables[name] = value;
        }

        public void SetProcessEnvironmentVariables(ProcessSpec processSpec)
        {
            foreach (var (name, value) in GetEnvironment())
            {
                processSpec.EnvironmentVariables.Add(name, value);
            }
        }

        public IEnumerable<(string name, string value)> GetEnvironment()
        {
            foreach (var (name, value) in _variables)
            {
                yield return (name, value);
            }

            if (DotNetStartupHooks is not [])
            {
                yield return (EnvironmentVariables.Names.DotNetStartupHooks, string.Join(s_startupHooksSeparator, DotNetStartupHooks));
            }

            if (AspNetCoreHostingStartupAssemblies is not [])
            {
                yield return (EnvironmentVariables.Names.AspNetCoreHostingStartupAssemblies, string.Join(AssembliesSeparator, AspNetCoreHostingStartupAssemblies));
            }
        }
    }
}
