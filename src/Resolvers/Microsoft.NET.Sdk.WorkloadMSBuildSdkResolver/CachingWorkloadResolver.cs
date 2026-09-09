// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using Microsoft.Build.Framework;
using Microsoft.DotNet.Cli.Utils;
using Microsoft.NET.Sdk.WorkloadManifestReader;
using System.Collections.Immutable;
using System.Security.Cryptography;

namespace Microsoft.NET.Sdk.WorkloadMSBuildSdkResolver
{

    //  This class contains workload SDK resolution logic which will be used by both .NET SDK MSBuild and Full Framework / Visual Studio MSBuild.
    //
    //  Keeping this performant in Visual Studio is tricky, as VS performs a lot of evaluations, but they are not linked by an MSBuild "Submission ID",
    //  so the state caching support provided by MSBuild for SDK Resolvers doesn't really help.  Additionally, multiple instances of the SDK resolver
    //  may be created, and the same instance may be called on multiple threads.  So state needs to be cached staticly and be thread-safe.
    //
    //  To keep the state static, the MSBuildSdkResolver keeps a static reference to the CachingWorkloadResolver that is used if the build is inside
    //  Visual Studio.  To keep it thread-safe, the body of the Resolve method is all protected by a lock statement.  This avoids having to make
    //  the classes consumed by the CachingWorkloadResolver (the manifest provider and workload resolver) thread-safe.
    //
    //  A resolver should not over-cache and return out-of-date results.  For workloads, the resolution could change due to:
    //  - Installation, update, or uninstallation of a workload
    //  - Resolved SDK changes (either due to an SDK installation or uninstallation, or a global.json change)
    //  For SDK or workload installation actions, we expect to be running under a new process since Visual Studio will have been restarted.
    //  For global.json changes, the Resolve method takes parameters for the dotnet root and the SDK version.  If those values have changed
    //  from the previous call, the cached state will be thrown out and recreated.
    //  A caller that keeps an instance beyond a single build is responsible for the rest: see
    //  WorkloadSdkResolver, which replaces its instance when the opt-out, the user profile
    //  directory, or the contents of global.json change.
    class CachingWorkloadResolver
    {
        private sealed record CachedState
        {
            public string? DotnetRootPath { get; init; }
            public string? SdkVersion { get; init; }
            public string? GlobalJsonPath { get; init; }
            public IWorkloadManifestProvider? ManifestProvider { get; init; }
            public IWorkloadResolver? WorkloadResolver { get; init; }
            public ImmutableDictionary<string, ResolutionResult> CachedResults { get; init; }

            public CachedState()
            {
                CachedResults = ImmutableDictionary.Create<string, ResolutionResult>(StringComparer.OrdinalIgnoreCase);
            }
        }

        public object _lockObject { get; } = new object();
        private CachedState? _cachedState;
        private readonly bool _enabled;


        public CachingWorkloadResolver()
            : this(IsEnabled())
        {
        }

        public CachingWorkloadResolver(bool enabled)
        {
            _enabled = enabled;
        }

