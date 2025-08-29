// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Diagnostics;

namespace Microsoft.DotNet.Watch
{
    internal sealed class EnvironmentVariablesBuilder
    {
        public static IDictionary<string, string> FromCurrentEnvironment()
        {
            var builder = new Dictionary<string, string>();

            if (Environment.GetEnvironmentVariable(EnvironmentVariables.Names.DotNetStartupHooks) is { } dotnetStartupHooks)
            {
                builder[EnvironmentVariables.Names.DotNetStartupHooks] = dotnetStartupHooks;
            }

            if (Environment.GetEnvironmentVariable(EnvironmentVariables.Names.AspNetCoreHostingStartupAssemblies) is { } assemblies)
            {
                builder[EnvironmentVariables.Names.DotNetStartupHooks] = assemblies;
            }

            return builder;
        }
    }
}
