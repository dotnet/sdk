// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;

namespace Microsoft.DotNet.GenAPI.Shared;

/// <summary>
/// Loads <see cref="IAssemblySymbol"/> objects from source files, binaries or directories containing binaries.
/// </summary>
public class AssemblySymbolLoader : IAssemblySymbolLoader
{
    /// <summary>
    /// Dictionary that holds the paths to help loading dependencies. Keys will be assembly name and 
    /// value are the containing folder.
    /// </summary>
    private readonly Dictionary<string, string> _referencePaths = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, MetadataReference> _loadedAssemblies = new();
    private readonly List<string> _warnings = new();
    private readonly bool _resolveReferences;
    private CSharpCompilation _cSharpCompilation;

    /// <inheritdoc />
    public AssemblySymbolLoader(bool resolveAssemblyReferences = false)
    {
        _resolveReferences = resolveAssemblyReferences;

        var compilationOptions = new CSharpCompilationOptions(
            OutputKind.DynamicallyLinkedLibrary,
            nullableContextOptions: NullableContextOptions.Enable);
        _cSharpCompilation = CSharpCompilation.Create(
            $"AssemblyLoader_{DateTime.Now:MM_dd_yy_HH_mm_ss_FFF}",
            options: compilationOptions);
    }

    /// <inheritdoc />
    public void AddReferenceSearchDirectories(IEnumerable<string> paths)
    {
        if (paths == null)
        {
            throw new ArgumentNullException(nameof(paths));
        }

        foreach (string path in paths)
        {
            AddReferenceSearchDirectory(path);
        }
    }

    /// <inheritdoc />
    public void AddReferenceSearchDirectory(string path)
    {
        FileAttributes attr = File.GetAttributes(path);

        if (attr.HasFlag(FileAttributes.Directory))
        {
            _referencePaths.TryAdd(path, path);
        }
        else
        {
            string assemblyName = Path.GetFileName(path);
            if (!_referencePaths.ContainsKey(assemblyName))
            {
                string? directoryName = Path.GetDirectoryName(path);
                if (directoryName != null)
                {
                    _referencePaths.Add(assemblyName, directoryName);
                }
            }
        }
    }

    /// <inheritdoc />
    public IEnumerable<Diagnostic> GetRoslynDiagnostics() => _cSharpCompilation.GetDiagnostics();

    /// <inheritdoc />
    public IEnumerable<string> GetResolutionWarnings() => _warnings;

    public IEnumerable<IAssemblySymbol> LoadAssemblies(IEnumerable<string> paths)
    {
        IEnumerable<MetadataReference> assembliesToReturn = LoadFromPaths(paths);

        List<IAssemblySymbol> result = new();
        foreach (var metadataReference in assembliesToReturn)
        {
            ISymbol? symbol = _cSharpCompilation.GetAssemblyOrModuleSymbol(metadataReference);
            if (symbol is IAssemblySymbol assemblySymbol)
            {
                result.Add(assemblySymbol);
            }
        }

        return result;
    }

    /// <inheritdoc />
    public IAssemblySymbol? LoadAssembly(string path)
    {
        var metadataReference = CreateOrGetMetadataReferenceFromPath(path);
        return _cSharpCompilation.GetAssemblyOrModuleSymbol(metadataReference) as IAssemblySymbol;
    }

    /// <inheritdoc />
    public IAssemblySymbol? LoadAssembly(string name, Stream stream)
    {
        if (stream.Position >= stream.Length)
        {
            throw new ArgumentException("Stream position is greater than it's length, so there are no contents available to read.", nameof(stream));
        }

        if (!_loadedAssemblies.TryGetValue(name, out MetadataReference? metadataReference))
        {
            metadataReference = CreateAndAddReferenceToCompilation(name, stream);
        }

        return _cSharpCompilation.GetAssemblyOrModuleSymbol(metadataReference) as IAssemblySymbol;
    }

