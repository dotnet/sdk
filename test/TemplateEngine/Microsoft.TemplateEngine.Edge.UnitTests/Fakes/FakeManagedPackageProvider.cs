// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using FakeItEasy;
using Microsoft.TemplateEngine.Abstractions;
using Microsoft.TemplateEngine.Abstractions.TemplatePackage;

namespace Microsoft.TemplateEngine.Edge.UnitTests.Fakes
{
    internal class FakeManagedPackageProviderFactory : ITemplatePackageProviderFactory
    {
        private static readonly List<WeakReference<ITemplatePackageProvider>> AllCreatedProviders = new List<WeakReference<ITemplatePackageProvider>>();

        public string DisplayName => nameof(FakeManagedPackageProviderFactory);

        public Guid Id { get; } = new Guid("{61CFA828-97B6-44EB-A44D-0AE673D6DF58}");

        public ITemplatePackageProvider CreateProvider(IEngineEnvironmentSettings settings)
        {
            var managedTemplatePackageProvider = new FakeManagedPackageProvider();
            AllCreatedProviders.Add(new WeakReference<ITemplatePackageProvider>(managedTemplatePackageProvider));
            return managedTemplatePackageProvider;
        }
    }

    internal class FakeManagedPackageProvider : ITemplatePackageProvider
    {
        private const string ManagedPackageMountPoint = "ManagedMount";
        private const string ManagedPackageIdentifier = "ManagedPackage";

        public ITemplatePackageProviderFactory Factory => new FakeManagedPackageProviderFactory();

        public event Action? TemplatePackagesChanged
        {
            add { }
            remove { }
        }

        public Task<IReadOnlyList<ITemplatePackage>> GetAllTemplatePackagesAsync(CancellationToken cancellationToken)
        {
            var managedTemplatePackage = A.Fake<IManagedTemplatePackage>();
            A.CallTo(() => managedTemplatePackage.MountPointUri).Returns(ManagedPackageMountPoint);
            A.CallTo(() => managedTemplatePackage.Identifier).Returns(ManagedPackageIdentifier);
            A.CallTo(() => managedTemplatePackage.Version).Returns("1.0.0");

            IReadOnlyList<ITemplatePackage> managedPackages = new List<ITemplatePackage> { managedTemplatePackage };
            return Task.FromResult(managedPackages);
        }
    }
}
