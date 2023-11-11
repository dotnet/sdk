// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
// See the LICENSE file in the project root for more information.

using Microsoft.Build.Framework;
using Microsoft.Build.Utilities;
using System;
#if !NETFRAMEWORK
using System.Formats.Tar;
#endif
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Microsoft.DotNet.Build.Tasks
{
    /// <summary>
    /// Extracts a .zip or .tar.gz file to a directory.
    /// </summary>
    public sealed class ExtractArchiveToDirectory : ToolTask
    {
        /// <summary>
        /// The path to the archive to extract.
        /// </summary>
        [Required]
        public string SourceArchive { get; set; }

        /// <summary>
        /// The path of the directory to which the archive should be extracted.
        /// </summary>
        [Required]
        public string DestinationDirectory { get; set; }

        /// <summary>
        /// Indicates if the destination directory should be cleaned if it already exists.
        /// </summary>
        public bool CleanDestination { get; set; }

        /// <summary>
        /// A list of directories, relative to the root of the archive to include. If empty all directories will be copied.
        /// </summary>
        public ITaskItem[] DirectoriesToCopy { get; set; }

        protected override bool ValidateParameters()
        {
            base.ValidateParameters();

            var retVal = true;

            if (Directory.Exists(DestinationDirectory))
            {
                if (CleanDestination == true)
                {
                    Log.LogMessage(MessageImportance.Low, "'{0}' already exists, trying to delete before unzipping...", DestinationDirectory);
                    Directory.Delete(DestinationDirectory, recursive: true);
                }
            }

            if (!File.Exists(SourceArchive))
            {
                Log.LogError($"SourceArchive '{SourceArchive} does not exist.");

                retVal = false;
            }

            if (retVal)
            {
                Log.LogMessage($"Creating Directory {DestinationDirectory}");
                Directory.CreateDirectory(DestinationDirectory);
            }
            
            return retVal;
        }

        public override bool Execute()
        {
            bool retVal = true;
            bool isZipArchive = Path.GetExtension(SourceArchive).Equals(".zip", StringComparison.OrdinalIgnoreCase);
            bool isTarballArchive = SourceArchive.EndsWith(".tar.gz", StringComparison.OrdinalIgnoreCase);

            //  Inherits from ToolTask in order to shell out to tar for complete extraction
            //  If the file is a .zip, then don't call the base Execute method, just run as a normal task
            //  If the file is a .tar.gz, and DirectoriesToCopy isn't empty, also run a normal task.
            if (isZipArchive || isTarballArchive)
            {
                if (ValidateParameters())
                {
                    if (DirectoriesToCopy != null && DirectoriesToCopy.Length != 0)
                    {
                        // Partial archive extraction
                        if (isZipArchive)
                        {
                            var zip = new ZipArchive(File.OpenRead(SourceArchive));
                            string loc = DestinationDirectory;
                            foreach (var entry in zip.Entries)
                            {
                                if (ShouldExtractItem(entry.FullName))
                                {
                                    if (!Directory.Exists(Path.Combine(DestinationDirectory, Path.GetDirectoryName(entry.FullName))))
                                    {
                                        Directory.CreateDirectory(Path.Combine(DestinationDirectory, Path.GetDirectoryName(entry.FullName)));
                                    }

                                    Log.LogMessage(Path.GetDirectoryName(entry.FullName));
                                    entry.ExtractToFile(Path.Combine(loc, entry.FullName));
                                }
                            }
                        }
                        else
                        {
#if NETFRAMEWORK
                            // Run the base tool, which uses external 'tar' command
                            retVal = base.Execute();
#else
                            // Decompress GZip content
                            using FileStream compressedFileStream = File.Open(SourceArchive, FileMode.Open);
                            using var decompressor = new GZipStream(compressedFileStream, CompressionMode.Decompress);
                            using var decompressedStream = new MemoryStream();
                            decompressor.CopyTo(decompressedStream);
                            decompressedStream.Seek(0, SeekOrigin.Begin);

                            // Extract Tar content
                            using TarReader tr = new TarReader(decompressedStream);
                            while (tr.GetNextEntry() is TarEntry tarEntry)
                            {
                                if (tarEntry.EntryType != TarEntryType.Directory)
                                {
                                    string entryName = tarEntry.Name;
                                    entryName = entryName.StartsWith("./") ? entryName[2..] : entryName;
                                    if (ShouldExtractItem(entryName))
                                    {
                                        Log.LogMessage(entryName);
                                        string destinationPath = Path.Combine(DestinationDirectory, entryName);
                                        Directory.CreateDirectory(Path.GetDirectoryName(destinationPath));
                                        tarEntry.ExtractToFile(destinationPath, overwrite: true);
                                    }
                                }
                            }
#endif
                        }
                    }
                    else
                    {
                        // Complete archive extraction
                        if (isZipArchive)
                        {
#if NETFRAMEWORK
                            //  .NET Framework doesn't have overload to overwrite files
                            ZipFile.ExtractToDirectory(SourceArchive, DestinationDirectory);
#else

                            ZipFile.ExtractToDirectory(SourceArchive, DestinationDirectory, overwriteFiles: true);
#endif
                        }
                        else
                        {
                            // Run the base tool, which uses external 'tar' command
                            retVal = base.Execute();
                        }
                    }
                }
                else
                {
                    retVal = false;
                }
            }
            else
            {
                retVal = base.Execute();
            }

            if (!retVal)
            {
                Log.LogMessage($"Deleting Directory {DestinationDirectory}");
                Directory.Delete(DestinationDirectory);
            }

            return retVal;
        }

        private bool ShouldExtractItem(string path)
        {
            if (DirectoriesToCopy != null)
            {
                return DirectoriesToCopy.Any(p => path.StartsWith(p.ItemSpec));

            }

            return false;
        }

        protected override string ToolName => "tar";

        protected override MessageImportance StandardOutputLoggingImportance => MessageImportance.High;

        protected override string GenerateFullPathToTool()
        {
            return "tar";
        }

        protected override string GenerateCommandLineCommands()
        {
            return $"xf {SourceArchive} -C {DestinationDirectory}";
        }
    }
}