    private IEnumerable<MetadataReference> LoadFromPaths(IEnumerable<string> paths)
    {
        List<MetadataReference> result = new();
        foreach (string path in paths)
        {
            string resolvedPath = Environment.ExpandEnvironmentVariables(path);
            string? directory = null;
            if (Directory.Exists(resolvedPath))
            {
                directory = resolvedPath;
                result.AddRange(LoadAssembliesFromDirectory(resolvedPath));
            }
            else if (File.Exists(resolvedPath))
            {
                directory = Path.GetDirectoryName(resolvedPath);
                result.Add(CreateOrGetMetadataReferenceFromPath(resolvedPath));
            }
            else
            {
                throw new FileNotFoundException(string.Format("Could not find the provided path '{0}' to load binaries from.", resolvedPath));
            }

            if (_resolveReferences && !string.IsNullOrEmpty(directory))
                _referencePaths.Add(Path.GetFileName(directory), directory);
        }

        return result;
    }

    private IEnumerable<MetadataReference> LoadAssembliesFromDirectory(string directory)
    {
        foreach (string assembly in Directory.EnumerateFiles(directory, "*.dll"))
        {
            yield return CreateOrGetMetadataReferenceFromPath(assembly);
        }
    }

    private MetadataReference CreateOrGetMetadataReferenceFromPath(string path)
    {
        // Roslyn doesn't support having two assemblies as references with the same identity and then getting the symbol for it.
        string name = Path.GetFileName(path);
        if (!_loadedAssemblies.TryGetValue(name, out MetadataReference? metadataReference))
        {
            using FileStream stream = File.OpenRead(path);
            metadataReference = CreateAndAddReferenceToCompilation(name, stream);
        }

        return metadataReference;
    }

    private MetadataReference CreateAndAddReferenceToCompilation(string name, Stream fileStream)
    {
        if (_loadedAssemblies.TryGetValue(name, out MetadataReference? metadataReference))
        {
            return metadataReference;
        }
        // If we need to resolve references we can't reuse the same stream after creating the metadata
        // reference from it as Roslyn closes it. So instead we use PEReader and get the bytes
        // and create the metadata reference from that.
        using PEReader reader = new(fileStream);

        if (!reader.HasMetadata)
        {
            throw new ArgumentException(string.Format("Provided stream for assembly '{0}' doesn't have any metadata to read. from.", name));
        }

        var image = reader.GetEntireImage();
        metadataReference = MetadataReference.CreateFromImage(image.GetContent());
        _loadedAssemblies.Add(name, metadataReference);
        _cSharpCompilation = _cSharpCompilation.AddReferences(new MetadataReference[] { metadataReference });

        if (_resolveReferences)
        {
            ResolveReferences(reader);
        }

        return metadataReference;
    }

    private void ResolveReferences(PEReader peReader)
    {
        var reader = peReader.GetMetadataReader();
        foreach (var handle in reader.AssemblyReferences)
        {
            var reference = reader.GetAssemblyReference(handle);
            string name = $"{reader.GetString(reference.Name)}.dll";

            if (!ResolveReferences(name))
            {
                _warnings.Add(string.Format("Could not resolve reference '{0}' in any of the provided search directories.", name));
            }
        }
    }

    private bool ResolveReferences(string assemblyName)
    {
        // First we try to see if a reference path for this specific assembly was passed in directly, and if so
        // we use that.
        if (_referencePaths.TryGetValue(assemblyName, out string? fullReferencePath))
        {
            using var resolvedStream = File.OpenRead(Path.Combine(fullReferencePath, assemblyName));
            CreateAndAddReferenceToCompilation(assemblyName, resolvedStream);
            return true;
        }

        // If we can't find a specific reference path for the dependency, then we look in the folders where the
        // rest of the reference paths are located to see if we can find the dependency there.
        foreach (var referencePath in _referencePaths)
        {
            string potentialPath = Path.Combine(referencePath.Value, assemblyName);
            if (File.Exists(potentialPath))
            {
                using FileStream resolvedStream = File.OpenRead(potentialPath);
                CreateAndAddReferenceToCompilation(assemblyName, resolvedStream);
                return true;
            }
        }
        return false;
    }
}
