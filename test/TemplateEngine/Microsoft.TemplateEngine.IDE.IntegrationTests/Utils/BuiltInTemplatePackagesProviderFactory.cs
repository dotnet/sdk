// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.IDE.IntegrationTests.Utils
{
    internal class BuiltInTemplatePackagesProviderFactory : ITemplatePackageProviderFactory
    {
        public string DisplayName => "IDE.IntegrationTests BuiltIn";

        public Guid Id { get; } = new Guid("{3227D09D-C1EA-48F1-A33B-1F132BFD9F01}");

        public ITemplatePackageProvider CreateProvider(IEngineEnvironmentSettings settings)
        {
            return new BuiltInTemplatePackagesProvider(this, settings);
        }

        private class BuiltInTemplatePackagesProvider : ITemplatePackageProvider
        {
            private readonly IEngineEnvironmentSettings settings;

            public BuiltInTemplatePackagesProvider(BuiltInTemplatePackagesProviderFactory factory, IEngineEnvironmentSettings settings)
            {
                this.settings = settings;
                this.Factory = factory;
            }

            event Action ITemplatePackageProvider.TemplatePackagesChanged
            {
                add { }
                remove { }
            }

            public ITemplatePackageProviderFactory Factory { get; }

            public Task<IReadOnlyList<ITemplatePackage>> GetAllTemplatePackagesAsync(CancellationToken cancellationToken)
            {
                List<ITemplatePackage> toInstallList = new List<ITemplatePackage>();
                string codebase = typeof(BootstrapperFactory).GetTypeInfo().Assembly.Location;
                Uri cb = new Uri(codebase);
                string asmPath = cb.LocalPath;
                string dir = Path.GetDirectoryName(asmPath);
                string[] locations = new[]
                {
                    Path.Combine(dir, "..", "..", "..", "..", "..", "template_feed"),
                    Path.Combine(dir, "..", "..", "..", "..", "..", "test", "Microsoft.TemplateEngine.TestTemplates", "test_templates")
                };

                foreach (string location in locations)
                {
                    if (Directory.Exists(location))
                    {
                        toInstallList.Add(new TemplatePackage(this, new DirectoryInfo(location).FullName, File.GetLastWriteTime(location)));
                    }
                }
                return Task.FromResult((IReadOnlyList<ITemplatePackage>)toInstallList);
            }
        }
    }
}
