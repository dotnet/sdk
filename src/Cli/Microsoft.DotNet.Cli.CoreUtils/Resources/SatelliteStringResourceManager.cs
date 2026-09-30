// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Reflection;
using System.Resources;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Microsoft.DotNet.Cli.Resources;

/// <summary>
///  Resolves localized strings from one configured culture source and falls back to a neutral
///  <see cref="StringResourceManager"/>.
/// </summary>
/// <remarks>
///  <para>
///   Create an instance with
///   <see cref="FromRuntimeSatellites(string, Assembly, StringResourceManagerOptions)"/> for normal
///   runtime satellite binding,
///   <see cref="FromResourcesDirectory(string, string, Assembly, StringResourceManagerOptions)"/> for loose
///   per-culture <c>.resources</c> files, or
///   <see cref="FromSatelliteDirectory(string, string, Assembly, StringResourceManagerOptions)"/> to parse deployed satellite
///  assemblies as data. Use
///  <see cref="FromAssemblyFiles(string, string, string, StringResourceManagerOptions)"/> when both
///  the neutral owner and its satellites must be parsed as data. A manager never mixes these
///  localized source modes.
///  </para>
///  <para>
///   For a requested culture the manager walks from the most specific culture up to (but not
///   including) the invariant culture or an assembly-declared neutral culture. More-specific tables
///   take precedence. When no localized table supplies the key, lookup defers to the neutral manager.
///  </para>
///  <para>
///   Loaded source tables and culture chains are cached until <see cref="ReleaseAllResources()"/> is
///   called. Each localized file candidate is opened at most once per cache generation, including
///   during concurrent first lookup. Caller-supplied neutral managers remain caller-owned and are not
///   released by this manager.
///  </para>
///  <para>
///   Runtime assembly metadata and successful or missing satellite bind results are cached per
///   resource assembly for the process lifetime. <see cref="ReleaseAllResources()"/> reloads resource
///   tables but does not repeat runtime assembly binding.
///  </para>
///  <para>
///   Each distinct requested culture and each missing source candidate remain cached for the current
///   generation. Callers must not supply an unbounded sequence of attacker-controlled cultures.
///  </para>
///  <para>
///   Directory factories capture a fully qualified root during construction. The resource base name,
///   culture names, and satellite assembly name must each be a single path segment so probing remains
///   deterministic and independent of later current-directory changes.
///  </para>
///  <para>
///   Resource data is expected to be a trusted output of the application's build and deployment
///   pipeline. Strict probing continues parent or neutral fallback only for an absent localized
///   source. <see cref="SatelliteStringResourceProbeMode.FallbackOnFailure"/> also treats unreadable,
///   malformed, unsupported, incorrectly bundled, and identity-mismatched localized satellite
///   candidates as missing. Neutral resource failures always throw.
///  </para>
///  <para>
///   Direct satellite modes validate the satellite simple name, culture, version, and public key
///   against the resource-owning assembly before reading its manifest resource.
///  </para>
///  <para>
///   Null resources always reject a table. By default any non-string resource rejects the table;
///   <see cref="StringResourceManagerOptions.IgnoreNonStringResources"/> skips non-string names and
///   values, so those names behave as missing during culture fallback.
///  </para>
///  <para>
///   Resource names are matched ordinally and case-sensitively. Case-insensitive lookup is not
///   supported.
///  </para>
/// </remarks>
public sealed partial class SatelliteStringResourceManager : StringResourceManager
{
    private readonly string _resourceName;
    private readonly SatelliteStringResourceSourceKind _sourceKind;
    private readonly string? _localizedRoot;
    private readonly Assembly? _resourceAssembly;
    private readonly ManagedAssemblyStringResourceSource? _resourceAssemblySource;
    private readonly string? _resourceAssemblySimpleNameAlias;
    private readonly StringResourceManager _neutralResources;
    private readonly Func<string, MappedMemoryManager> _openFile;
    private readonly SatelliteStringResourceProbeMode _probeMode;
    private volatile SatelliteStringResourceSourceMetadata? _neutralSourceMetadata;
    private object? _localizedLoadGate;
    private int _localizedGeneration;
    private Dictionary<string, CultureCache>? _cultureCaches;
    private Dictionary<string, LocalizedStringResourceTableCache>? _sourceTables;

    private volatile CultureCache? _lastCultureCache;

    private static readonly Func<string, MappedMemoryManager> s_openFile = MappedMemoryManager.CreateFromFile;

#pragma warning disable IDE0028 // ConditionalWeakTable cannot be constructed with a collection expression on net472.
    private static readonly ConditionalWeakTable<Assembly, SatelliteAssemblyCache> s_satelliteAssemblies = new();
#pragma warning restore IDE0028

    /// <summary>
    ///  Initializes a manager that reads loose per-culture resource files from the
    ///  <c>resources</c> directory under <see cref="AppContext.BaseDirectory"/>.
    /// </summary>
    /// <param name="baseName">The root name of the resource table.</param>
    /// <param name="assembly">The assembly that contains the neutral resource.</param>
    public SatelliteStringResourceManager(string baseName, Assembly assembly)
        : this(baseName, assembly, Path.Join(AppContext.BaseDirectory, "resources"))
    {
    }

