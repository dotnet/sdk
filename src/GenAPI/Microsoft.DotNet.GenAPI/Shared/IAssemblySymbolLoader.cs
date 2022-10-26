// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Generic;
using System.IO;
using Microsoft.CodeAnalysis;

namespace Microsoft.DotNet.GenAPI.Shared;

/// <summary>
/// Interface responsible for creating Compilation Factory and loading <see cref="IAssemblySymbol"/> out of binaries.
/// </summary>
public interface IAssemblySymbolLoader
{
    /// <summary>
    /// Adds a set of paths to the search directories to resolve references from. Paths may
    /// be directories or full paths to assembly files.
    /// This is only used when the setting to resolve assembly references is set to true.
    /// </summary>
    /// <param name="paths">The list of paths to register as search directories.</param>
    void AddReferenceSearchDirectories(IEnumerable<string> paths);

    /// <summary>
    /// Adds a path to the search directories to resolve references from. Path may
    /// be directory or full path to assembly file.
    /// This is only used when the setting to resolve assembly references is set to true.
    /// </summary>
    /// <param name="path">The path to register as search directories.</param>
    void AddReferenceSearchDirectory(string path);

    /// <summary>
    /// Indicates if the <see cref="CSharpCompilation"/> used to resolve binaries has any roslyn diagnostics.
    /// Might be useful when loading an assembly from source files.
    /// </summary>
    /// <returns>Return list of diagnostics.</returns>
    IEnumerable<Diagnostic> GetRoslynDiagnostics();

    /// <summary>
    /// Gets the list of warnings the loader emitted that might affect the assembly resolution.
    /// </summary>
    /// <returns>List of warnings.</returns>
    IEnumerable<string> GetResolutionWarnings();

    /// <summary>
    /// Loads a list of assemblies and gets its corresponding <see cref="IAssemblySymbol"/> from the specified paths.
    /// </summary>
    /// <param name="paths">List of paths to load binaries from. Can be full paths to binaries or a directory.</param>
    /// <returns>The list of resolved <see cref="IAssemblySymbol"/>.</returns>
    IEnumerable<IAssemblySymbol> LoadAssemblies(IEnumerable<string> paths);

    /// <summary>
    /// Loads an assembly from the provided path.
    /// </summary>
    /// <param name="path">The full path to the assembly.</param>
    /// <returns><see cref="IAssemblySymbol"/> representing the loaded assembly.</returns>
    IAssemblySymbol? LoadAssembly(string path);

    /// <summary>
    /// Loads an assembly using the provided name from a given <see cref="Stream"/>.
    /// </summary>
    /// <param name="name">The name to use to resolve the assembly.</param>
    /// <param name="stream">The stream to read the metadata from.</param>
    /// <returns><see cref="IAssemblySymbol"/> respresenting the given <paramref name="stream"/>. If an 
    /// assembly with the same <paramref name="name"/> was already loaded, the previously loaded assembly is returned.</returns>
    IAssemblySymbol? LoadAssembly(string name, Stream stream);
}
