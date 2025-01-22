// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.

using System.Collections.Immutable;
using System.IO.Compression;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.DotNet.ApiSymbolExtensions.Logging;

namespace Microsoft.DotNet.ApiSymbolExtensions
{
    /// <summary>
    /// Loads <see cref="IAssemblySymbol"/> objects from source files, binaries or directories containing binaries.
    /// </summary>
    public class AssemblySymbolLoader : IAssemblySymbolLoader
    {
        // This is a list of dangling .NET Framework internal assemblies that should never get loaded.
        private static readonly HashSet<string> s_assembliesToIgnore = [
            "System.ServiceModel.Internals",
            "Microsoft.Internal.Tasks.Dataflow",
            "MSDATASRC",
            "ADODB",
            "Microsoft.StdFormat",
            "stdole",
            "PresentationUI",
            "Microsoft.VisualBasic.Activities.Compiler",
            "SMDiagnostics",
            "System.Xaml.Hosting",
            "Microsoft.Transactions.Bridge",
            "Microsoft.Workflow.Compiler"
        ];

        private readonly ILog _log;
        // Dictionary that holds the paths to help loading dependencies. Keys will be assembly name and
        // value are the containing folder.
        private readonly Dictionary<string, string> _referencePathFiles = new(StringComparer.OrdinalIgnoreCase);
        private readonly HashSet<string> _referencePathDirectories = new(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, MetadataReference> _loadedAssemblies;
        private readonly bool _resolveReferences;
        private CSharpCompilation _cSharpCompilation;

        /// <summary>
        /// Error code that is emitted when an assembly isn't found.
        /// </summary>
        public const string AssemblyNotFoundErrorCode = "CP1001";

        /// <summary>
        /// Error code that is emitted when an assembly reference isn't found.
        /// </summary>
        public const string AssemblyReferenceNotFoundErrorCode = "CP1002";

        /// <summary>
        /// Creates a new instance of the <see cref="AssemblySymbolLoader"/> class.
        /// </summary>
        /// <param name="log">A logger instance for logging message.</param>
        /// <param name="resolveAssemblyReferences">True to attempt to load references for loaded assemblies from the locations specified with <see cref="AddReferenceSearchPaths(string[])"/>. Default is false.</param>
        /// <param name="includeInternalSymbols">True to include all internal metadata for assemblies loaded. Default is false which only includes public and some internal metadata. <seealso cref="MetadataImportOptions"/></param>
        public AssemblySymbolLoader(ILog log, bool resolveAssemblyReferences = false, bool includeInternalSymbols = false)
        {
            _log = log;
            _loadedAssemblies = [];
            CSharpCompilationOptions compilationOptions = new(OutputKind.DynamicallyLinkedLibrary, nullableContextOptions: NullableContextOptions.Enable,
                metadataImportOptions: includeInternalSymbols ? MetadataImportOptions.Internal : MetadataImportOptions.Public);
            _cSharpCompilation = CSharpCompilation.Create($"AssemblyLoader_{DateTime.Now:MM_dd_yy_HH_mm_ss_FFF}", options: compilationOptions);
            _resolveReferences = resolveAssemblyReferences;
        }

        /// <inheritdoc />
        public void AddReferenceSearchPaths(params string[] paths)
        {
            foreach (string path in paths)
            {
                if (Directory.Exists(path))
                {
                    _referencePathDirectories.Add(path);
                }
                else
                {
                    string assemblyName = Path.GetFileName(path);
                    if (!_referencePathFiles.ContainsKey(assemblyName))
                    {
                        string? directoryName = Path.GetDirectoryName(path);
                        if (directoryName != null)
                        {
                            _referencePathFiles.Add(assemblyName, directoryName);
                            _referencePathDirectories.Add(directoryName);
                        }
                    }
                }
            }
        }

        /// <inheritdoc />
        public IReadOnlyList<IAssemblySymbol?> LoadAssemblies(params string[] paths)
        {
            _log.LogMessage(MessageImportance.Low, string.Format(Resources.LoadingAssemblies, string.Join(", ", paths)));

            // First resolve all assemblies that are passed in and create metadata references out of them.
            // Reference assemblies of the passed in assemblies that themselves are passed in, will be skipped to be resolved,
            // as they are resolved as part of the loop below.
            ImmutableHashSet<string> fileNames = paths.Select(path => Path.GetFileName(path)).ToImmutableHashSet();
            List<MetadataReference> assembliesToReturn = LoadFromPaths(paths, fileNames);

            // Create IAssemblySymbols out of the MetadataReferences.
            // Doing this after resolving references to make sure that references are available.
            IAssemblySymbol?[] assemblySymbols = new IAssemblySymbol[assembliesToReturn.Count];
            for (int i = 0; i < assembliesToReturn.Count; i++)
            {
                MetadataReference metadataReference = assembliesToReturn[i];
                ISymbol? symbol = _cSharpCompilation.GetAssemblyOrModuleSymbol(metadataReference);
                assemblySymbols[i] = symbol as IAssemblySymbol;
            }

            LogCompilationDiagnostics();

            return assemblySymbols;
        }

        /// <inheritdoc />
        public IReadOnlyList<IAssemblySymbol?> LoadAssembliesFromArchive(string archivePath, IReadOnlyList<string> relativePaths)
        {
            _log.LogMessage(MessageImportance.Low, string.Format(Resources.LoadingAssembliesFromArchive, string.Join(", ", relativePaths), archivePath));

            using FileStream stream = File.OpenRead(archivePath);
            using ZipArchive zipFile = new(stream);

            // First resolve all assemblies that are passed in and create metadata references out of them. Reference assemblies of the
            // assemblies inside the archive that themselves are part of the archive will be skipped to be resolved, as they are resolved
            // as part of the loop below.
            ImmutableHashSet<string> fileNames = relativePaths.Select(relativePath => Path.GetFileName(relativePath)).ToImmutableHashSet();
            MetadataReference?[] metadataReferences = new MetadataReference[relativePaths.Count];
            for (int i = 0; i < relativePaths.Count; i++)
            {
                ZipArchiveEntry? entry = zipFile.GetEntry(relativePaths[i]);
                if (entry == null)
                {
                    metadataReferences[i] = null;
                    continue;
                }

                using MemoryStream memoryStream = new();
                using Stream entryStream = entry.Open();
                entryStream.CopyTo(memoryStream);
                memoryStream.Seek(0, SeekOrigin.Begin);

                string name = Path.GetFileName(relativePaths[i]);
                if (!_loadedAssemblies.TryGetValue(name, out MetadataReference? metadataReference))
                {
                    string rootAssemblyDisplayString = string.Format(Resources.RootAssemblyFromPackageDisplayString, relativePaths[i], archivePath);
                    metadataReference = CreateAndAddReferenceToCompilation(name, rootAssemblyDisplayString, memoryStream, fileNames);
                }

                metadataReferences[i] = metadataReference;
            }

            // Create IAssemblySymbols out of the MetadataReferences. At this point, references are resolved
            // and part of the compilation context.
            IAssemblySymbol?[] assemblySymbols = new IAssemblySymbol[metadataReferences.Length];
            for (int i = 0; i < metadataReferences.Length; i++)
            {
                MetadataReference? metadataReference = metadataReferences[i];

                assemblySymbols[i] = metadataReference != null ?
                    _cSharpCompilation.GetAssemblyOrModuleSymbol(metadataReference) as IAssemblySymbol :
                    null;
            }

            LogCompilationDiagnostics();

            return assemblySymbols;
        }

        /// <inheritdoc />
        public IAssemblySymbol? LoadAssembly(string path)
        {
            _log.LogMessage(MessageImportance.Low, string.Format(Resources.LoadingAssembly, path));

            MetadataReference metadataReference = CreateOrGetMetadataReferenceFromPath(path);
            IAssemblySymbol? assemblySymbol = _cSharpCompilation.GetAssemblyOrModuleSymbol(metadataReference) as IAssemblySymbol;
            LogCompilationDiagnostics();

            return assemblySymbol;
        }

        /// <inheritdoc />
        public IAssemblySymbol? LoadAssembly(string name, Stream stream)
        {
            _log.LogMessage(MessageImportance.Low, string.Format(Resources.LoadingAssemblyFromStream, name));

            if (stream.Position >= stream.Length)
            {
                throw new ArgumentException(Resources.StreamPositionGreaterThanLength, nameof(stream));
            }

            if (!_loadedAssemblies.TryGetValue(name, out MetadataReference? metadataReference))
            {
                string rootAssemblyDisplayString = string.Format(Resources.RootAssemblyDisplayString, name);
                metadataReference = CreateAndAddReferenceToCompilation(name, rootAssemblyDisplayString, stream);
            }

            IAssemblySymbol? assemblySymbol = _cSharpCompilation.GetAssemblyOrModuleSymbol(metadataReference) as IAssemblySymbol;
            LogCompilationDiagnostics();

            return assemblySymbol;
        }

        /// <inheritdoc />
        public IAssemblySymbol LoadAssemblyFromSourceFiles(IEnumerable<string> filePaths, string? assemblyName, IEnumerable<string> referencePaths)
        {
            if (!filePaths.Any())
            {
                throw new ArgumentNullException(nameof(filePaths), Resources.ShouldNotBeNullAndContainAtLeastOneElement);
            }

            if (string.IsNullOrEmpty(assemblyName))
            {
                throw new ArgumentNullException(nameof(assemblyName), Resources.ShouldProvideValidAssemblyName);
            }

            _cSharpCompilation = _cSharpCompilation.WithAssemblyName(assemblyName);

            List<SyntaxTree> syntaxTrees = [];
            foreach (string filePath in filePaths)
            {
                syntaxTrees.Add(CSharpSyntaxTree.ParseText(File.ReadAllText(filePath)));
            }

            _cSharpCompilation = _cSharpCompilation.AddSyntaxTrees(syntaxTrees);

            LoadFromPaths(referencePaths);
            LogCompilationDiagnostics();

            return _cSharpCompilation.Assembly;
        }

        /// <inheritdoc />
        public IEnumerable<IAssemblySymbol> LoadMatchingAssemblies(IEnumerable<IAssemblySymbol> fromAssemblies, IEnumerable<string> searchPaths, bool validateMatchingIdentity = true, bool warnOnMissingAssemblies = true)
        {
            List<IAssemblySymbol> matchingAssemblies = [];
            foreach (IAssemblySymbol assembly in fromAssemblies)
            {
                bool found = false;
                string name = $"{assembly.Name}.dll";
                foreach (string directory in searchPaths)
                {
                    if (!Directory.Exists(directory))
                    {
                        throw new FileNotFoundException(string.Format(Resources.ShouldProvideValidAssemblyName, directory), nameof(searchPaths));
                    }

                    string possiblePath = Path.Combine(directory, name);
                    if (File.Exists(possiblePath))
                    {
                        MetadataReference reference = CreateOrGetMetadataReferenceFromPath(possiblePath);
                        ISymbol? symbol = _cSharpCompilation.GetAssemblyOrModuleSymbol(reference);
                        if (symbol is IAssemblySymbol matchingAssembly)
                        {
                            if (validateMatchingIdentity && !matchingAssembly.Identity.Equals(assembly.Identity))
                            {
                                _cSharpCompilation = _cSharpCompilation.RemoveReferences([ reference ]);
                                _loadedAssemblies.Remove(name);
                                continue;
                            }

                            matchingAssemblies.Add(matchingAssembly);
                            found = true;
                            break;
                        }
                    }
                }

                if (warnOnMissingAssemblies && !found)
                {
                    string assemblyInfo = validateMatchingIdentity ? assembly.Identity.GetDisplayName() : assembly.Name;
                    _log.LogWarning(AssemblyNotFoundErrorCode, string.Format(Resources.MatchingAssemblyNotFound, assemblyInfo));
                }
            }

            LogCompilationDiagnostics();

            return matchingAssemblies;
        }

        /// <inheritdoc />
        public IEnumerable<MetadataReference> MetadataReferences => _cSharpCompilation.References;

        private List<MetadataReference> LoadFromPaths(IEnumerable<string> paths, ImmutableHashSet<string>? referenceAssemblyNamesToIgnore = null)
        {
            List<MetadataReference> result = [];
            foreach (string path in paths)
            {
                if (Directory.Exists(path))
                {
                    // If a directory is passed in as a path, add that to the reference paths.
                    // Otherwise, if a file is passed in, add its parent directory to the reference paths.
                    _referencePathDirectories.Add(path);

                    foreach (string assembly in Directory.EnumerateFiles(path, "*.dll"))
                    {
                        result.Add(CreateOrGetMetadataReferenceFromPath(assembly, referenceAssemblyNamesToIgnore));
                    }
                }
                else if (File.Exists(path))
                {
                    string? directory = Path.GetDirectoryName(path);
                    // If a directory is passed in as a path, add that to the reference paths.
                    // Otherwise, if a file is passed in, add its parent directory to the reference paths.
                    if (!string.IsNullOrEmpty(directory))
                        _referencePathDirectories.Add(directory);

                    result.Add(CreateOrGetMetadataReferenceFromPath(path, referenceAssemblyNamesToIgnore));
                }
                else
                {
                    throw new FileNotFoundException(string.Format(Resources.ProvidedPathToLoadBinariesFromNotFound, path));
                }
            }

            return result;
        }

        private MetadataReference CreateOrGetMetadataReferenceFromPath(string path, ImmutableHashSet<string>? referenceAssemblyNamesToIgnore = null)
        {
            // Roslyn doesn't support having two assemblies as references with the same identity and then getting the symbol for it.
            string name = Path.GetFileName(path);
            if (!_loadedAssemblies.TryGetValue(name, out MetadataReference? metadataReference))
            {
                using FileStream stream = File.OpenRead(path);
                string rootAssemblyDisplayString = string.Format(Resources.RootAssemblyDisplayString, path);
                metadataReference = CreateAndAddReferenceToCompilation(name, rootAssemblyDisplayString, stream, referenceAssemblyNamesToIgnore);
            }

            return metadataReference;
        }

        private MetadataReference CreateAndAddReferenceToCompilation(string assemblyName, string rootAssemblyDisplayString, Stream fileStream, ImmutableHashSet<string>? referenceAssemblyNamesToIgnore = null, string? referenceeAssemblyName = null)
        {
            // If we need to resolve references we can't reuse the same stream after creating the metadata
            // reference from it as Roslyn closes it. So instead we use PEReader and get the bytes
            // and create the metadata reference from that.
            using PEReader reader = new(fileStream);

            if (!reader.HasMetadata)
            {
                throw new ArgumentException(string.Format(Resources.ProvidedStreamDoesNotHaveMetadata, assemblyName));
            }

            PEMemoryBlock image = reader.GetEntireImage();
            MetadataReference metadataReference = MetadataReference.CreateFromImage(image.GetContent());
            _loadedAssemblies.Add(assemblyName, metadataReference);
            _cSharpCompilation = _cSharpCompilation.AddReferences([ metadataReference ]);

            if (referenceeAssemblyName is null)
            {
                _log.LogMessage(MessageImportance.Low, string.Format(Resources.AssemblyLoaded, assemblyName));
            }
            else
            {
                _log.LogMessage(MessageImportance.Low, string.Format(Resources.AssemblyReferenceLoaded, assemblyName, referenceeAssemblyName));
            }

            if (_resolveReferences)
            {
                ResolveReferences(reader, assemblyName, rootAssemblyDisplayString, referenceAssemblyNamesToIgnore);
            }

            return metadataReference;
        }

        private void ResolveReferences(PEReader peReader, string assemblyName, string rootAssemblyDisplayString, ImmutableHashSet<string>? referenceAssemblyNamesToIgnore = null)
        {
            MetadataReader reader = peReader.GetMetadataReader();
            foreach (AssemblyReferenceHandle assemblyReferenceHandle in reader.AssemblyReferences)
            {
                AssemblyReference assemblyReference = reader.GetAssemblyReference(assemblyReferenceHandle);
                string assemblyReferenceNameWithoutExtension = reader.GetString(assemblyReference.Name);

                // Skip assemblies that should never get loaded because they are purely internal
                if (s_assembliesToIgnore.Contains(assemblyReferenceNameWithoutExtension))
                    continue;

                string assemblyReferenceName = assemblyReferenceNameWithoutExtension + ".dll";

                // Skip reference assemblies that are loaded later.
                if (referenceAssemblyNamesToIgnore != null && referenceAssemblyNamesToIgnore.Contains(assemblyReferenceName))
                    continue;

                // If the assembly reference is already loaded, don't do anything.
                if (_loadedAssemblies.ContainsKey(assemblyReferenceName))
                    continue;

                // First we try to see if a reference path for this specific assembly was passed in directly, and if so
                // we use that.
                if (_referencePathFiles.TryGetValue(assemblyReferenceName, out string? fullReferencePath))
                {
                    // TODO: add version check and add a warning if it doesn't match?
                    using FileStream resolvedStream = File.OpenRead(Path.Combine(fullReferencePath, assemblyReferenceName));
                    CreateAndAddReferenceToCompilation(assemblyReferenceName, rootAssemblyDisplayString, resolvedStream, referenceAssemblyNamesToIgnore, assemblyName);
                }
                // If we can't find a specific reference path for the dependency, then we look in the folders where the
                // rest of the reference paths are located to see if we can find the dependency there.
                else
                {
                    bool found = false;

                    foreach (string referencePathDirectory in _referencePathDirectories)
                    {
                        string potentialPath = Path.Combine(referencePathDirectory, assemblyReferenceName);
                        if (File.Exists(potentialPath))
                        {
                            // TODO: add version check and add a warning if it doesn't match?
                            using FileStream resolvedStream = File.OpenRead(potentialPath);
                            CreateAndAddReferenceToCompilation(assemblyReferenceName, rootAssemblyDisplayString, resolvedStream, referenceAssemblyNamesToIgnore, assemblyName);
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        _log.LogWarning(AssemblyReferenceNotFoundErrorCode,
                            string.Format(Resources.CouldNotResolveReference, assemblyReferenceName, rootAssemblyDisplayString));
                    }
                }
            }
        }

        private void LogCompilationDiagnostics()
        {
            var diagnostics = _cSharpCompilation.GetDiagnostics();
            foreach (Diagnostic warning in diagnostics)
            {
                _log.LogMessage(MessageImportance.Normal, warning.ToString());
            }
        }
    }
}