    /// <summary>
    ///  Initializes a manager that reads loose per-culture resource files.
    /// </summary>
    /// <param name="baseName">The root name of the resource table.</param>
    /// <param name="assembly">The assembly that contains the neutral resource.</param>
    /// <param name="probeRoot">The directory containing culture subdirectories.</param>
    public SatelliteStringResourceManager(string baseName, Assembly assembly, string probeRoot)
        : this(
            baseName,
            new StringResourceManager(baseName, assembly),
            ownsNeutralResources: true,
            SatelliteStringResourceSourceKind.ResourcesDirectory,
            probeRoot,
            assembly,
            resourceAssemblySource: null,
            s_openFile,
            StringResourceManagerOptions.None)
    {
    }

    /// <inheritdoc cref="FromRuntimeSatellites(string, Assembly, StringResourceManagerOptions)"/>
    public static SatelliteStringResourceManager FromRuntimeSatellites(
        string baseName,
        Assembly resourceAssembly)
    {
        return FromRuntimeSatellites(
            baseName,
            resourceAssembly,
            StringResourceManagerOptions.None);
    }

    /// <summary>
    ///  Creates a manager that binds localized satellite assemblies through the runtime.
    /// </summary>
    /// <param name="baseName">The root name of the resource table.</param>
    /// <param name="resourceAssembly">The assembly that owns the neutral and satellite resources.</param>
    /// <param name="options">The resource loading options.</param>
    /// <returns>A runtime-satellite manager.</returns>
    public static SatelliteStringResourceManager FromRuntimeSatellites(
        string baseName,
        Assembly resourceAssembly,
        StringResourceManagerOptions options)
    {
        ArgumentNullException.ThrowIfNull(baseName);
        ArgumentNullException.ThrowIfNull(resourceAssembly);

        return new(
            baseName,
            new StringResourceManager(baseName, resourceAssembly, options),
            ownsNeutralResources: true,
            SatelliteStringResourceSourceKind.RuntimeSatellites,
            localizedRoot: null,
            resourceAssembly,
            resourceAssemblySource: null,
            s_openFile,
            options);
    }

    /// <inheritdoc cref="FromRuntimeSatellites(string, Assembly, StringResourceManager, StringResourceManagerOptions)"/>
    public static SatelliteStringResourceManager FromRuntimeSatellites(
        string baseName,
        Assembly resourceAssembly,
        StringResourceManager neutralResources)
    {
        return FromRuntimeSatellites(
            baseName,
            resourceAssembly,
            neutralResources,
            StringResourceManagerOptions.None);
    }

    /// <summary>
    ///  Creates a manager that binds localized satellite assemblies through the runtime and delegates
    ///  neutral fallback to <paramref name="neutralResources"/>.
    /// </summary>
    /// <param name="baseName">The root name of the resource table.</param>
    /// <param name="resourceAssembly">The assembly that owns the localized satellites.</param>
    /// <param name="neutralResources">The caller-owned manager that supplies neutral strings.</param>
    /// <param name="options">The localized resource loading options.</param>
    /// <returns>A runtime-satellite manager.</returns>
    public static SatelliteStringResourceManager FromRuntimeSatellites(
        string baseName,
        Assembly resourceAssembly,
        StringResourceManager neutralResources,
        StringResourceManagerOptions options)
    {
        ArgumentNullException.ThrowIfNull(baseName);
        ArgumentNullException.ThrowIfNull(resourceAssembly);
        ArgumentNullException.ThrowIfNull(neutralResources);

        return new(
            baseName,
            neutralResources,
            ownsNeutralResources: false,
            SatelliteStringResourceSourceKind.RuntimeSatellites,
            localizedRoot: null,
            resourceAssembly,
            resourceAssemblySource: null,
            s_openFile,
            options);
    }

    /// <inheritdoc cref="FromResourcesDirectory(string, string, Assembly, StringResourceManagerOptions)"/>
    public static SatelliteStringResourceManager FromResourcesDirectory(
        string baseName,
        string resourcesDirectory,
        Assembly neutralAssembly)
    {
        return FromResourcesDirectory(
            baseName,
            resourcesDirectory,
            neutralAssembly,
            StringResourceManagerOptions.None);
    }

    /// <summary>
    ///  Creates a manager that reads loose per-culture binary resource files.
    /// </summary>
    /// <param name="baseName">The root name of the resource table.</param>
    /// <param name="resourcesDirectory">The directory containing culture subdirectories.</param>
    /// <param name="neutralAssembly">The assembly that contains the neutral resource.</param>
    /// <param name="options">The resource loading options.</param>
    /// <returns>A loose-resource manager.</returns>
    public static SatelliteStringResourceManager FromResourcesDirectory(
        string baseName,
        string resourcesDirectory,
        Assembly neutralAssembly,
        StringResourceManagerOptions options)
    {
        ArgumentNullException.ThrowIfNull(baseName);
        ArgumentNullException.ThrowIfNull(resourcesDirectory);
        ArgumentNullException.ThrowIfNull(neutralAssembly);

        return new(
            baseName,
            new StringResourceManager(baseName, neutralAssembly, options),
            ownsNeutralResources: true,
            SatelliteStringResourceSourceKind.ResourcesDirectory,
            resourcesDirectory,
            neutralAssembly,
            resourceAssemblySource: null,
            s_openFile,
            options);
    }

    /// <inheritdoc cref="FromResourcesDirectory(string, string, StringResourceManager, StringResourceManagerOptions)"/>
    public static SatelliteStringResourceManager FromResourcesDirectory(
        string baseName,
        string resourcesDirectory,
        StringResourceManager neutralResources)
    {
        return FromResourcesDirectory(
            baseName,
            resourcesDirectory,
            neutralResources,
            StringResourceManagerOptions.None);
    }

