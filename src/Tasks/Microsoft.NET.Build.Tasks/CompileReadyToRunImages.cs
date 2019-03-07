// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System.Reflection.Metadata;
using System.Reflection;

namespace Microsoft.NET.Build.Tasks
{
    public class CompileReadyToRunImages : ToolTaskBase
    {
        public ITaskItem[] FilesToPublishAlways { get; set; }
        public ITaskItem[] FilesToPublishPreserveNewest { get; set; }
        public string[] ReadyToRunExcludeList { get; set; }
        public bool ReadyToRunEmitSymbols { get; set; }

        [Required]
        public string OutputPath { get; set; }
        [Required]
        public string TargetFramework { get; set; }
        [Required]
        public ITaskItem[] RuntimePacks { get; set; }


        [Output]
        public ITaskItem[] R2RFilesToPublishAlways => _r2rPublishAlways.ToArray();
        [Output]
        public ITaskItem[] R2RFilesToPublishPreserveNewest => _r2rPublishPreserveNewest.ToArray();

        protected override string ToolName => Path.GetFileName(_crossgenPath);
        protected override string GenerateFullPathToTool() => _crossgenPath;

        private List<ITaskItem> _r2rPublishAlways = new List<ITaskItem>();
        private List<ITaskItem> _r2rPublishPreserveNewest = new List<ITaskItem>();

        private string _runtimeIdentifier;
        private string _packagePath;
        private string _crossgenPath;
        private string _clrjitPath;
        private string _platformAssembliesPath;
        private string _diasymreaderPath;
        private string _pathSeparatorCharacter = ";";

        private OSPlatform _targetPlatform;
        private Architecture _targetArchitecture;

        protected override void ExecuteCore()
        {
            foreach (var pack in RuntimePacks)
            {
                if (String.Compare(pack.GetMetadata(MetadataKeys.FrameworkName), "Microsoft.NETCore.App", true) != 0)
                    continue;

                _runtimeIdentifier = pack.GetMetadata(MetadataKeys.RuntimeIdentifier);
                _packagePath = pack.GetMetadata(MetadataKeys.PackageDirectory);
                break;
            }

            if(_runtimeIdentifier == null || _packagePath == null)
            {
                Log.LogError(Strings.ReadyToRunNoValidRuntimePackageError);
                return;
            }

            if (!ExtractTargetPlatformAndArchitectureFromRuntimeIdentifier() || !GetCrossgenComponentsPaths(_packagePath) || !File.Exists(_crossgenPath) || !File.Exists(_clrjitPath))
            {
                Log.LogError(Strings.ReadyToRunTargedNotSuppotedError);
                return;
            }

            LogStandardErrorAsError = true;

            // Run Crossgen on input assemblies
            ProcessInputFileList(FilesToPublishPreserveNewest, _r2rPublishPreserveNewest);
            ProcessInputFileList(FilesToPublishAlways, _r2rPublishAlways);
        }

