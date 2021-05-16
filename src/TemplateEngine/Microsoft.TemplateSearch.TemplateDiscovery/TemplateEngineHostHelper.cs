// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

#nullable enable

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Core.Util;
using Microsoft.TemplateEngine.Edge;

namespace Microsoft.TemplateSearch.TemplateDiscovery
{
    public static class TemplateEngineHostHelper
    {
        private const string DefaultHostVersion = "1.0.0";

        private static readonly Dictionary<string, string> DefaultPreferences = new Dictionary<string, string>
        {
            { "prefs:language", "C#" }
        };

        public static DefaultTemplateEngineHost CreateHost(string hostIdentifier, string? hostVersion = null, Dictionary<string, string>? preferences = null)
        {
            if (string.IsNullOrEmpty(hostIdentifier))
            {
                throw new ArgumentException("hostIdentifier cannot be null");
            }

            if (string.IsNullOrEmpty(hostVersion))
            {
                hostVersion = DefaultHostVersion;
            }

            if (preferences == null)
            {
                preferences = DefaultPreferences;
            }

            var builtIns = new List<(Type, IIdentifiedComponent)>();
            builtIns.AddRange(TemplateEngine.Edge.Components.AllComponents);
            builtIns.AddRange(TemplateEngine.Orchestrator.RunnableProjects.Components.AllComponents);

            // use "dotnetcli" as a fallback host so the correct host specific files are read.
            DefaultTemplateEngineHost host = new DefaultTemplateEngineHost(hostIdentifier, hostVersion, preferences, builtIns, new[] { "dotnetcli" });
            return host;
        }
    }
}
