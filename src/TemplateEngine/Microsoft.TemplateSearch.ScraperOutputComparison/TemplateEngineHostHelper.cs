// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using Microsoft.TemplateEngine.Edge;

namespace Microsoft.TemplateSearch.ScraperOutputComparison
{
    public static class TemplateEngineHostHelper
    {
        private const string DefaultHostVersion = "1.0.0";

        private static readonly Dictionary<string, string> DefaultPreferences = new Dictionary<string, string>
        {
            { "prefs:language", "C#" }
        };

        public static DefaultTemplateEngineHost CreateHost(string hostIdentifier)
        {
            if (string.IsNullOrEmpty(hostIdentifier))
            {
                throw new ArgumentException("hostIdentifier cannot be null");
            }
            // use "dotnetcli" as a fallback host so the correct host specific files are read.
            DefaultTemplateEngineHost host = new DefaultTemplateEngineHost(hostIdentifier, DefaultHostVersion, DefaultPreferences, null, new[] { "dotnetcli" });
            return host;
        }
    }
}
