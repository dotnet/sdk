// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.TemplateEngine.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockEngineEnvironmentSettings : IEngineEnvironmentSettings
    {
        public ISettingsLoader SettingsLoader { get { throw new NotImplementedException(); } }

        public ITemplateEngineHost Host { get; set; }

        public IEnvironment Environment { get; set; }

        public IPathInfo Paths { get { throw new NotImplementedException(); } }
    }

    public class MockEnvironment : IEnvironment
    {
        public string NewLine { get; set; } = Environment.NewLine;

        public int ConsoleBufferWidth { get; set; } = 160;

        public string ExpandEnvironmentVariables(string name)
        {
            throw new NotImplementedException();
        }

        public string GetEnvironmentVariable(string name)
        {
            throw new NotImplementedException();
        }

        public IReadOnlyDictionary<string, string> GetEnvironmentVariables()
        {
            throw new NotImplementedException();
        }
    }
}