    /// <summary>
    ///  Creates a loose-resource manager that opens files through <paramref name="openFile"/>.
    /// </summary>
    /// <param name="baseName">The root name of the resource table.</param>
    /// <param name="resourcesDirectory">The directory containing culture subdirectories.</param>
    /// <param name="neutralAssembly">The assembly that contains the neutral resource.</param>
    /// <param name="openFile">The function that opens a file as mapped memory.</param>
    /// <param name="options">The resource loading options.</param>
    /// <returns>A loose-resource manager.</returns>
    internal static SatelliteStringResourceManager FromResourcesDirectory(
        string baseName,
        string resourcesDirectory,
        Assembly neutralAssembly,
        Func<string, MappedMemoryManager> openFile,
        StringResourceManagerOptions options = StringResourceManagerOptions.None)
    {
        return new(
            baseName,
            new StringResourceManager(baseName, neutralAssembly, options),
            ownsNeutralResources: true,
            SatelliteStringResourceSourceKind.ResourcesDirectory,
            resourcesDirectory,
            neutralAssembly,
            resourceAssemblySource: null,
            openFile,
            options);
    }

    /// <summary>
    ///  Creates a manager that reads loose per-culture binary resource files and delegates neutral
    ///  fallback to <paramref name="neutralResources"/>.
    /// </summary>
    /// <param name="baseName">The root name of the resource table.</param>
    /// <param name="resourcesDirectory">The directory containing culture subdirectories.</param>
    /// <param name="neutralResources">The caller-owned manager that supplies neutral strings.</param>
    /// <param name="options">The localized resource loading options.</param>
    /// <returns>A loose-resource manager.</returns>
    public static SatelliteStringResourceManager FromResourcesDirectory(
        string baseName,
        string resourcesDirectory,
        StringResourceManager neutralResources,
        StringResourceManagerOptions options)
    {
        ArgumentNullException.ThrowIfNull(baseName);
        ArgumentNullException.ThrowIfNull(resourcesDirectory);
        ArgumentNullException.ThrowIfNull(neutralResources);

        return new(
            baseName,
            neutralResources,
            ownsNeutralResources: false,
            SatelliteStringResourceSourceKind.ResourcesDirectory,
            resourcesDirectory,
            neutralResources.SourceAssembly,
            resourceAssemblySource: null,
            s_openFile,
            options);
    }

    /// <inheritdoc cref="FromSatelliteDirectory(string, string, Assembly, StringResourceManagerOptions)"/>
    public static SatelliteStringResourceManager FromSatelliteDirectory(
        string baseName,
        string satelliteDirectory,
        Assembly resourceAssembly)
    {
        return FromSatelliteDirectory(
            baseName,
            satelliteDirectory,
            resourceAssembly,
            StringResourceManagerOptions.None);
    }

    /// <inheritdoc cref="FromSatelliteDirectory(string, string, Assembly, StringResourceManagerOptions, SatelliteStringResourceProbeMode)"/>
    public static SatelliteStringResourceManager FromSatelliteDirectory(
        string baseName,
        string satelliteDirectory,
        Assembly resourceAssembly,
        SatelliteStringResourceProbeMode probeMode) => FromSatelliteDirectory(
            baseName,
            satelliteDirectory,
            resourceAssembly,
            StringResourceManagerOptions.None,
            probeMode);

    /// <summary>
    ///  Creates a direct-satellite manager that maps a generated owner assembly to an external
    ///  assembly family name.
    /// </summary>
    /// <param name="baseName">The root name of the resource table.</param>
    /// <param name="satelliteDirectory">The directory containing culture subdirectories.</param>
    /// <param name="generatedOwnerAssembly">The generated accessor's owner assembly.</param>
    /// <param name="externalOwnerSimpleName">The external managed owner assembly simple name.</param>
    /// <param name="probeMode">How localized satellite candidate failures affect fallback.</param>
    /// <returns>A direct-satellite manager.</returns>
    public static SatelliteStringResourceManager FromSatelliteDirectory(
        string baseName,
        string satelliteDirectory,
        Assembly generatedOwnerAssembly,
        string externalOwnerSimpleName,
        SatelliteStringResourceProbeMode probeMode)
    {
        ArgumentNullException.ThrowIfNull(baseName);
        ArgumentNullException.ThrowIfNull(satelliteDirectory);
        ArgumentNullException.ThrowIfNull(generatedOwnerAssembly);
        ArgumentNullException.ThrowIfNull(externalOwnerSimpleName);
        ValidatePathSegment(externalOwnerSimpleName, nameof(externalOwnerSimpleName));

        return new(
            baseName,
            new StringResourceManager(baseName, generatedOwnerAssembly),
            ownsNeutralResources: true,
            SatelliteStringResourceSourceKind.SatelliteDirectory,
            satelliteDirectory,
            generatedOwnerAssembly,
            resourceAssemblySource: null,
            s_openFile,
            StringResourceManagerOptions.None,
            probeMode,
            externalOwnerSimpleName);
    }

