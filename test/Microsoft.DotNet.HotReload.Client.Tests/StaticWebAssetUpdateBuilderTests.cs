// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.DotNet.Watch.UnitTests;
using Microsoft.Extensions.Logging;

namespace Microsoft.DotNet.HotReload.UnitTests;

public class StaticWebAssetUpdateBuilderTests(ITestOutputHelper testOutput)
{
    /// <summary>
    /// In-memory model of a project graph used to drive <see cref="StaticWebAssetUpdateBuilder.AddAssets"/>.
    /// </summary>
    private sealed class TestUpdateBuilder(ILogger logger) : StaticWebAssetUpdateBuilder
    {
        private readonly Dictionary<string, ProjectInstanceId> _idsByName = [];
        private readonly Dictionary<string, List<ProjectInstanceInfo>> _instancesByPath = [];
        private readonly Dictionary<ProjectInstanceId, ProjectInstanceInfo> _infos = [];
        private readonly Dictionary<ProjectInstanceId, StaticWebAssetsManifest> _manifests = [];

        // child instance -> the instances that directly reference it (its "parents"/ancestors)
        private readonly Dictionary<ProjectInstanceId, HashSet<ProjectInstanceId>> _referencingProjects = [];

        // instances that have a corresponding running (launched) project
        private readonly HashSet<ProjectInstanceId> _running = [];

        public void AddProject(string name, bool running, bool hasScopedCssTargets, StaticWebAssetsManifest? manifest = null)
        {
            var id = new ProjectInstanceId(ProjPath(name), Tfm);
            var info = new ProjectInstanceInfo
            {
                Id = id,
                AssemblyName = name,
                HasScopedCssTargets = hasScopedCssTargets,
            };

            _idsByName.Add(name, id);
            _infos.Add(id, info);
            _instancesByPath.Add(id.ProjectPath, [info]);
            _referencingProjects.Add(id, []);

            if (running)
            {
                _running.Add(id);
            }

            if (manifest != null)
            {
                _manifests.Add(id, manifest);
            }
        }

        public void AddReference(string referencingProject, string referencedProject)
            => _referencingProjects[_idsByName[referencedProject]].Add(_idsByName[referencingProject]);

        protected override bool TryGetManifest(ProjectInstanceId id, [NotNullWhen(true)] out StaticWebAssetsManifest? manifest)
        {
            if (_manifests.TryGetValue(id, out var found))
            {
                manifest = found;
                return true;
            }

            manifest = null;
            return false;
        }

        protected override IEnumerable<ProjectInstanceInfo> GetProjectInstances(string projectPath)
            => _instancesByPath.TryGetValue(projectPath, out var list) ? list : [];

        protected override IEnumerable<(ProjectInstanceInfo info, ILogger logger)> GetApplicationProjectAncestors(ProjectInstanceId projectInstanceId)
        {
            foreach (var ancestor in GetAncestorsAndSelf(projectInstanceId))
            {
                if (_running.Contains(ancestor))
                {
                    yield return (_infos[ancestor], logger);
                }
            }

            IEnumerable<ProjectInstanceId> GetAncestorsAndSelf(ProjectInstanceId id)
            {
                var visited = new HashSet<ProjectInstanceId> { id };
                var queue = new Queue<ProjectInstanceId>();
                queue.Enqueue(id);

                while (queue.Count > 0)
                {
                    var current = queue.Dequeue();
                    yield return current;

                    foreach (var referrer in _referencingProjects[current])
                    {
                        if (visited.Add(referrer))
                        {
                            queue.Enqueue(referrer);
                        }
                    }
                }
            }
        }
    }

    private const string Tfm = "net10.0";

    private static readonly string s_root = Path.Combine(Path.GetTempPath(), "StaticWebAssetUpdateBuilderTests");

    private static string ProjDir(string name)
        => Path.Combine(s_root, name);

    private static string ProjPath(string name)
        => Path.Combine(ProjDir(name), name + ".csproj");

    private static string Asset(string projName, params string[] relativeSegments)
        => Path.Combine([ProjDir(projName), .. relativeSegments]);

    // Path of the scoped CSS bundle that a web application generates for the styles of a referenced project.
    private static string Bundle(string appName, string containingProjectName)
        => Path.Combine(ProjDir(appName), "obj", containingProjectName + ".bundle.scp.css");

    private static StaticWebAssetsManifest Manifest(params (string url, string path)[] entries)
        => new(entries.ToImmutableDictionary(e => e.url, e => e.path, StringComparer.Ordinal), discoveryPatterns: []);


    private static string Inspect(StaticWebAsset asset)
        => $"{asset.FilePath} | {asset.RelativeUrl} | {asset.AssemblyName} | app={asset.IsApplicationProject}";