        //  A caller that caches a resolver must key on this, so sample it once and build the
        //  resolver from the sampled value rather than reading it again.
        public static bool IsEnabled()
        {
            // Support opt-out for workload resolution
            var envVar = Environment.GetEnvironmentVariable("MSBuildEnableWorkloadResolver");
            if (envVar != null && envVar.Equals("false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string sentinelPath = Path.Combine(SdkPaths.SdkDirectory, "DisableWorkloadResolver.sentinel");
            return !File.Exists(sentinelPath);
        }

        //  One resolver shared by every build in this process.
        //
        //  What a resolver reads is fixed for the life of the process: an SDK or a workload is not
        //  installed underneath a running build server. Three things are not fixed, and they are
        //  the key below:
        //    - the opt-out, which MSBuild re-applies from the client environment on every build;
        //    - the user profile directory, which selects user-local manifests and packs;
        //    - global.json, which selects a workload version and can be edited in place.
        //  Resolve already rebuilds the cached state when the dotnet root or the SDK version
        //  changes, so those are not repeated here.
        private static readonly object s_sharedLock = new();
        private static CachingWorkloadResolver? s_shared;
        private static (bool Enabled, string? UserProfileDir, string? GlobalJsonPath, string GlobalJson)? s_sharedKey;

        /// <summary>
        ///  The shared resolver for these settings, built anew when any of them has changed since
        ///  the last call. Callers should ask once per build rather than once per SDK reference,
        ///  and keep the returned instance for the whole build.
        /// </summary>
        public static CachingWorkloadResolver GetShared(bool enabled, string? userProfileDir, string? globalJsonPath)
        {
            //  Hashed outside the lock when enabled: it is file I/O and does not need to be serialized.
            var key = (enabled, userProfileDir, globalJsonPath, enabled ? DescribeGlobalJson(globalJsonPath) : "disabled");

            lock (s_sharedLock)
            {
                if (s_shared is null || !key.Equals(s_sharedKey))
                {
                    s_shared = new CachingWorkloadResolver(enabled);
                    s_sharedKey = key;
                }

                return s_shared;
            }
        }

        //  Contents, not a timestamp: editing global.json in place need not move its timestamp
        //  and need not change its length.
        private static string DescribeGlobalJson(string? globalJsonPath)
        {
            //  A stable value, so that the ordinary case of having no global.json still reuses.
            if (globalJsonPath is null || !File.Exists(globalJsonPath))
            {
                return "none";
            }

            try
            {
                using var contents = File.OpenRead(globalJsonPath);
                using var sha = SHA256.Create();
                return Convert.ToBase64String(sha.ComputeHash(contents));
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                //  Unreadable: build a new resolver rather than trust a file we could not check.
                return Guid.NewGuid().ToString();
            }
        }

        public record ResolutionResult()
        {
            public SdkResult? ToSdkResult(SdkReference sdkReference, SdkResultFactory factory)
            {
                switch (this)
                {
                    case SinglePathResolutionResult r:
                        return factory.IndicateSuccess(r.Path, sdkReference.Version);
                    case MultiplePathResolutionResult r:
                        return factory.IndicateSuccess(r.Paths, sdkReference.Version);
                    case EmptyResolutionResult r:
                        return factory.IndicateSuccess(Enumerable.Empty<string>(), sdkReference.Version, r.propertiesToAdd, r.itemsToAdd);
                    case NullResolutionResult:
                        return null;
                }

                throw new InvalidOperationException("Unknown resolutionResult type: " + GetType());
            }
        }

        public sealed record SinglePathResolutionResult(
            string Path
        ) : ResolutionResult;

        public sealed record MultiplePathResolutionResult(
            IEnumerable<string> Paths
        ) : ResolutionResult;

        public sealed record EmptyResolutionResult(
            IDictionary<string, string> propertiesToAdd,
            IDictionary<string, SdkResultItem> itemsToAdd
        ) : ResolutionResult;

        public sealed record NullResolutionResult() : ResolutionResult;

        private static ResolutionResult Resolve(string sdkReferenceName, IWorkloadManifestProvider? manifestProvider, IWorkloadResolver? workloadResolver)
        {
            if (sdkReferenceName.Equals("Microsoft.NET.SDK.WorkloadAutoImportPropsLocator", StringComparison.OrdinalIgnoreCase))
            {
                List<string> autoImportSdkPaths = new();
                if (workloadResolver != null)
                {
                    foreach (var sdkPackInfo in workloadResolver.GetInstalledWorkloadPacksOfKind(WorkloadPackKind.Sdk))
                    {
                        string sdkPackSdkFolder = Path.Combine(sdkPackInfo.Path, "Sdk");
                        string autoImportPath = Path.Combine(sdkPackSdkFolder, "AutoImport.props");
                        if (File.Exists(autoImportPath))
                        {
                            autoImportSdkPaths.Add(sdkPackSdkFolder);
                        }
                    }
                } 
                //  Call Distinct() here because with aliased packs, there may be duplicates of the same path
                return new MultiplePathResolutionResult(autoImportSdkPaths.Distinct());
            }
            else if (sdkReferenceName.Equals("Microsoft.NET.SDK.WorkloadManifestTargetsLocator", StringComparison.OrdinalIgnoreCase))
            {
                List<string> workloadManifestPaths = new();
                if (manifestProvider != null)
                {
                    foreach (var manifestDirectory in manifestProvider.GetManifests().Select(m => m.ManifestDirectory))
                    {
                        var workloadManifestTargetPath = Path.Combine(manifestDirectory, "WorkloadManifest.targets");
                        if (File.Exists(workloadManifestTargetPath))
                        {
                            workloadManifestPaths.Add(manifestDirectory);
                        }
                    }
                }
                return new MultiplePathResolutionResult(workloadManifestPaths);
            }
            else
            {
                var packInfo = workloadResolver?.TryGetPackInfo(new WorkloadPackId(sdkReferenceName));
                if (packInfo != null)
                {
                    if (Directory.Exists(packInfo.Path))
                    {
                        return new SinglePathResolutionResult(Path.Combine(packInfo.Path, "Sdk"));
                    }
                    else
                    {
                        var itemsToAdd = new Dictionary<string, SdkResultItem>();
                        itemsToAdd.Add("MissingWorkloadPack",
                            new SdkResultItem(sdkReferenceName,
                                metadata: new Dictionary<string, string>()
                                {
                                    { "Version", packInfo.Version }
                                }));

                        Dictionary<string, string> propertiesToAdd = new();
                        return new EmptyResolutionResult(propertiesToAdd, itemsToAdd);
                    }
                }
            }
            return new NullResolutionResult();
        }

        public ResolutionResult Resolve(string sdkReferenceName, string dotnetRootPath, string sdkVersion, string? userProfileDir, string? globalJsonPath)
        {
            if (!_enabled)
            {
                return new NullResolutionResult();
            }

            ResolutionResult? resolutionResult;

            lock (_lockObject)
            {
                if (_cachedState == null ||
                    _cachedState.DotnetRootPath != dotnetRootPath ||
                    _cachedState.SdkVersion != sdkVersion ||
                    _cachedState.GlobalJsonPath != globalJsonPath)
                {
                    var workloadManifestProvider = new SdkDirectoryWorkloadManifestProvider(dotnetRootPath, sdkVersion, userProfileDir, globalJsonPath);
                    var workloadResolver = WorkloadResolver.Create(workloadManifestProvider, dotnetRootPath, sdkVersion, userProfileDir);

                    _cachedState = new CachedState()
                    {
                        DotnetRootPath = dotnetRootPath,
                        SdkVersion = sdkVersion,
                        GlobalJsonPath = globalJsonPath,
                        ManifestProvider = workloadManifestProvider,
                        WorkloadResolver = workloadResolver
                    };
                }

                if (!_cachedState.CachedResults.TryGetValue(sdkReferenceName, out resolutionResult))
                {
                    resolutionResult = Resolve(sdkReferenceName, _cachedState.ManifestProvider, _cachedState.WorkloadResolver);

                    _cachedState = _cachedState with
                    {
                        CachedResults = _cachedState.CachedResults.Add(sdkReferenceName, resolutionResult)
                    };
                }
            }

            return resolutionResult;
        }
    }
}