    /// <summary>
    ///  Creates a manager that parses satellite assemblies directly from a directory.
    /// </summary>
    /// <param name="baseName">The root name of the resource table.</param>
    /// <param name="satelliteDirectory">The directory containing culture subdirectories.</param>
    /// <param name="resourceAssembly">The assembly that owns the neutral and satellite resources.</param>
    /// <param name="options">The resource loading options.</param>
    /// <returns>A direct-satellite manager.</returns>
    public static SatelliteStringResourceManager FromSatelliteDirectory(
        string baseName,
        string satelliteDirectory,
        Assembly resourceAssembly,
        StringResourceManagerOptions options) => FromSatelliteDirectory(
            baseName,
            satelliteDirectory,
            resourceAssembly,
            options,
            SatelliteStringResourceProbeMode.Strict);

    /// <summary>
    ///  Creates a manager that parses satellite assemblies directly from a directory.
    /// </summary>
    /// <param name="baseName">The root name of the resource table.</param>
    /// <param name="satelliteDirectory">The directory containing culture subdirectories.</param>
    /// <param name="resourceAssembly">The assembly that owns the neutral and satellite resources.</param>
    /// <param name="options">The resource loading options.</param>
    /// <param name="probeMode">How localized satellite candidate failures affect fallback.</param>
    /// <returns>A direct-satellite manager.</returns>
    public static SatelliteStringResourceManager FromSatelliteDirectory(
        string baseName,
        string satelliteDirectory,
        Assembly resourceAssembly,
        StringResourceManagerOptions options,
        SatelliteStringResourceProbeMode probeMode)
    {
        ArgumentNullException.ThrowIfNull(baseName);
        ArgumentNullException.ThrowIfNull(satelliteDirectory);
        ArgumentNullException.ThrowIfNull(resourceAssembly);

        return new(
            baseName,
            new StringResourceManager(baseName, resourceAssembly, options),
            ownsNeutralResources: true,
            SatelliteStringResourceSourceKind.SatelliteDirectory,
            satelliteDirectory,
            resourceAssembly,
            resourceAssemblySource: null,
            s_openFile,
            options,
            probeMode);
    }

    /// <inheritdoc cref="FromSatelliteDirectory(string, string, Assembly, StringResourceManager, StringResourceManagerOptions)"/>
    public static SatelliteStringResourceManager FromSatelliteDirectory(
        string baseName,
        string satelliteDirectory,
        Assembly resourceAssembly,
        StringResourceManager neutralResources)
    {
        return FromSatelliteDirectory(
            baseName,
            satelliteDirectory,
            resourceAssembly,
            neutralResources,
            StringResourceManagerOptions.None);
    }

    /// <summary>
    ///  Creates a direct-satellite manager that opens files through <paramref name="openFile"/>.
    /// </summary>
    /// <param name="baseName">The root name of the resource table.</param>
    /// <param name="satelliteDirectory">The directory containing culture subdirectories.</param>
    /// <param name="resourceAssembly">The assembly that owns the resources.</param>
    /// <param name="openFile">The function that opens a file as mapped memory.</param>
    /// <param name="options">The resource loading options.</param>
    /// <param name="probeMode">How localized satellite candidate failures affect fallback.</param>
    /// <returns>A direct-satellite manager.</returns>
    internal static SatelliteStringResourceManager FromSatelliteDirectory(
        string baseName,
        string satelliteDirectory,
        Assembly resourceAssembly,
        Func<string, MappedMemoryManager> openFile,
        StringResourceManagerOptions options = StringResourceManagerOptions.None,
        SatelliteStringResourceProbeMode probeMode = SatelliteStringResourceProbeMode.Strict)
    {
        return new(
            baseName,
            new StringResourceManager(baseName, resourceAssembly, options),
            ownsNeutralResources: true,
            SatelliteStringResourceSourceKind.SatelliteDirectory,
            satelliteDirectory,
            resourceAssembly,
            resourceAssemblySource: null,
            openFile,
            options,
            probeMode);
    }

    /// <summary>
    ///  Creates a manager that parses satellite assemblies directly from a directory and delegates
    ///  neutral fallback to <paramref name="neutralResources"/>.
    /// </summary>
    /// <param name="baseName">The root name of the resource table.</param>
    /// <param name="satelliteDirectory">The directory containing culture subdirectories.</param>
    /// <param name="resourceAssembly">The assembly that owns the localized satellites.</param>
    /// <param name="neutralResources">The caller-owned manager that supplies neutral strings.</param>
    /// <param name="options">The localized resource loading options.</param>
    /// <returns>A direct-satellite manager.</returns>
    public static SatelliteStringResourceManager FromSatelliteDirectory(
        string baseName,
        string satelliteDirectory,
        Assembly resourceAssembly,
        StringResourceManager neutralResources,
        StringResourceManagerOptions options)
    {
        ArgumentNullException.ThrowIfNull(baseName);
        ArgumentNullException.ThrowIfNull(satelliteDirectory);
        ArgumentNullException.ThrowIfNull(resourceAssembly);
        ArgumentNullException.ThrowIfNull(neutralResources);

        return new(
            baseName,
            neutralResources,
            ownsNeutralResources: false,
            SatelliteStringResourceSourceKind.SatelliteDirectory,
            satelliteDirectory,
            resourceAssembly,
            resourceAssemblySource: null,
            s_openFile,
            options);
    }

    /// <inheritdoc cref="FromAssemblyFiles(string, string, string, StringResourceManagerOptions)"/>
    public static SatelliteStringResourceManager FromAssemblyFiles(
        string baseName,
        string resourceAssemblyFile,
        string satelliteDirectory) => FromAssemblyFiles(
            baseName,
            resourceAssemblyFile,
            satelliteDirectory,
            StringResourceManagerOptions.None);