        void ProcessInputFileList(ITaskItem[] inputFiles, List<ITaskItem> outputFiles)
        {
            if (inputFiles == null)
            {
                return;
            }

            // There seems to be duplicate entries of the same files in the input lists. Unify them
            var filteredInputs = new Dictionary<string, ITaskItem>(StringComparer.OrdinalIgnoreCase);
            foreach (var file in inputFiles)
                filteredInputs[file.ItemSpec] = file;

            foreach (var file in filteredInputs.Values)
            {
                if (InputFileEligibleForCompilation(file))
                {
                    Log.LogMessage(MessageImportance.Normal, Strings.ReadyToRunCompiling, file.ItemSpec);

                    string outputR2RImage;
                    if (CreateR2RImage(file, out outputR2RImage))
                    {
                        // Update path to newly created R2R image, and drop the input IL image from the list.
                        file.ItemSpec = outputR2RImage;

                        // Note: ReadyToRun PDB/Map files are not needed for debugging. They are only used for profiling, therefore the default behavior is to not generate them
                        // unless an explicit ReadyToRunEmitSymbols flag is enabled by the app developer. There is also another way to profile that the runtime supports, which does
                        // not rely on the native PDBs/Map files, so creating them is really an opt-in option, typically used by advanced users.
                        // For debugging, only the IL PDBs are required.
                        if (ReadyToRunEmitSymbols)
                        {
                            string outputPDBImage;
                            if (CreatePDBForImage(file, out outputPDBImage))
                            {
                                // If we create a native PDB/Map image, we add it to the publishing list in addition to all other files. Native PDBs/Maps do not replace IL PDBs.
                                TaskItem newPDBfile = new TaskItem();
                                newPDBfile.ItemSpec = outputPDBImage;
                                newPDBfile.SetMetadata(MetadataKeys.CopyToPublishDirectory, file.GetMetadata(MetadataKeys.CopyToPublishDirectory));
                                newPDBfile.SetMetadata(MetadataKeys.RelativePath, Path.GetFileName(outputPDBImage));
                                newPDBfile.RemoveMetadata(MetadataKeys.OriginalItemSpec);

                                outputFiles.Add(newPDBfile);
                            }
                        }
                    }
                }

                outputFiles.Add(file);
            }
        }

        bool InputFileEligibleForCompilation(ITaskItem file)
        {
            // Check if the file is explicitly excluded from being compiled
            if (ReadyToRunExcludeList != null)
            {
                foreach (var item in ReadyToRunExcludeList)
                    if (String.Compare(Path.GetFileName(file.ItemSpec), item, true) == 0)
                        return false;
            }

            // Check to see if this is a valid ILOnly image that we can compile
            try
            {
                using (FileStream fs = new FileStream(file.ItemSpec, FileMode.Open, FileAccess.Read))
                {
                    PEReader pereader = new PEReader(fs);

                    if (pereader.PEHeaders == null || pereader.PEHeaders.CorHeader == null)
                        return false;

                    if ((pereader.PEHeaders.CorHeader.Flags & CorFlags.ILOnly) != CorFlags.ILOnly)
                        return false;

                    // Skip assemblies that reference winmds
                    MetadataReader mdReader = pereader.GetMetadataReader();
                    foreach (var assemblyRefHandle in mdReader.AssemblyReferences)
                    {
                        AssemblyReference assemblyRef = mdReader.GetAssemblyReference(assemblyRefHandle);
                        if ((assemblyRef.Flags & AssemblyFlags.WindowsRuntime) == AssemblyFlags.WindowsRuntime)
                            return false;
                    }

                    // Skip reference assemblies
                    foreach (var attributeHandle in mdReader.GetAssemblyDefinition().GetCustomAttributes())
                    {
                        EntityHandle attributeCtor = mdReader.GetCustomAttribute(attributeHandle).Constructor;

                        string attributeTypeName = null, attributeTypeNamespace = null;

                        if (attributeCtor.Kind == HandleKind.MemberReference)
                        {
                            EntityHandle attributeMemberParent = mdReader.GetMemberReference((MemberReferenceHandle)attributeCtor).Parent;
                            if (attributeMemberParent.Kind == HandleKind.TypeReference)
                            {
                                TypeReference attributeTypeRef = mdReader.GetTypeReference((TypeReferenceHandle)attributeMemberParent);
                                attributeTypeName = mdReader.GetString(attributeTypeRef.Name);
                                attributeTypeNamespace = mdReader.GetString(attributeTypeRef.Namespace);
                            }
                        }
                        else if (attributeCtor.Kind == HandleKind.MethodDefinition)
                        {
                            var attributeTypeDefHandle = (TypeDefinitionHandle)mdReader.GetMethodDefinition((MethodDefinitionHandle)attributeCtor).GetDeclaringType();
                            TypeDefinition attributeTypeDef = mdReader.GetTypeDefinition(attributeTypeDefHandle);
                            attributeTypeName = mdReader.GetString(attributeTypeDef.Name);
                            attributeTypeNamespace = mdReader.GetString(attributeTypeDef.Namespace);
                        }

                        if (attributeTypeName == "ReferenceAssemblyAttribute" && attributeTypeNamespace == "System.Runtime.CompilerServices")
                            return false;
                    }
                }
            }
            catch
            {
                return false;
            }

            return true;
        }

