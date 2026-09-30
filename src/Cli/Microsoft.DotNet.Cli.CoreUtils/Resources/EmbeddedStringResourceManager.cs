// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Globalization;
using System.Reflection;
using System.Resources;

namespace Microsoft.DotNet.Cli.Resources;

/// <summary>
///  Adapts <see cref="ResourceManager"/> to <see cref="StringResourceManager"/> for resources
///  embedded by the deployment toolchain.
/// </summary>
/// <remarks>
///  This manager uses the platform resource implementation for culture fallback. In particular,
///  NativeAOT can resolve localized resources embedded by ILC without calling
///  <see cref="Assembly.GetSatelliteAssembly(CultureInfo)"/>.
/// </remarks>
public sealed class EmbeddedStringResourceManager : StringResourceManager
{
    private readonly ResourceManager _resourceManager;

    /// <summary>
    ///  Initializes an embedded resource manager.
    /// </summary>
    /// <param name="baseName">The root name of the resource table.</param>
    /// <param name="ownerAssembly">The generated accessor's owner assembly.</param>
    /// <exception cref="ArgumentNullException">
    ///  <paramref name="baseName"/> or <paramref name="ownerAssembly"/> is <see langword="null"/>.
    /// </exception>
    public EmbeddedStringResourceManager(string baseName, Assembly ownerAssembly)
        : base(baseName, ownerAssembly)
    {
        _resourceManager = new(baseName, ownerAssembly);
    }

    /// <inheritdoc/>
    public override string? GetString(string name, CultureInfo? culture)
    {
        ArgumentNullException.ThrowIfNull(name);
        return _resourceManager.GetString(name, culture);
    }

    /// <inheritdoc/>
    public override void ReleaseAllResources() => _resourceManager.ReleaseAllResources();
}
