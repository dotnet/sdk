// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.TemplateEngine.Abstractions;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockEngineEnvironmentSettings : IEngineEnvironmentSettings
    {
        [Obsolete("ISettingsLoader is obsolete.")]
        public ISettingsLoader SettingsLoader { get { throw new NotImplementedException(); } }

        public ITemplateEngineHost Host { get; set; }

        public IEnvironment Environment { get; set; }

        public IPathInfo Paths { get { throw new NotImplementedException(); } }

        public IComponentManager Components { get; set; }
    }

    public class MockEnvironment : IEnvironment
    {
        private readonly Dictionary<string, string> _environmentVariables = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        public MockEnvironment(Dictionary<string, string> environmentVariablesToOverride = null)
        {
            var env = Environment.GetEnvironmentVariables();
            foreach (string key in env.Keys.OfType<string>())
            {
                _environmentVariables[key] = (env[key] as string) ?? string.Empty;
            }

            if (environmentVariablesToOverride == null)
            {
                return;
            }

            foreach (var item in environmentVariablesToOverride)
            {
                _environmentVariables[item.Key] = item.Value;
            }
        }

        public string NewLine { get; set; } = Environment.NewLine;

        public int ConsoleBufferWidth { get; set; } = 160;

        public string ExpandEnvironmentVariables(string name)
        {
            return Environment.ExpandEnvironmentVariables(name);
        }

        public string GetEnvironmentVariable(string name)
        {
            return _environmentVariables[name];
        }

        public IReadOnlyDictionary<string, string> GetEnvironmentVariables()
        {
            return _environmentVariables;
        }
    }
}