        private bool CreateR2RImage(ITaskItem file, out string outputR2RImage)
        {
            outputR2RImage = Path.Combine(OutputPath, file.GetMetadata(MetadataKeys.RelativePath));

            string dirName = Path.GetDirectoryName(outputR2RImage);
            if (!Directory.Exists(dirName))
                Directory.CreateDirectory(dirName);

            string arguments = $"/nologo " +
                $"/MissingDependenciesOK " +
                $"/JITPath \"{_clrjitPath}\" " +
                $"/Platform_Assemblies_Paths \"{_platformAssembliesPath}{_pathSeparatorCharacter}{Path.GetDirectoryName(file.ItemSpec)}\" " +
                $"/out \"{outputR2RImage}\" " +
                $"\"{file.ItemSpec}\"";

            return ExecuteTool(_crossgenPath, null, arguments) == 0;
        }

        private bool CreatePDBForImage(ITaskItem file, out string outputPDBImage)
        {
            outputPDBImage = null;

            string arguments = null;

            if (_targetPlatform == OSPlatform.Windows)
            {
                if (_diasymreaderPath == null || !File.Exists(_diasymreaderPath))
                    return false;

                outputPDBImage = Path.ChangeExtension(file.ItemSpec, "ni.pdb");

                arguments = $"/nologo " +
                    $"/Platform_Assemblies_Paths \"{_platformAssembliesPath}{_pathSeparatorCharacter}{Path.GetDirectoryName(file.ItemSpec)}\" " +
                    $"/DiasymreaderPath \"{_diasymreaderPath}\" " +
                    $"/CreatePDB \"{Path.GetDirectoryName(file.ItemSpec)}\" " +
                    $"\"{file.ItemSpec}\"";
            }
            else if(_targetPlatform == OSPlatform.Linux)
            {
                using (FileStream fs = new FileStream(file.ItemSpec, FileMode.Open, FileAccess.Read))
                {
                    PEReader pereader = new PEReader(fs);
                    MetadataReader mdReader = pereader.GetMetadataReader();
                    Guid mvid = mdReader.GetGuid(mdReader.GetModuleDefinition().Mvid);

                    outputPDBImage = Path.ChangeExtension(file.ItemSpec, "ni.{" + mvid + "}.map");
                }

                arguments = $"/nologo " +
                    $"/Platform_Assemblies_Paths \"{_platformAssembliesPath}{_pathSeparatorCharacter}{Path.GetDirectoryName(file.ItemSpec)}\" " +
                    $"/CreatePerfMap \"{Path.GetDirectoryName(file.ItemSpec)}\" " +
                    $"\"{file.ItemSpec}\"";
            }

            return arguments == null ? false : (ExecuteTool(_crossgenPath, null, arguments) == 0);
        }

        bool ExtractTargetPlatformAndArchitectureFromRuntimeIdentifier()
        {
            int separator = _runtimeIdentifier.LastIndexOf('-');
            if (separator < 0 || separator >= _runtimeIdentifier.Length)
            {
                return false;
            }

            string targetPlatform = _runtimeIdentifier.Substring(0, separator).ToLowerInvariant();
            string targetArchitecture = _runtimeIdentifier.Substring(separator + 1).ToLowerInvariant();

            if (targetPlatform.Contains("linux"))
                _targetPlatform = OSPlatform.Linux;
            else if (targetPlatform.Contains("osx"))
                _targetPlatform = OSPlatform.OSX;
            else if (targetPlatform.Contains("win"))
                _targetPlatform = OSPlatform.Windows;
            else
                return false;

            if (targetArchitecture == "arm")
                _targetArchitecture = Architecture.Arm;
            else if (targetArchitecture == "arm64")
                _targetArchitecture = Architecture.Arm64;
            else if (targetArchitecture == "x64")
                _targetArchitecture = Architecture.X64;
            else if (targetArchitecture == "x86")
                _targetArchitecture = Architecture.X86;
            else
                return false;

            return true;
        }