    private static void VerifyAssets(StaticWebAssetUpdateBuilder builder, params (string appName, string[] assets)[] expected)
    {
        var actual = builder.Assets.ToDictionary(
            entry => Path.GetFileNameWithoutExtension(entry.Key.ProjectPath),
            entry => entry.Value.Values.Select(Inspect).OrderBy(x => x, StringComparer.Ordinal).ToArray());

        AssertEx.SequenceEqual(
            expected.Select(e => e.appName).OrderBy(x => x, StringComparer.Ordinal),
            actual.Keys.OrderBy(x => x, StringComparer.Ordinal));

        foreach (var (appName, assets) in expected)
        {
            AssertEx.SequenceEqual(
                assets.OrderBy(x => x, StringComparer.Ordinal),
                actual[appName],
                message: appName);
        }
    }

    private static void AssertRegenerate(StaticWebAssetUpdateBuilder builder, params string[] expectedProjectNames)
        => AssertEx.SequenceEqual(
            expectedProjectNames.OrderBy(x => x, StringComparer.Ordinal),
            builder.ProjectInstancesToRegenerate.Select(id => Path.GetFileNameWithoutExtension(id.ProjectPath)).OrderBy(x => x, StringComparer.Ordinal));

    /// <summary>
    /// Host (running, non-web) references WebA (running), WebB (running), WebC (not running) and Console (running, non-web).
    /// Each of these projects references a single RCL that contains a static web asset and a scoped CSS file.
    /// Both RCL assets are updated.
    ///
    /// Only running web applications (WebA, WebB) receive updates.
    /// WebC is excluded because it is not running; Host and Console are excluded because they are not web applications.
    /// </summary>
    [Fact]
    public void MultipleAppsReferencingSharedRcl()
    {
        var logger = new TestLogger(testOutput);
        var builder = new TestUpdateBuilder(logger);

        builder.AddProject("Host", running: true, hasScopedCssTargets: false);
        builder.AddProject("WebA", running: true, hasScopedCssTargets: true, manifest: Manifest(("Rcl.bundle.scp.css", Bundle("WebA", "Rcl"))));
        builder.AddProject("WebB", running: true, hasScopedCssTargets: true, manifest: Manifest(("Rcl.bundle.scp.css", Bundle("WebB", "Rcl"))));
        builder.AddProject("WebC", running: false, hasScopedCssTargets: true, manifest: Manifest(("Rcl.bundle.scp.css", Bundle("WebC", "Rcl"))));
        builder.AddProject("Console", running: true, hasScopedCssTargets: false);
        builder.AddProject("Rcl", running: false, hasScopedCssTargets: true);

        builder.AddReference("Host", "WebA");
        builder.AddReference("Host", "WebB");
        builder.AddReference("Host", "WebC");
        builder.AddReference("Host", "Console");
        builder.AddReference("WebA", "Rcl");
        builder.AddReference("WebB", "Rcl");
        builder.AddReference("WebC", "Rcl");
        builder.AddReference("Console", "Rcl");

        // Regular static web asset contained in the RCL:
        builder.AddAssets(Asset("Rcl", "wwwroot", "background.png"), [ProjPath("Rcl")], "background.png");

        // Scoped CSS file contained in the RCL:
        builder.AddAssets(Asset("Rcl", "Components", "Component.razor.css"), [ProjPath("Rcl")], staticWebAssetRelativeUrl: null);

        VerifyAssets(builder,
            ("WebA",
            [
                $"{Asset("Rcl", "wwwroot", "background.png")} | wwwroot/background.png | Rcl | app=False",
                $"{Bundle("WebA", "Rcl")} | wwwroot/Rcl.bundle.scp.css | Rcl | app=False",
            ]),
            ("WebB",
            [
                $"{Asset("Rcl", "wwwroot", "background.png")} | wwwroot/background.png | Rcl | app=False",
                $"{Bundle("WebB", "Rcl")} | wwwroot/Rcl.bundle.scp.css | Rcl | app=False",
            ]));

        AssertRegenerate(builder, "Rcl", "WebA", "WebB");

        Assert.False(logger.HasWarning);
        Assert.False(logger.HasError);
    }