    /// <inheritdoc cref="FromAssemblyFiles(string, string, string, StringResourceManagerOptions, SatelliteStringResourceProbeMode)"/>
    public static SatelliteStringResourceManager FromAssemblyFiles(
        string baseName,
        string resourceAssemblyFile,
        string satelliteDirectory,
        SatelliteStringResourceProbeMode probeMode) => FromAssemblyFiles(
            baseName,
            resourceAssemblyFile,
            satelliteDirectory,
            StringResourceManagerOptions.None,
            probeMode);

    /// <summary>
    ///  Strictly validates an aliased external owner assembly, then creates an external-all manager.
    /// </summary>
    /// <param name="baseName">The root name of the resource table.</param>
    /// <param name="resourceAssemblyFile">The external managed owner assembly file.</param>
    /// <param name="satelliteDirectory">The directory containing culture subdirectories.</param>
    /// <param name="generatedOwnerAssembly">The generated accessor's owner assembly.</param>
    /// <param name="externalOwnerSimpleName">The expected external owner simple name.</param>
    /// <param name="probeMode">How localized satellite candidate failures affect fallback.</param>
    /// <returns>An external-all string resource manager.</returns>
    public static SatelliteStringResourceManager FromAssemblyFiles(
        string baseName,
        string resourceAssemblyFile,
        string satelliteDirectory,
        Assembly generatedOwnerAssembly,
        string externalOwnerSimpleName,
        SatelliteStringResourceProbeMode probeMode)
    {
        StringResourceManager.ValidateAssemblyFile(
            baseName,
            resourceAssemblyFile,
            generatedOwnerAssembly,
            externalOwnerSimpleName);

        return FromAssemblyFiles(
            baseName,
            resourceAssemblyFile,
            satelliteDirectory,
            probeMode);
    }

    /// <summary>
    ///  Creates a manager that parses neutral resources from a managed owner assembly and localized
    ///  resources from its satellite assemblies without loading those assemblies.
    /// </summary>
    /// <param name="baseName">The root name of the resource table.</param>
    /// <param name="resourceAssemblyFile">The managed assembly containing the neutral resource.</param>
    /// <param name="satelliteDirectory">
    ///  The directory containing culture subdirectories with satellite resource assemblies.
    /// </param>
    /// <param name="options">The resource loading options.</param>
    /// <returns>An external-assembly string resource manager.</returns>
    /// <exception cref="ArgumentNullException">
    ///  <paramref name="baseName"/>, <paramref name="resourceAssemblyFile"/>, or
    ///  <paramref name="satelliteDirectory"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="ArgumentException"><paramref name="baseName"/> is not a single path segment.</exception>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="options"/> contains an unknown flag.</exception>
    public static SatelliteStringResourceManager FromAssemblyFiles(
        string baseName,
        string resourceAssemblyFile,
        string satelliteDirectory,
        StringResourceManagerOptions options) => FromAssemblyFiles(
            baseName,
            resourceAssemblyFile,
            satelliteDirectory,
            options,
            SatelliteStringResourceProbeMode.Strict);

    /// <summary>
    ///  Creates a manager that parses a neutral owner assembly and localized satellite assemblies
    ///  without loading them.
    /// </summary>
    /// <param name="baseName">The root name of the resource table.</param>
    /// <param name="resourceAssemblyFile">The managed assembly containing the neutral resource.</param>
    /// <param name="satelliteDirectory">The directory containing culture subdirectories.</param>
    /// <param name="options">The resource loading options.</param>
    /// <param name="probeMode">How localized satellite candidate failures affect fallback.</param>
    /// <returns>An external-assembly string resource manager.</returns>
    public static SatelliteStringResourceManager FromAssemblyFiles(
        string baseName,
        string resourceAssemblyFile,
        string satelliteDirectory,
        StringResourceManagerOptions options,
        SatelliteStringResourceProbeMode probeMode) => FromAssemblyFiles(
            baseName,
            resourceAssemblyFile,
            satelliteDirectory,
            s_openFile,
            options,
            probeMode);

    /// <summary>
    ///  Creates an external-assembly manager that opens files through <paramref name="openFile"/>.
    /// </summary>
    /// <param name="baseName">The root name of the resource table.</param>
    /// <param name="resourceAssemblyFile">The managed assembly containing the neutral resource.</param>
    /// <param name="satelliteDirectory">The directory containing culture subdirectories.</param>
    /// <param name="openFile">The function that opens a file as mapped memory.</param>
    /// <param name="options">The resource loading options.</param>
    /// <param name="probeMode">How localized satellite candidate failures affect fallback.</param>
    /// <returns>An external-assembly string resource manager.</returns>
    internal static SatelliteStringResourceManager FromAssemblyFiles(
        string baseName,
        string resourceAssemblyFile,
        string satelliteDirectory,
        Func<string, MappedMemoryManager> openFile,
        StringResourceManagerOptions options = StringResourceManagerOptions.None,
        SatelliteStringResourceProbeMode probeMode = SatelliteStringResourceProbeMode.Strict)
    {
        ArgumentNullException.ThrowIfNull(baseName);
        ArgumentNullException.ThrowIfNull(resourceAssemblyFile);
        ArgumentNullException.ThrowIfNull(satelliteDirectory);
        ArgumentNullException.ThrowIfNull(openFile);
        options.Validate();

        StringResourceManager neutralResources = StringResourceManager.FromAssemblyFile(
            baseName,
            resourceAssemblyFile,
            openFile,
            options);

        ManagedAssemblyStringResourceSource source = neutralResources.ManagedAssemblySource
            ?? throw new InvalidOperationException("The managed assembly source was not initialized.");

        return new(
            baseName,
            neutralResources,
            ownsNeutralResources: true,
            SatelliteStringResourceSourceKind.SatelliteDirectory,
            satelliteDirectory,
            resourceAssembly: null,
            source,
            openFile,
            options,
            probeMode);
    }

