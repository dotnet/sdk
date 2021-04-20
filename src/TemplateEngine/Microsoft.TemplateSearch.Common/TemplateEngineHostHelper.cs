// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Reflection;
using Microsoft.TemplateEngine.Edge;
using Microsoft.TemplateEngine.Utils;

namespace Microsoft.TemplateSearch.Common
{
    public static class TemplateEngineHostHelper
    {
        private static readonly string DefaultHostVersion = "1.0.0";

        private static readonly Dictionary<string, string> DefaultPreferences = new Dictionary<string, string>
        {
            { "prefs:language", "C#" }
        };

        public static DefaultTemplateEngineHost CreateHost(string hostIdentifier, string hostVersion = null, Dictionary<string, string> preferences = null)
        {
            if (string.IsNullOrEmpty(hostIdentifier))
            {
                throw new Exception("hostIdentifier cannot be null");
            }

            if (string.IsNullOrEmpty(hostVersion))
            {
                hostVersion = DefaultHostVersion;
            }

            if (preferences == null)
            {
                preferences = DefaultPreferences;
            }

            var builtIns = new AssemblyComponentCatalog(new[]
            {
                typeof(Microsoft.TemplateEngine.Orchestrator.RunnableProjects.Abstractions.IMacro).GetTypeInfo().Assembly,    // RPG
                typeof(Microsoft.TemplateEngine.Edge.Template.TemplateCreator).GetTypeInfo().Assembly,   // edge
            });

            // use "dotnetcli" as a fallback host so the correct host specific files are read.
            DefaultTemplateEngineHost host = new DefaultTemplateEngineHost(hostIdentifier, hostVersion, preferences, builtIns, new[] { "dotnetcli" });
            return host;
        }
    }
}