    /// <summary>
    /// Two running web applications (WebX, WebY). WebX references RclA and WebY references RclB.
    /// A single static web asset file is linked into both RCL projects and is updated once.
    ///
    /// Each application receives the update for the asset via the RCL it references.
    /// The reported assembly name reflects the containing RCL of each ancestor path.
    /// </summary>
    [Fact]
    public void SharedLinkedAssetAcrossSeparateRcls()
    {
        var logger = new TestLogger(testOutput);
        var builder = new TestUpdateBuilder(logger);

        builder.AddProject("WebX", running: true, hasScopedCssTargets: true, manifest: Manifest());
        builder.AddProject("WebY", running: true, hasScopedCssTargets: true, manifest: Manifest());
        builder.AddProject("RclA", running: false, hasScopedCssTargets: true);
        builder.AddProject("RclB", running: false, hasScopedCssTargets: true);

        builder.AddReference("WebX", "RclA");
        builder.AddReference("WebY", "RclB");

        // A single file linked into both RCLs (its containing projects are RclA and RclB):
        var linkedAsset = Path.Combine(s_root, "Shared", "wwwroot", "shared.js");
        builder.AddAssets(linkedAsset, [ProjPath("RclA"), ProjPath("RclB")], "shared.js");

        VerifyAssets(builder,
            ("WebX",
            [
                $"{linkedAsset} | wwwroot/shared.js | RclA | app=False",
            ]),
            ("WebY",
            [
                $"{linkedAsset} | wwwroot/shared.js | RclB | app=False",
            ]));

        // No scoped CSS involved:
        AssertRegenerate(builder);

        Assert.False(logger.HasWarning);
        Assert.False(logger.HasError);
    }

    /// <summary>
    /// A running web application references a non-RCL library (MidLib), which in turn references an RCL.
    /// The RCL contains an updated static web asset and an updated scoped CSS file.
    ///
    /// The update reaches the web application transitively through the intermediate library.
    /// </summary>
    [Fact]
    public void TransitiveReferenceThroughNonRclLibrary()
    {
        var logger = new TestLogger(testOutput);
        var builder = new TestUpdateBuilder(logger);

        builder.AddProject("Web", running: true, hasScopedCssTargets: true, manifest: Manifest(("Rcl.bundle.scp.css", Bundle("Web", "Rcl"))));
        builder.AddProject("MidLib", running: false, hasScopedCssTargets: false);
        builder.AddProject("Rcl", running: false, hasScopedCssTargets: true);

        builder.AddReference("Web", "MidLib");
        builder.AddReference("MidLib", "Rcl");

        builder.AddAssets(Asset("Rcl", "wwwroot", "logo.svg"), [ProjPath("Rcl")], "logo.svg");
        builder.AddAssets(Asset("Rcl", "Pages", "Counter.razor.css"), [ProjPath("Rcl")], staticWebAssetRelativeUrl: null);

        VerifyAssets(builder,
            ("Web",
            [
                $"{Asset("Rcl", "wwwroot", "logo.svg")} | wwwroot/logo.svg | Rcl | app=False",
                $"{Bundle("Web", "Rcl")} | wwwroot/Rcl.bundle.scp.css | Rcl | app=False",
            ]));

        AssertRegenerate(builder, "Rcl", "Web");

        Assert.False(logger.HasWarning);
        Assert.False(logger.HasError);
    }

    /// <summary>
    /// A single web application references two RCL projects.
    /// Each RCL contains a static web asset and a scoped CSS file. All four assets are updated.
    /// </summary>
    [Fact]
    public void SingleAppWithTwoRcls()
    {
        var logger = new TestLogger(testOutput);
        var builder = new TestUpdateBuilder(logger);

        builder.AddProject("Web", running: true, hasScopedCssTargets: true, manifest: Manifest(
            ("Rcl1.bundle.scp.css", Bundle("Web", "Rcl1")),
            ("Rcl2.bundle.scp.css", Bundle("Web", "Rcl2"))));
        builder.AddProject("Rcl1", running: false, hasScopedCssTargets: true);
        builder.AddProject("Rcl2", running: false, hasScopedCssTargets: true);

        builder.AddReference("Web", "Rcl1");
        builder.AddReference("Web", "Rcl2");

        builder.AddAssets(Asset("Rcl1", "wwwroot", "a.png"), [ProjPath("Rcl1")], "a.png");
        builder.AddAssets(Asset("Rcl2", "wwwroot", "b.png"), [ProjPath("Rcl2")], "b.png");
        builder.AddAssets(Asset("Rcl1", "Component1.razor.css"), [ProjPath("Rcl1")], staticWebAssetRelativeUrl: null);
        builder.AddAssets(Asset("Rcl2", "Component2.razor.css"), [ProjPath("Rcl2")], staticWebAssetRelativeUrl: null);

        VerifyAssets(builder,
            ("Web",
            [
                $"{Asset("Rcl1", "wwwroot", "a.png")} | wwwroot/a.png | Rcl1 | app=False",
                $"{Asset("Rcl2", "wwwroot", "b.png")} | wwwroot/b.png | Rcl2 | app=False",
                $"{Bundle("Web", "Rcl1")} | wwwroot/Rcl1.bundle.scp.css | Rcl1 | app=False",
                $"{Bundle("Web", "Rcl2")} | wwwroot/Rcl2.bundle.scp.css | Rcl2 | app=False",
            ]));

        AssertRegenerate(builder, "Rcl1", "Rcl2", "Web");

        Assert.False(logger.HasWarning);
        Assert.False(logger.HasError);
    }
}
