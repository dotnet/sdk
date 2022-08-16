// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.TestHelper
{
    public class BuiltInTemplatePackagesProviderFactory : ITemplatePackageProviderFactory
    {
        public static List<(Type, IIdentifiedComponent)> GetComponents(params string[] pathsToProbe)
        {
            return new() { (typeof(ITemplatePackageProviderFactory), new BuiltInTemplatePackagesProviderFactory(pathsToProbe)) };
        }

        public static readonly Guid FactoryId = new Guid("{B9EE7CC5-D3AD-4982-94A4-CDF9E1C7FFCA}");
        private readonly IReadOnlyList<string> _pathsToProbe;

        public string DisplayName => "BuiltIn";

        public Guid Id => FactoryId;

        public BuiltInTemplatePackagesProviderFactory(params string[] pathsToProbe)
        {
            _pathsToProbe = pathsToProbe;
        }

        public ITemplatePackageProvider CreateProvider(IEngineEnvironmentSettings settings)
        {
            return new BuiltInTemplatePackagesProvider(this, settings, _pathsToProbe);
        }

        private class BuiltInTemplatePackagesProvider : ITemplatePackageProvider
        {
            private readonly IEngineEnvironmentSettings _settings;
            private readonly IReadOnlyList<string> _pathsToProbe;

            public BuiltInTemplatePackagesProvider(BuiltInTemplatePackagesProviderFactory factory, IEngineEnvironmentSettings settings, IReadOnlyList<string> pathsToProbe)
            {
                _settings = settings;
                _pathsToProbe = pathsToProbe;
                Factory = factory;
            }

#pragma warning disable CS0067

            public event Action? TemplatePackagesChanged;

#pragma warning restore CS0067

            public ITemplatePackageProviderFactory Factory { get; }

            public Task<IReadOnlyList<ITemplatePackage>> GetAllTemplatePackagesAsync(CancellationToken cancellationToken)
            {
                List<ITemplatePackage> toInstallList = new List<ITemplatePackage>();
                foreach (string location in _pathsToProbe)
                {
                    if (_settings.Host.FileSystem.DirectoryExists(location))
                    {
                        toInstallList.Add(new TemplatePackage(this, new DirectoryInfo(location).FullName, File.GetLastWriteTime(location)));
                    }
                    else
                    {
                        _settings.Host.Logger.LogWarning($"{location} doesn't exist.");
                    }
                }
                return Task.FromResult((IReadOnlyList<ITemplatePackage>)toInstallList);
            }
        }
    }
}
