// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Reflection;
using System.Threading;

namespace Microsoft.DotNet.Cli.Resources;

/// <summary>
///  Selects the process-wide factory used by generated localized string resource accessors.
/// </summary>
/// <remarks>
///  <para>
///   Register a provider during application startup, before any generated localized resource
///   accessor initializes its manager. If no provider is registered before first use, the selection
///   is frozen to <see cref="SatelliteStringResourceManager.FromRuntimeSatellites(string, Assembly)"/>.
///  </para>
///  <para>
///   Provider selection is thread-safe and can happen only once. Each generated resource class
///   caches the resulting manager independently.
///  </para>
/// </remarks>
public static class StringResourceManagerProvider
{
    private static readonly object s_runtimeSatelliteProvider = new();
    private static object? s_selectedProvider;

    /// <summary>
    ///  Registers the provider used by generated localized string resource accessors.
    /// </summary>
    /// <param name="provider">A factory that receives the base name and generated owner assembly.</param>
    /// <exception cref="ArgumentNullException"><paramref name="provider"/> is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">
    ///  A provider or the default runtime-satellite behavior has already been selected.
    /// </exception>
    public static void Register(Func<string, Assembly, StringResourceManager> provider)
    {
        ArgumentNullException.ThrowIfNull(provider);
        if (Interlocked.CompareExchange(ref s_selectedProvider, provider, comparand: null) is not null)
        {
            throw new InvalidOperationException("A string resource manager provider has already been selected.");
        }
    }

    /// <summary>
    ///  Registers the platform resource manager adapter for resources embedded by the deployment
    ///  toolchain.
    /// </summary>
    /// <exception cref="InvalidOperationException">
    ///  A provider or the default runtime-satellite behavior has already been selected.
    /// </exception>
    public static void RegisterEmbedded() => Register(
        static (baseName, ownerAssembly) => new EmbeddedStringResourceManager(baseName, ownerAssembly));

    /// <summary>
    ///  Creates a manager using the registered provider or the runtime-satellite default.
    /// </summary>
    /// <param name="baseName">The root name of the resource table.</param>
    /// <param name="ownerAssembly">The generated accessor's owner assembly.</param>
    /// <returns>The created string resource manager.</returns>
    /// <exception cref="ArgumentNullException">
    ///  <paramref name="baseName"/> or <paramref name="ownerAssembly"/> is <see langword="null"/>.
    /// </exception>
    /// <exception cref="InvalidOperationException">The registered provider returns <see langword="null"/>.</exception>
    public static StringResourceManager Create(string baseName, Assembly ownerAssembly)
    {
        ArgumentNullException.ThrowIfNull(baseName);
        ArgumentNullException.ThrowIfNull(ownerAssembly);

        object selectedProvider = Volatile.Read(ref s_selectedProvider)
            ?? Interlocked.CompareExchange(
                ref s_selectedProvider,
                s_runtimeSatelliteProvider,
                comparand: null)
            ?? s_runtimeSatelliteProvider;

        if (ReferenceEquals(selectedProvider, s_runtimeSatelliteProvider))
        {
            return SatelliteStringResourceManager.FromRuntimeSatellites(baseName, ownerAssembly);
        }

        Func<string, Assembly, StringResourceManager> provider =
            (Func<string, Assembly, StringResourceManager>)selectedProvider;

        StringResourceManager? manager = provider(baseName, ownerAssembly);
        return manager
            ?? throw new InvalidOperationException("The string resource manager provider returned null.");
    }

    /// <summary>
    ///  Resets provider selection for isolated tests.
    /// </summary>
    internal static void ResetForTests() => Interlocked.Exchange(ref s_selectedProvider, value: null);
}