        bool GetCrossgenComponentsPaths(string runtimePackagePath)
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (_targetPlatform !=  OSPlatform.Windows)
                    return false;

                if (_targetArchitecture == Architecture.Arm)
                {
                    _crossgenPath = Path.Combine(runtimePackagePath, "tools", "x86_arm", "crossgen.exe");
                    _clrjitPath = Path.Combine(runtimePackagePath, "runtimes", "x86_arm", "native", "clrjit.dll");
                    _diasymreaderPath = Path.Combine(runtimePackagePath, "runtimes", _runtimeIdentifier, "native", "Microsoft.DiaSymReader.Native.x86.dll");
                }
                else if (_targetArchitecture == Architecture.Arm64)
                {
                    // We only have 64-bit hosted compilers for ARM64.
                    if (RuntimeInformation.OSArchitecture != Architecture.X64)
                        return false;

                    _crossgenPath = Path.Combine(runtimePackagePath, "tools", "x64_arm64", "crossgen.exe");
                    _clrjitPath = Path.Combine(runtimePackagePath, "runtimes", "x64_arm64", "native", "clrjit.dll");
                    _diasymreaderPath = Path.Combine(runtimePackagePath, "runtimes", _runtimeIdentifier, "native", "Microsoft.DiaSymReader.Native.amd64.dll");
                }
                else
                {
                    _crossgenPath = Path.Combine(runtimePackagePath, "tools", "crossgen.exe");
                    _clrjitPath = Path.Combine(runtimePackagePath, "runtimes", _runtimeIdentifier, "native", "clrjit.dll");
                    if(_targetArchitecture == Architecture.X64)
                        _diasymreaderPath = Path.Combine(runtimePackagePath, "runtimes", _runtimeIdentifier, "native", "Microsoft.DiaSymReader.Native.amd64.dll");
                    else
                        _diasymreaderPath = Path.Combine(runtimePackagePath, "runtimes", _runtimeIdentifier, "native", "Microsoft.DiaSymReader.Native.x86.dll");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _pathSeparatorCharacter = ":";

                if (_targetPlatform != OSPlatform.Linux)
                    return false;

                if (_targetArchitecture == Architecture.Arm || _targetArchitecture == Architecture.Arm64)
                {
                    // We only have x64 hosted crossgen for both ARM target architectures
                    if (RuntimeInformation.OSArchitecture != Architecture.X64)
                        return false;

                    string xarchPath = (_targetArchitecture == Architecture.Arm ? "x64_arm" : "x64_arm64");
                    _crossgenPath = Path.Combine(runtimePackagePath, "tools", xarchPath, "crossgen");
                    _clrjitPath = Path.Combine(runtimePackagePath, "runtimes", xarchPath, "native", "libclrjit.so");
                }
                else
                {
                    _crossgenPath = Path.Combine(runtimePackagePath, "tools", "crossgen");
                    _clrjitPath = Path.Combine(runtimePackagePath, "runtimes", _runtimeIdentifier, "native", "libclrjit.so");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _pathSeparatorCharacter = ":";

                // Only x64 supported for OSX
                if (_targetPlatform != OSPlatform.OSX || _targetArchitecture != Architecture.X64 || RuntimeInformation.OSArchitecture != Architecture.X64)
                    return false;

                _crossgenPath = Path.Combine(runtimePackagePath, "tools", "crossgen");
                _clrjitPath = Path.Combine(runtimePackagePath, "runtimes", _runtimeIdentifier, "native", "libclrjit.dylib");
            }
            else
            {
                // Unknown platform
                return false;
            }

            _platformAssembliesPath =
                Path.Combine(runtimePackagePath, "runtimes", _runtimeIdentifier, "native") + _pathSeparatorCharacter +
                Path.Combine(runtimePackagePath, "runtimes", _runtimeIdentifier, "lib", TargetFramework);

            return true;
        }
    }
}