    private SatelliteStringResourceManager(
        string baseName,
        StringResourceManager neutralResources,
        bool ownsNeutralResources,
        SatelliteStringResourceSourceKind sourceKind,
        string? localizedRoot,
        Assembly? resourceAssembly,
        ManagedAssemblyStringResourceSource? resourceAssemblySource,
        Func<string, MappedMemoryManager> openFile,
        StringResourceManagerOptions options,
        SatelliteStringResourceProbeMode probeMode = SatelliteStringResourceProbeMode.Strict,
        string? resourceAssemblySimpleNameAlias = null)
        : base(
            baseName,
            neutralResources,
            ownsNeutralResources,
            options)
    {
        ArgumentNullException.ThrowIfNull(baseName);
        ArgumentNullException.ThrowIfNull(neutralResources);
        ArgumentNullException.ThrowIfNull(openFile);
        if (probeMode is not SatelliteStringResourceProbeMode.Strict
            and not SatelliteStringResourceProbeMode.FallbackOnFailure)
        {
            throw new ArgumentOutOfRangeException(nameof(probeMode));
        }

        if (sourceKind != SatelliteStringResourceSourceKind.RuntimeSatellites)
        {
            ArgumentNullException.ThrowIfNull(localizedRoot);
            ValidatePathSegment(baseName, nameof(baseName));
            localizedRoot = Path.GetFullPath(localizedRoot);
        }

        if (sourceKind == SatelliteStringResourceSourceKind.RuntimeSatellites)
        {
            ArgumentNullException.ThrowIfNull(resourceAssembly);
        }

        if (sourceKind == SatelliteStringResourceSourceKind.SatelliteDirectory
            && resourceAssembly is null
            && resourceAssemblySource is null)
        {
            throw new ArgumentException(
                "A resource assembly or managed assembly file source is required.",
                nameof(resourceAssembly));
        }

        _resourceName = $"{baseName}.resources";
        _sourceKind = sourceKind;
        _localizedRoot = localizedRoot;
        _resourceAssembly = resourceAssembly;
        _resourceAssemblySource = resourceAssemblySource;
        _resourceAssemblySimpleNameAlias = resourceAssemblySimpleNameAlias;
        _neutralResources = neutralResources;
        _openFile = openFile;
        _probeMode = probeMode;
    }

    /// <summary>
    ///  Gets the string with the given name for the requested culture, its parents, or the neutral
    ///  resource table.
    /// </summary>
    /// <param name="name">The resource name.</param>
    /// <param name="culture">
    ///  The requested culture, or <see langword="null"/> to use <see cref="CultureInfo.CurrentUICulture"/>.
    /// </param>
    /// <returns>The string value, or <see langword="null"/> when the resource does not exist.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="name"/> is <see langword="null"/>.</exception>
    /// <exception cref="NotSupportedException">
    ///  A table contains a non-string resource and <see cref="StringResourceManagerOptions.IgnoreNonStringResources"/>
    ///  is not set.
    /// </exception>
    /// <exception cref="ArgumentException">A configured source is not a valid resource file.</exception>
    /// <exception cref="BadImageFormatException">
    ///  A configured source contains malformed data or a null resource.
    /// </exception>
    /// <exception cref="IOException">A configured file or stream cannot be read.</exception>
    public override string? GetString(string name, CultureInfo? culture)
    {
        ArgumentNullException.ThrowIfNull(name);
        culture ??= CultureInfo.CurrentUICulture;

        while (true)
        {
            CultureCache cache = GetCultureCache(culture);
            bool stale = false;
            foreach (IndexedStringResourceTable table in cache.Tables)
            {
                StringResourceLookupKind result = table.Lookup(name, out string? value);
                if (result == StringResourceLookupKind.Found)
                {
                    return value;
                }

                if (result == StringResourceLookupKind.Stale)
                {
                    stale = true;
                    break;
                }
            }

            if (!stale)
            {
                return _neutralResources.GetString(name, culture);
            }
        }
    }

    /// <summary>
    ///  Releases localized state and any neutral manager created by this instance. A caller-supplied
    ///  neutral manager remains unchanged.
    /// </summary>
    public override void ReleaseAllResources() => ReleaseAllResourcesCore(waitingForLoad: null);

    /// <summary>
    ///  Releases resources and invokes <paramref name="waitingForLoad"/> after detecting a contended
    ///  localized load gate.
    /// </summary>
    /// <param name="waitingForLoad">The callback invoked before waiting for the localized load gate.</param>
    internal new void ReleaseAllResources(Action waitingForLoad)
    {
        ArgumentNullException.ThrowIfNull(waitingForLoad);
        ReleaseAllResourcesCore(waitingForLoad);
    }

