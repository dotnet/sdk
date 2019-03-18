// Copyright (c) .NET Foundation and contributors. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
        [Required]
        public ITaskItem[] KnownFrameworkReferences { get; set; }
        [Required]
        public string RuntimeGraphPath { get; set; }


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

        private Architecture _targetArchitecture;

        private bool _crossgenFailures = false;

        public override bool Execute()
        {
            // Get the list of runtime identifiers that we support and can target
            ITaskItem frameworkRef = KnownFrameworkReferences.Where(item => String.Compare(item.ItemSpec, "Microsoft.NETCore.App", true) == 0).SingleOrDefault();
            string supportedRuntimeIdentifiers = frameworkRef == null ? null : frameworkRef.GetMetadata("RuntimePackRuntimeIdentifiers");

            // Get information on the runtime package used for the current target
            ITaskItem frameworkPack = RuntimePacks.Where(pack => pack.ItemSpec.EndsWith(".Microsoft.NETCore.App", StringComparison.InvariantCultureIgnoreCase)).SingleOrDefault();
            _runtimeIdentifier = frameworkPack == null ? null : frameworkPack.GetMetadata(MetadataKeys.RuntimeIdentifier);
            _packagePath = frameworkPack == null ? null : frameworkPack.GetMetadata(MetadataKeys.PackageDirectory);

            var runtimeGraph = new RuntimeGraphCache(BuildEngine4, Log).GetRuntimeGraph(RuntimeGraphPath);
            var supportedRIDsList = supportedRuntimeIdentifiers == null ? Array.Empty<string>() : supportedRuntimeIdentifiers.Split(';');

            // Get the best RID for the host machine, which will be used to validate that we can run crossgen for the target platform and architecture
            string hostRuntimeIdentifier = NuGetUtils.GetBestMatchingRid(
                runtimeGraph, 
                DotNet.PlatformAbstractions.RuntimeEnvironment.GetRuntimeIdentifier(), 
                supportedRIDsList, 
                out bool wasInGraph);

            if (hostRuntimeIdentifier == null || _runtimeIdentifier == null || _packagePath == null)
            {
                Log.LogError(Strings.ReadyToRunNoValidRuntimePackageError);
                return false;
            }

            if (!ExtractTargetPlatformAndArchitecture(_runtimeIdentifier, out string targetPlatform, out _targetArchitecture) ||
                !ExtractTargetPlatformAndArchitecture(hostRuntimeIdentifier, out string hostPlatform, out Architecture hostArchitecture) ||
                targetPlatform != hostPlatform)
            {
                Log.LogError(Strings.ReadyToRunTargetNotSuppotedError);
                return false;
            }

            if (!GetCrossgenComponentsPaths() || !File.Exists(_crossgenPath) || !File.Exists(_clrjitPath))
            {
                Log.LogError(Strings.ReadyToRunTargetNotSuppotedError);
                return false;
            }

            LogStandardErrorAsError = true;

            // Run Crossgen on input assemblies
            ProcessInputFileList(FilesToPublishPreserveNewest, _r2rPublishPreserveNewest);
            ProcessInputFileList(FilesToPublishAlways, _r2rPublishAlways);

            if (_crossgenFailures && !HasLoggedErrors)
            {
                Log.LogError(Strings.ReadyToRunCompilationsFailure);
            }

            return !HasLoggedErrors;
        }

        void ProcessInputFileList(ITaskItem[] inputFiles, List<ITaskItem> outputFiles)
        {
            if (inputFiles == null)
            {
                return;
            }

            foreach (var file in inputFiles)
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
            using (FileStream fs = new FileStream(file.ItemSpec, FileMode.Open, FileAccess.Read))
            {
                try
                {
                    using (var pereader = new PEReader(fs))
                    {
                        if (!pereader.HasMetadata)
                            return false;

                        if ((pereader.PEHeaders.CorHeader.Flags & CorFlags.ILOnly) != CorFlags.ILOnly)
                            return false;

                        MetadataReader mdReader = pereader.GetMetadataReader();
                        if (!mdReader.IsAssembly)
                        {
                            return false;
                        }

                        // Skip reference assemblies and assemblies that reference winmds
                        if (ReferencesWinMD(mdReader) || IsReferenceAssembly(mdReader))
                        {
                            return false;
                        }
                    }
                }
                catch (BadImageFormatException)
                {
                    // Not a valid assembly file
                    return false;
                }
            }

            return true;
        }

        private bool IsReferenceAssembly(MetadataReader mdReader)
        {
            foreach (var attributeHandle in mdReader.GetAssemblyDefinition().GetCustomAttributes())
            {
                EntityHandle attributeCtor = mdReader.GetCustomAttribute(attributeHandle).Constructor;

                StringHandle attributeTypeName = default;
                StringHandle attributeTypeNamespace = default;

                if (attributeCtor.Kind == HandleKind.MemberReference)
                {
                    EntityHandle attributeMemberParent = mdReader.GetMemberReference((MemberReferenceHandle)attributeCtor).Parent;
                    if (attributeMemberParent.Kind == HandleKind.TypeReference)
                    {
                        TypeReference attributeTypeRef = mdReader.GetTypeReference((TypeReferenceHandle)attributeMemberParent);
                        attributeTypeName = attributeTypeRef.Name;
                        attributeTypeNamespace = attributeTypeRef.Namespace;
                    }
                }
                else if (attributeCtor.Kind == HandleKind.MethodDefinition)
                {
                    TypeDefinitionHandle attributeTypeDefHandle = mdReader.GetMethodDefinition((MethodDefinitionHandle)attributeCtor).GetDeclaringType();
                    TypeDefinition attributeTypeDef = mdReader.GetTypeDefinition(attributeTypeDefHandle);
                    attributeTypeName = attributeTypeDef.Name;
                    attributeTypeNamespace = attributeTypeDef.Namespace;
                }

                if (!attributeTypeName.IsNil && 
                    !attributeTypeNamespace.IsNil && 
                    mdReader.StringComparer.Equals(attributeTypeName, "ReferenceAssemblyAttribute") && 
                    mdReader.StringComparer.Equals(attributeTypeNamespace, "System.Runtime.CompilerServices"))
                {
                    return true;
                }
            }

            return false;
        }

        private bool ReferencesWinMD(MetadataReader mdReader)
        {
            foreach (var assemblyRefHandle in mdReader.AssemblyReferences)
            {
                AssemblyReference assemblyRef = mdReader.GetAssemblyReference(assemblyRefHandle);
                if ((assemblyRef.Flags & AssemblyFlags.WindowsRuntime) == AssemblyFlags.WindowsRuntime)
                    return true;
            }

            return false;
        }

        private bool CreateR2RImage(ITaskItem file, out string outputR2RImage)
        {
            outputR2RImage = Path.Combine(OutputPath, file.GetMetadata(MetadataKeys.RelativePath));

            string dirName = Path.GetDirectoryName(outputR2RImage);
            Directory.CreateDirectory(dirName);

            string arguments = $"/nologo " +
                $"/MissingDependenciesOK " +
                $"/JITPath \"{_clrjitPath}\" " +
                $"/Platform_Assemblies_Paths \"{_platformAssembliesPath}{_pathSeparatorCharacter}{Path.GetDirectoryName(file.ItemSpec)}\" " +
                $"/out \"{outputR2RImage}\" " +
                $"\"{file.ItemSpec}\"";

            if (ExecuteTool(_crossgenPath, null, arguments) != 0)
            {
                _crossgenFailures = true;
                return false;
            }

            return true;
        }

        private bool CreatePDBForImage(ITaskItem file, out string outputPDBImage)
        {
            outputPDBImage = null;
            string arguments = null;

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && _diasymreaderPath != null && File.Exists(_diasymreaderPath))
            {
                outputPDBImage = Path.ChangeExtension(file.ItemSpec, "ni.pdb");

                arguments = $"/nologo " +
                    $"/Platform_Assemblies_Paths \"{_platformAssembliesPath}{_pathSeparatorCharacter}{Path.GetDirectoryName(file.ItemSpec)}\" " +
                    $"/DiasymreaderPath \"{_diasymreaderPath}\" " +
                    $"/CreatePDB \"{Path.GetDirectoryName(file.ItemSpec)}\" " +
                    $"\"{file.ItemSpec}\"";
            }
            else if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
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

            if (arguments == null)
            {
                // Not a failure. Just a platform where we don't emit native symbols (ex: OSX)
                return false;
            }

            if (ExecuteTool(_crossgenPath, null, arguments) != 0)
            {
                _crossgenFailures = true;
                return false;
            }

            return true;
        }

        bool ExtractTargetPlatformAndArchitecture(string runtimeIdentifier, out string platform, out Architecture architecture)
        {
            platform = null;
            architecture = default;

            int separator = runtimeIdentifier.LastIndexOf('-');
            if (separator < 0 || separator >= runtimeIdentifier.Length)
            {
                return false;
            }

            platform = runtimeIdentifier.Substring(0, separator).ToLowerInvariant();
            string architectureStr = runtimeIdentifier.Substring(separator + 1).ToLowerInvariant();

            switch (architectureStr)
            {
                case "arm":
                    architecture = Architecture.Arm;
                    break;
                case "arm64":
                    architecture = Architecture.Arm64;
                    break;
                case "x64":
                    architecture = Architecture.X64;
                    break;
                case "x86":
                    architecture = Architecture.X86;
                    break;
                default:
                    return false;
            }

            return true;
        }

        bool GetCrossgenComponentsPaths()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                if (_targetArchitecture == Architecture.Arm)
                {
                    _crossgenPath = Path.Combine(_packagePath, "tools", "x86_arm", "crossgen.exe");
                    _clrjitPath = Path.Combine(_packagePath, "runtimes", "x86_arm", "native", "clrjit.dll");
                    _diasymreaderPath = Path.Combine(_packagePath, "runtimes", _runtimeIdentifier, "native", "Microsoft.DiaSymReader.Native.x86.dll");
                }
                else if (_targetArchitecture == Architecture.Arm64)
                {
                    // We only have 64-bit hosted compilers for ARM64.
                    if (RuntimeInformation.OSArchitecture != Architecture.X64)
                        return false;

                    _crossgenPath = Path.Combine(_packagePath, "tools", "x64_arm64", "crossgen.exe");
                    _clrjitPath = Path.Combine(_packagePath, "runtimes", "x64_arm64", "native", "clrjit.dll");
                    _diasymreaderPath = Path.Combine(_packagePath, "runtimes", _runtimeIdentifier, "native", "Microsoft.DiaSymReader.Native.amd64.dll");
                }
                else
                {
                    _crossgenPath = Path.Combine(_packagePath, "tools", "crossgen.exe");
                    _clrjitPath = Path.Combine(_packagePath, "runtimes", _runtimeIdentifier, "native", "clrjit.dll");
                    if(_targetArchitecture == Architecture.X64)
                        _diasymreaderPath = Path.Combine(_packagePath, "runtimes", _runtimeIdentifier, "native", "Microsoft.DiaSymReader.Native.amd64.dll");
                    else
                        _diasymreaderPath = Path.Combine(_packagePath, "runtimes", _runtimeIdentifier, "native", "Microsoft.DiaSymReader.Native.x86.dll");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                _pathSeparatorCharacter = ":";

                if (_targetArchitecture == Architecture.Arm || _targetArchitecture == Architecture.Arm64)
                {
                    // We only have x64 hosted crossgen for both ARM target architectures
                    if (RuntimeInformation.OSArchitecture != Architecture.X64)
                        return false;

                    string xarchPath = (_targetArchitecture == Architecture.Arm ? "x64_arm" : "x64_arm64");
                    _crossgenPath = Path.Combine(_packagePath, "tools", xarchPath, "crossgen");
                    _clrjitPath = Path.Combine(_packagePath, "runtimes", xarchPath, "native", "libclrjit.so");
                }
                else
                {
                    _crossgenPath = Path.Combine(_packagePath, "tools", "crossgen");
                    _clrjitPath = Path.Combine(_packagePath, "runtimes", _runtimeIdentifier, "native", "libclrjit.so");
                }
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                _pathSeparatorCharacter = ":";

                // Only x64 supported for OSX
                if (_targetArchitecture != Architecture.X64 || RuntimeInformation.OSArchitecture != Architecture.X64)
                    return false;

                _crossgenPath = Path.Combine(_packagePath, "tools", "crossgen");
                _clrjitPath = Path.Combine(_packagePath, "runtimes", _runtimeIdentifier, "native", "libclrjit.dylib");
            }
            else
            {
                // Unknown platform
                return false;
            }

            _platformAssembliesPath =
                Path.Combine(_packagePath, "runtimes", _runtimeIdentifier, "native") + _pathSeparatorCharacter +
                Path.Combine(_packagePath, "runtimes", _runtimeIdentifier, "lib", TargetFramework);

            return true;
        }
    }
}
