// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection.Metadata;
using System.Reflection.PortableExecutable;
using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;

namespace Microsoft.DotNet.Build.Tasks
{
    // Creates a symbols layout that matches the SDK layout
    public class CreateSdkSymbolsLayout : Task
    {
        /// <summary>
        /// Path to SDK layout.
        /// </summary>
        [Required]
        public string SdkLayoutPath { get; set; }

        /// <summary>
        /// Path to all source-built symbols, flat or with folder hierarchy.
        /// </summary>
        [Required]
        public string AllSymbolsPath { get; set; }

        /// <summary>
        /// Path to SDK symbols layout - will be created if it doesn't exist.
        /// </summary>
        [Required]
        public string SdkSymbolsLayoutPath { get; set; }

        /// <summary>
        /// If true, fails the build if any PDBs are missing.
        /// </summary>
        public bool FailOnMissingPDBs { get; set; }

        public override bool Execute()
        {
            IList<string> filesWithoutPDBs = GenerateSymbolsLayout(IndexAllSymbols());
            if (filesWithoutPDBs.Count > 0)
            {
                LogErrorOrWarning(FailOnMissingPDBs, $"Did not find PDBs for the following SDK files:");
                foreach (string file in filesWithoutPDBs)
                {
                    LogErrorOrWarning(FailOnMissingPDBs, file);
                }
           }

            return !Log.HasLoggedErrors;
        }

        private void LogErrorOrWarning(bool isError, string message)
        {
            if (FailOnMissingPDBs)
            {
                Log.LogError(message);
            }
            else
            {
                Log.LogWarning(message);
            }
        }

        private IList<string> GenerateSymbolsLayout(Hashtable allPdbGuids)
        {
            List<string> filesWithoutPDBs = new List<string>();

            if (Directory.Exists(SdkSymbolsLayoutPath))
            {
                Directory.Delete(SdkSymbolsLayoutPath, true);
            }

            foreach (string file in Directory.GetFiles(SdkLayoutPath, "*", SearchOption.AllDirectories))
            {
                if (file.EndsWith(".dll", StringComparison.InvariantCultureIgnoreCase) &&
                    !file.EndsWith(".resources.dll", StringComparison.InvariantCultureIgnoreCase))
                {
                    string guid = string.Empty;
                    using var pdbStream = File.OpenRead(file);
                    using var peReader = new PEReader(pdbStream);
                    try
                    {
                        // Check if pdb is embedded
                        if (peReader.ReadDebugDirectory().Any(entry => entry.Type == DebugDirectoryEntryType.EmbeddedPortablePdb))
                        {
                            continue;
                        }

                        var debugDirectory = peReader.ReadDebugDirectory().First(entry => entry.Type == DebugDirectoryEntryType.CodeView);
                        var codeViewData = peReader.ReadCodeViewDebugDirectoryData(debugDirectory);
                        guid = $"{codeViewData.Guid.ToString("N").Replace("-", string.Empty)}";
                    }
                    catch (Exception e) when (e is BadImageFormatException || e is InvalidOperationException)
                    {
                        // Ignore binaries without debug info
                        continue;
                    }

                    if (guid != string.Empty)
                    {
                        if (!allPdbGuids.ContainsKey(guid))
                        {
                            filesWithoutPDBs.Add(file.Substring(SdkLayoutPath.Length + 1));
                        }
                        else
                        {
                            // Copy matching pdb to symbols path, preserving sdk binary's hierarchy
                            string sourcePath = (string)allPdbGuids[guid]!;
                            string destinationPath =
                                file.Replace(SdkLayoutPath, SdkSymbolsLayoutPath)
                                    .Replace(Path.GetFileName(file), Path.GetFileName(sourcePath));

                            Directory.CreateDirectory(Path.GetDirectoryName(destinationPath)!);
                            File.Copy(sourcePath, destinationPath, true);
                        }
                    }
                }
            }

            return filesWithoutPDBs;
        }

        public Hashtable IndexAllSymbols()
        {
            Hashtable allPdbGuids = new Hashtable();

            foreach (string file in Directory.GetFiles(AllSymbolsPath, "*.pdb", SearchOption.AllDirectories))
            {
                using var pdbFileStream = File.OpenRead(file);
                var metadataProvider = MetadataReaderProvider.FromPortablePdbStream(pdbFileStream);
                var metadataReader = metadataProvider.GetMetadataReader();
                if (metadataReader.DebugMetadataHeader == null)
                {
                    continue;
                }

                var id = new BlobContentId(metadataReader.DebugMetadataHeader.Id);
                string guid = $"{id.Guid:N}";
                if (!string.IsNullOrEmpty(guid) && !allPdbGuids.ContainsKey(guid))
                {
                    allPdbGuids.Add(guid, file);
                }
            }

            return allPdbGuids;
        }
    }
}
