// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.CommandLine;

namespace Microsoft.TemplateSearch.TemplateDiscovery
{
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            CliCommand rootCommand = new TemplateDiscoveryCommand();
            return await rootCommand.Parse(args).InvokeAsync().ConfigureAwait(false);
        }
    }
}
