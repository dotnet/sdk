// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using Microsoft.CodeAnalysis.Tools.Commands;

namespace Microsoft.CodeAnalysis.Tools
{
    internal class Program
    {
        private static async Task<int> Main(string[] args)
        {
            string? uiLanguage = Environment.GetEnvironmentVariable("DOTNET_CLI_UI_LANGUAGE");
            if (uiLanguage is not null)
            {
                try
                {
                    CultureInfo culture = new(uiLanguage);
                    CultureInfo.DefaultThreadCurrentUICulture = culture;
                    CultureInfo.CurrentUICulture = culture;
                }
                catch (CultureNotFoundException)
                {
                }
            }

            var rootCommand = RootFormatCommand.GetCommand();
            return await rootCommand.Parse(args).InvokeAsync(null, CancellationToken.None);
        }
    }
}