    private void ReleaseAllResourcesCore(Action? waitingForLoad)
    {
        ExceptionDispatchInfo? releaseFailure = null;
        object loadGate = GetLocalizedLoadGate();
        bool lockTaken = Monitor.TryEnter(loadGate);
        if (!lockTaken)
        {
            waitingForLoad?.Invoke();
            Monitor.Enter(loadGate, ref lockTaken);
        }

        try
        {
            Dictionary<string, LocalizedStringResourceTableCache>? sourceTables = _sourceTables;
            _localizedGeneration = unchecked(_localizedGeneration + 1);
            _lastCultureCache = null;
            _cultureCaches = null;
            _sourceTables = null;

            if (sourceTables is not null)
            {
                foreach (LocalizedStringResourceTableCache table in sourceTables.Values)
                {
                    try
                    {
                        table.Release();
                    }
                    catch (Exception exception)
                    {
                        releaseFailure ??= ExceptionDispatchInfo.Capture(exception);
                    }
                }
            }
        }
        finally
        {
            if (lockTaken)
            {
                Monitor.Exit(loadGate);
            }
        }

        try
        {
            base.ReleaseAllResources();
        }
        catch (Exception exception)
        {
            releaseFailure ??= ExceptionDispatchInfo.Capture(exception);
        }

        releaseFailure?.Throw();
    }

    private CultureCache GetCultureCache(CultureInfo culture)
    {
        int generation = Volatile.Read(ref _localizedGeneration);
        CultureCache? cache = _lastCultureCache;
        if (cache is not null
            && cache.Generation == generation
            && string.Equals(cache.CultureName, culture.Name, StringComparison.Ordinal))
        {
            return cache;
        }

        lock (GetLocalizedLoadGate())
        {
            generation = Volatile.Read(ref _localizedGeneration);
            cache = _lastCultureCache;
            if (cache is not null
                && cache.Generation == generation
                && string.Equals(cache.CultureName, culture.Name, StringComparison.Ordinal))
            {
                return cache;
            }

            if (_cultureCaches is not null
                && _cultureCaches.TryGetValue(culture.Name, out cache)
                && cache.Generation == generation)
            {
                _lastCultureCache = cache;
                return cache;
            }

            SatelliteStringResourceSourceMetadata metadata = GetSourceMetadata();
            IndexedStringResourceTable[] tables = LoadCultureChain(culture, metadata);
            cache = new(culture.Name, generation, tables);
            (_cultureCaches ??= [with(StringComparer.Ordinal)])[culture.Name] = cache;
            _lastCultureCache = cache;
            return cache;
        }
    }

    private IndexedStringResourceTable[] LoadCultureChain(
        CultureInfo culture,
        SatelliteStringResourceSourceMetadata metadata)
    {
        IndexedStringResourceTable? first = null;
        List<IndexedStringResourceTable>? multiple = null;

        for (CultureInfo current = culture;
            !current.Equals(CultureInfo.InvariantCulture)
                && !IsNeutralCulture(current.Name, metadata.NeutralCultureName);
            current = current.Parent)
        {
            if (_sourceKind != SatelliteStringResourceSourceKind.RuntimeSatellites)
            {
                ValidatePathSegment(current.Name, nameof(culture));
            }

            IndexedStringResourceTable? table = GetOrLoadSourceTable(current, metadata);
            if (table is null)
            {
                continue;
            }

            if (first is null)
            {
                first = table;
                continue;
            }

            (multiple ??= [first]).Add(table);
        }

        return first is null
            ? []
            : multiple is null ? [first] : [.. multiple];
    }

    private IndexedStringResourceTable? GetOrLoadSourceTable(
        CultureInfo culture,
        SatelliteStringResourceSourceMetadata metadata)
    {
        _sourceTables ??= [with(StringComparer.Ordinal)];
        if (_sourceTables.TryGetValue(culture.Name, out LocalizedStringResourceTableCache? cache))
        {
            return cache.Table;
        }

        try
        {
            cache = new(TryLoadSourceTable(culture, metadata));
        }
        catch (Exception exception)
        {
            cache = new(exception);
        }

        _sourceTables.Add(culture.Name, cache);
        return cache.Table;
    }

    /// <summary>
    ///  Returns whether two names identify the same resource culture.
    /// </summary>
    /// <param name="cultureName">The requested culture name.</param>
    /// <param name="neutralCultureName">The declared neutral culture name.</param>
    /// <returns><see langword="true"/> when the names identify the same culture.</returns>
    internal static bool IsNeutralCulture(string cultureName, string? neutralCultureName) =>
        string.Equals(cultureName, neutralCultureName, StringComparison.OrdinalIgnoreCase);

