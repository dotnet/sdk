// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Watcher
{
    internal sealed class EnvironmentVariablesBuilder
    {
        private static readonly char s_startupHooksSeparator = Path.PathSeparator;
        private const char AssembliesSeparator = ';';

        public List<string> DotNetStartupHookDirective { get; } = [];
        public List<string> AspNetCoreHostingStartupAssembliesVariable { get; } = [];

        /// <summary>
        /// Environment variables set on the dotnet run process.
        /// </summary>
        private readonly Dictionary<string, string> _variables = [];

        /// <summary>
        /// Environment variables passed as directives on command line (dotnet [env:name=value] run).
        /// Currently, the effect is the same as setting <see cref="_variables"/> due to
        /// https://github.com/dotnet/sdk/issues/40484
        /// </summary>
        private readonly Dictionary<string, string> _directives = [];

        public static EnvironmentVariablesBuilder FromCurrentEnvironment()
        {
            var builder = new EnvironmentVariablesBuilder();

            if (Environment.GetEnvironmentVariable(EnvironmentVariables.Names.DotnetStartupHooks) is { } dotnetStartupHooks)
            {
                builder.DotNetStartupHookDirective.AddRange(dotnetStartupHooks.Split(s_startupHooksSeparator));
            }

            if (Environment.GetEnvironmentVariable(EnvironmentVariables.Names.AspNetCoreHostingStartupAssemblies) is { } assemblies)
            {
                builder.AspNetCoreHostingStartupAssembliesVariable.AddRange(assemblies.Split(AssembliesSeparator));
            }

            return builder;
        }

        public void SetDirective(string name, string value)
        {
            // should use DotNetStartupHookDirective
            Debug.Assert(!name.Equals(EnvironmentVariables.Names.DotnetStartupHooks, StringComparison.OrdinalIgnoreCase));

            _directives[name] = value;
        }

        public void SetVariable(string name, string value)
        {
            // should use AspNetCoreHostingStartupAssembliesVariable
            Debug.Assert(!name.Equals(EnvironmentVariables.Names.AspNetCoreHostingStartupAssemblies, StringComparison.OrdinalIgnoreCase));

            _variables[name] = value;
        }

        public void ConfigureProcess(ProcessSpec processSpec)
        {
            processSpec.Arguments = [.. GetCommandLineDirectives(), .. processSpec.Arguments];
            AddToEnvironment(processSpec.EnvironmentVariables);
        }

        // for testing
        internal void AddToEnvironment(Dictionary<string, string> variables)
        {
            foreach (var (name, value) in _variables)
            {
                variables.Add(name, value);
            }

            if (AspNetCoreHostingStartupAssembliesVariable is not [])
            {
                variables.Add(EnvironmentVariables.Names.AspNetCoreHostingStartupAssemblies, string.Join(AssembliesSeparator, AspNetCoreHostingStartupAssembliesVariable));
            }
        }

        // for testing
        internal IEnumerable<string> GetCommandLineDirectives()
        {
            foreach (var (name, value) in _directives)
            {
                yield return MakeDirective(name, value);
            }

            if (DotNetStartupHookDirective is not [])
            {
                yield return MakeDirective(EnvironmentVariables.Names.DotnetStartupHooks, string.Join(s_startupHooksSeparator, DotNetStartupHookDirective));
            }

            static string MakeDirective(string name, string value)
                => $"[env:{name}={value}]";
        }
    }
}
