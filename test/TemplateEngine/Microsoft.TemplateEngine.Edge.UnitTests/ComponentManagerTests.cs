// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.Installer;
using Microsoft.TemplateEngine.Edge.Settings;
using Microsoft.TemplateEngine.Mocks;
using Xunit;

namespace Microsoft.TemplateEngine.Edge.UnitTests
{
    public class ComponentManagerTests
    {
        [Fact]
        public void TestAllEdgeComponentsAdded()
        {
            var componentManager = new ComponentManager(
                new MockSettingsLoader(new MockEngineEnvironmentSettings()
                {
                    Host = new TestHelper.TestHost()
                }), new SettingsStore());

            var assemblyCatalog = new AssemblyComponentCatalog(new[] { typeof(ComponentManager).Assembly });
            var expectedTypeNames = assemblyCatalog.Select(pair => pair.Value().FullName).OrderBy(name => name);

            var actualTypeNames = componentManager._componentCache.Values.SelectMany(t => t.Values).Select(o => o.GetType().FullName).OrderBy(name => name);

            Assert.Equal(expectedTypeNames, actualTypeNames);
            Assert.Equal(2, componentManager.OfType<IInstallerFactory>().Count());
        }
    }
}
