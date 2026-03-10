// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.CodeAnalysis.Tools.Commands;

namespace Microsoft.CodeAnalysis.Tools
{
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            var rootCommand = RootFormatCommand.GetCommand();
            return await rootCommand.Parse(args).InvokeAsync(null, CancellationToken.None);
        }
    }
}
