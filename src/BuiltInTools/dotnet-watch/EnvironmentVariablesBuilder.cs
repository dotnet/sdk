// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

namespace Microsoft.DotNet.Watcher
{
    internal sealed class EnvironmentVariablesBuilder : Dictionary<string, string>
    {
        private static readonly char s_startupHooksSeparator = Path.PathSeparator;
        private const char AssembliesSeparator = ';';

        public List<string> DotNetStartupHooks { get; } = [];
        public List<string> AspNetCoreHostingStartupAssemblies { get; } = [];

        public static EnvironmentVariablesBuilder FromCurrentEnvironment()
        {
            var builder = new EnvironmentVariablesBuilder();

            if (Environment.GetEnvironmentVariable(EnvironmentVariables.Names.DotnetStartupHooks) is { } dotnetStartupHooks)
            {
                builder.DotNetStartupHooks.AddRange(dotnetStartupHooks.Split(s_startupHooksSeparator));
            }

            if (Environment.GetEnvironmentVariable(EnvironmentVariables.Names.AspNetCoreHostingStartupAssemblies) is { } assemblies)
            {
                builder.DotNetStartupHooks.AddRange(assemblies.Split(AssembliesSeparator));
            }

            return builder;
        }

        public void AddToEnvironment(Dictionary<string, string> variables)
        {
            foreach (var (name, value) in this)
            {
                variables.Add(name, value);
            }

            variables.Add(EnvironmentVariables.Names.DotnetStartupHooks, string.Join(s_startupHooksSeparator, DotNetStartupHooks));
            variables.Add(EnvironmentVariables.Names.AspNetCoreHostingStartupAssemblies, string.Join(AssembliesSeparator, AspNetCoreHostingStartupAssemblies));
        }
    }
}
