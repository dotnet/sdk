using Microsoft.TemplateEngine.Abstractions;
using System;
using System.Collections.Generic;
using System.Text;

namespace Microsoft.TemplateEngine.Mocks
{
    public class MockEngineEnvironmentSettings : IEngineEnvironmentSettings
    {
        public ISettingsLoader SettingsLoader { get { throw new NotImplementedException(); } }

        public ITemplateEngineHost Host { get { throw new NotImplementedException(); } }

        public IEnvironment Environment { get { throw new NotImplementedException(); } }

        public IPathInfo Paths { get { throw new NotImplementedException(); } }
    }
}
