// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

//namespace Microsoft.TemplateEngine.Edge.Settings
//{
//    public class SourceRepositoryDependencyProvider : IRemoteDependencyProvider
//    {
//        private readonly object _lock = new object();
//        private readonly SourceRepository _sourceRepository;
//        private readonly ILogger _logger;
//        private readonly SourceCacheContext _cacheContext;
//        private FindPackageByIdResource _findPackagesByIdResource;
//        private readonly bool _ignoreFailedSources;
//        private readonly bool _ignoreWarning;

//        // Limiting concurrent requests to limit the amount of files open at a time on Mac OSX
//        // the default is 256 which is easy to hit if we don't limit concurrency
//        private static readonly SemaphoreSlim Throttle =
//            RuntimeEnvironmentHelper.IsMacOSX
//                ? new SemaphoreSlim(ConcurrencyLimit, ConcurrencyLimit)
//                : null;

//        // In order to avoid too many open files error, set concurrent requests number to 16 on Mac
//        private const int ConcurrencyLimit = 16;

//        public SourceRepositoryDependencyProvider(
//            SourceRepository sourceRepository,
//            ILogger logger,
//            SourceCacheContext cacheContext)
//            : this(sourceRepository, logger, cacheContext, cacheContext.IgnoreFailedSources)
//        {
//        }

//        public SourceRepositoryDependencyProvider(
//            SourceRepository sourceRepository,
//            ILogger logger,
//            SourceCacheContext cacheContext,
//            bool ignoreFailedSources)
//            : this(sourceRepository, logger, cacheContext, ignoreFailedSources, false)
//        {
//        }

//        public SourceRepositoryDependencyProvider(
//            SourceRepository sourceRepository,
//            ILogger logger,
//            SourceCacheContext cacheContext,
//            bool ignoreFailedSources,
//            bool ignoreWarning)
//        {
//            _sourceRepository = sourceRepository;
//            _logger = logger;
//            _cacheContext = cacheContext;
//            _ignoreFailedSources = ignoreFailedSources;
//            _ignoreWarning = ignoreWarning;
//        }

//        public bool IsHttp => _sourceRepository.PackageSource.IsHttp;

//        public async Task<LibraryIdentity> FindLibraryAsync(LibraryRange libraryRange, NuGetFramework targetFramework, CancellationToken cancellationToken)
//        {
//            await EnsureResource();

//            IEnumerable<NuGetVersion> packageVersions;

//            try
//            {
//                if (Throttle != null)
//                {
//                    await Throttle.WaitAsync();
//                }
//                packageVersions = await _findPackagesByIdResource.GetAllVersionsAsync(libraryRange.Name, cancellationToken);
//            }
//            catch (FatalProtocolException e) when (_ignoreFailedSources)
//            {
//                if (!_ignoreWarning)
//                {
//                    _logger.LogWarning(e.Message);
//                }
//                return null;
//            }
//            finally
//            {
//                Throttle?.Release();
//            }

//            NuGetVersion packageVersion = packageVersions?.FindBestMatch(libraryRange.VersionRange, version => version);

//            if (packageVersion != null)
//            {
//                return new LibraryIdentity
//                {
//                    Name = libraryRange.Name,
//                    Version = packageVersion,
//                    Type = LibraryType.Package
//                };
//            }

//            return null;
//        }

//        public async Task<IEnumerable<LibraryDependency>> GetDependenciesAsync(LibraryIdentity match, NuGetFramework targetFramework, CancellationToken cancellationToken)
//        {
//            await EnsureResource();

//            FindPackageByIdDependencyInfo packageInfo;
//            try
//            {
//                if (Throttle != null)
//                {
//                    await Throttle.WaitAsync();
//                }
//                packageInfo = await _findPackagesByIdResource.GetDependencyInfoAsync(match.Name, match.Version, cancellationToken);
//            }
//            catch (FatalProtocolException e) when (_ignoreFailedSources)
//            {
//                if (!_ignoreWarning)
//                {
//                    _logger.LogWarning(e.Message);
//                }
//                return new List<LibraryDependency>();
//            }
//            finally
//            {
//                Throttle?.Release();
//            }

//            return GetDependencies(packageInfo, targetFramework);
//        }

//        public async Task CopyToAsync(LibraryIdentity identity, Stream stream, CancellationToken cancellationToken)
//        {
//            await EnsureResource();

//            try
//            {
//                if (Throttle != null)
//                {
//                    await Throttle.WaitAsync();
//                }

//                using (Stream nupkgStream = await _findPackagesByIdResource.GetNupkgStreamAsync(identity.Name, identity.Version, cancellationToken))
//                {
//                    cancellationToken.ThrowIfCancellationRequested();

//                    // If the stream is already available, do not stop in the middle of copying the stream
//                    // Pass in CancellationToken.None
//                    await nupkgStream.CopyToAsync(stream, bufferSize: 8192, cancellationToken: CancellationToken.None);
//                }
//            }
//            catch (FatalProtocolException e) when (_ignoreFailedSources)
//            {
//                if (!_ignoreWarning)
//                {
//                    _logger.LogWarning(e.Message);
//                }
//            }
//            finally
//            {
//                Throttle?.Release();
//            }
//        }

//        private IEnumerable<LibraryDependency> GetDependencies(FindPackageByIdDependencyInfo packageInfo, NuGetFramework targetFramework)
//        {
//            if (packageInfo == null)
//            {
//                return new List<LibraryDependency>();
//            }
//            PackageDependencyGroup dependencies = NuGetFrameworkUtility.GetNearest(packageInfo.DependencyGroups,
//                targetFramework,
//                item => item.TargetFramework);

//            return GetDependencies(targetFramework, dependencies);
//        }

//        private static IList<LibraryDependency> GetDependencies(NuGetFramework targetFramework,
//            PackageDependencyGroup dependencies)
//        {
//            List<LibraryDependency> libraryDependencies = new List<LibraryDependency>();

//            if (dependencies != null)
//            {
//                libraryDependencies.AddRange(
//                    dependencies.Packages.Select(PackagingUtility.GetLibraryDependencyFromNuspec));
//            }

//            return libraryDependencies;
//        }

//        private async Task EnsureResource()
//        {
//            if (_findPackagesByIdResource == null)
//            {
//                FindPackageByIdResource resource = await _sourceRepository.GetResourceAsync<FindPackageByIdResource>();
//                resource.Logger = _logger;
//                resource.CacheContext = _cacheContext;

//                lock (_lock)
//                {
//                    if (_findPackagesByIdResource == null)
//                    {
//                        _findPackagesByIdResource = resource;
//                    }
//                }
//            }
//        }
//    }
//}