    private SatelliteStringResourceSourceMetadata GetSourceMetadata()
    {
        if (_resourceAssemblySource is not null)
        {
            SatelliteStringResourceSourceMetadata externalMetadata =
                _neutralResources.GetManagedAssemblySourceMetadata(_resourceAssemblySource);

            string satelliteAssemblyFileName = externalMetadata.SatelliteAssemblyFileName
                ?? throw new InvalidOperationException("The satellite assembly filename was not initialized.");

            ValidatePathSegment(satelliteAssemblyFileName, "resourceAssemblyFile");
            return externalMetadata;
        }

        SatelliteStringResourceSourceMetadata? metadata = _neutralSourceMetadata;
        if (metadata is not null)
        {
            return metadata;
        }

        Assembly? resourceAssembly = _resourceAssembly ?? _neutralResources.SourceAssembly;
        if (resourceAssembly is null)
        {
            metadata = new(
                resourceAssemblyIdentity: null,
                satelliteAssemblyFileName: null,
                neutralCultureName: null,
                satelliteContractVersion: null);
        }
        else
        {
            metadata = s_satelliteAssemblies
                .GetOrCreateValue(resourceAssembly)
                .GetMetadata(
                    resourceAssembly,
                    includeContractVersion: _sourceKind != SatelliteStringResourceSourceKind.ResourcesDirectory);

            if (_resourceAssemblySimpleNameAlias is not null)
            {
                metadata = metadata.WithSimpleName(_resourceAssemblySimpleNameAlias);
            }

            if (_sourceKind == SatelliteStringResourceSourceKind.SatelliteDirectory
                && metadata.SatelliteAssemblyFileName is not null)
            {
                ValidatePathSegment(metadata.SatelliteAssemblyFileName, nameof(resourceAssembly));
            }
        }

        _neutralSourceMetadata = metadata;
        return metadata;
    }

    private IndexedStringResourceTable? TryLoadSourceTable(
        CultureInfo culture,
        SatelliteStringResourceSourceMetadata metadata)
    {
        return _sourceKind switch
        {
            SatelliteStringResourceSourceKind.RuntimeSatellites => LoadRuntimeSatellite(culture, metadata),
            SatelliteStringResourceSourceKind.ResourcesDirectory => LoadResourcesFile(culture),
            SatelliteStringResourceSourceKind.SatelliteDirectory => LoadSatelliteFile(culture, metadata),
            _ => throw new InvalidOperationException("The localized resource source is invalid.")
        };
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private IndexedStringResourceTable? LoadRuntimeSatellite(
        CultureInfo culture,
        SatelliteStringResourceSourceMetadata metadata)
    {
        Assembly resourceAssembly = _resourceAssembly
            ?? throw new InvalidOperationException("The resource assembly was not initialized.");

        SatelliteAssemblyCache assemblyCache = s_satelliteAssemblies.GetOrCreateValue(resourceAssembly);
        Assembly? satellite = assemblyCache.GetSatelliteAssembly(
            resourceAssembly,
            culture,
            metadata.SatelliteContractVersion);

        if (satellite is null)
        {
            return null;
        }

        string resourceName = $"{BaseName}.{culture.Name}.resources";
        return StringResourceTableLoader.LoadIndexedTableFromAssembly(
            satellite,
            resourceName,
            Options)
            ?? throw new MissingManifestResourceException(
                $"The satellite assembly '{satellite.FullName}' does not contain '{resourceName}'.");
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private IndexedStringResourceTable? LoadResourcesFile(CultureInfo culture)
    {
        string resourcesRoot = _localizedRoot
            ?? throw new InvalidOperationException("The resources directory was not initialized.");

        string resourcesPath = Path.Join(resourcesRoot, culture.Name, _resourceName);
        MappedMemoryManager resourcesFile;
        try
        {
            resourcesFile = _openFile(resourcesPath);
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }

        return StringResourceTableLoader.LoadIndexedTableFromResourcesFile(
            resourcesFile.Memory,
            Options,
            resourcesFile);
    }

    [MethodImpl(MethodImplOptions.NoInlining)]
    private IndexedStringResourceTable? LoadSatelliteFile(
        CultureInfo culture,
        SatelliteStringResourceSourceMetadata metadata)
    {
        string satelliteRoot = _localizedRoot
            ?? throw new InvalidOperationException("The satellite directory was not initialized.");

        string satelliteFileName = metadata.SatelliteAssemblyFileName
            ?? throw new InvalidOperationException("The satellite assembly filename was not initialized.");

        string satellitePath = Path.Join(satelliteRoot, culture.Name, satelliteFileName);
        try
        {
            MappedMemoryManager satelliteFile = _openFile(satellitePath);
            string resourceName = $"{BaseName}.{culture.Name}.resources";
            return StringResourceTableLoader.LoadIndexedTableFromSatelliteAssembly(
                satelliteFile.Memory,
                resourceName,
                Options,
                satelliteFile,
                metadata,
                culture.Name)
                ?? throw new MissingManifestResourceException(
                    $"The satellite assembly '{satellitePath}' does not contain '{resourceName}'.");
        }
        catch (Exception exception) when (exception is FileNotFoundException or DirectoryNotFoundException)
        {
            return null;
        }
        catch (Exception exception) when (
            _probeMode == SatelliteStringResourceProbeMode.FallbackOnFailure
                && IsToleratedSatelliteFailure(exception))
        {
            return null;
        }
    }

    private object GetLocalizedLoadGate()
    {
        object? gate = Volatile.Read(ref _localizedLoadGate);
        if (gate is not null)
        {
            return gate;
        }

        object newGate = new();
        return Interlocked.CompareExchange(ref _localizedLoadGate, newGate, comparand: null) ?? newGate;
    }

    private static void ValidatePathSegment(string value, string paramName)
    {
        if (value.Length == 0
            || value is "." or ".."
            || value.Contains('/')
            || value.Contains('\\'))
        {
            throw new ArgumentException("The value must be a single non-empty path segment.", paramName);
        }
    }

    private static bool IsToleratedSatelliteFailure(Exception exception) =>
        exception is ArgumentException
            or BadImageFormatException
            or IOException
            or MissingManifestResourceException
            or NotSupportedException
            or UnauthorizedAccessException
            or System.Security.SecurityException;
}
